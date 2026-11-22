"""Release isolation, incremental reuse, ownership and save protection checks."""
import json
import hashlib
import os
from pathlib import Path
import shutil
import subprocess
import unittest

from publish_windows import publish
from release_source_bytes import restore
from validation_workspace import temporary_validation_directory


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = temporary_validation_directory('release-tooling-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def test_publish_delta_deletion_and_saves(self):
        source, delivery = self.root/'build', self.root/'delivery'
        (source/'Zjcs_Data').mkdir(parents=True)
        (source/'Zjcs.exe').write_bytes(b'player')
        (source/'Zjcs_Data/old.asset').write_bytes(b'old')
        (delivery/'saves').mkdir(parents=True)
        (delivery/'saves/user.json').write_bytes(b'user-save')
        (delivery/'profile-selection.txt').write_bytes(b'profile')
        self.assertEqual(publish(source, delivery), 2)
        stamp = (delivery/'Zjcs.exe').stat().st_mtime_ns
        self.assertEqual(publish(source, delivery), 0)
        self.assertEqual((delivery/'Zjcs.exe').stat().st_mtime_ns, stamp)
        (source/'Zjcs_Data/old.asset').unlink()
        (source/'Zjcs_Data/new.asset').write_bytes(b'new')
        self.assertEqual(publish(source, delivery), 1)
        self.assertFalse((delivery/'Zjcs_Data/old.asset').exists())
        self.assertEqual((delivery/'saves/user.json').read_bytes(), b'user-save')
        self.assertEqual((delivery/'profile-selection.txt').read_bytes(), b'profile')
        (delivery/'release-files.json').write_text(json.dumps({'../outside': 'x'}))
        with self.assertRaises(ValueError):
            publish(source, delivery)

    @unittest.skipUnless(os.name == 'nt', 'PowerShell Windows release workflow')
    def test_committed_source_and_persistent_cache(self):
        repository = self.root/'repository'
        repository.mkdir()
        def git(*args):
            return subprocess.check_output(['git', '-C', str(repository), *args], text=True).strip()
        git('init', '-q')
        git('config', 'user.name', 'Release test')
        git('config', 'user.email', 'release@example.invalid')
        (repository/'.gitignore').write_text('/output/\n/unity/Library/\n')
        scripts = repository/'scripts'
        scripts.mkdir()
        for name in ('release_windows.ps1', 'unity_internal_display.ps1', 'validation_workspace.ps1',
                     'committed_unity_snapshot.py', 'release_source_bytes.py'):
            shutil.copyfile(Path(__file__).parent/name, scripts/name)
        (repository/'unity/Assets').mkdir(parents=True)
        (repository/'assets/reference').mkdir(parents=True)
        (repository/'assets/reference/receipt.json').write_text('{}')
        # The real entry checks these provenance receipts even in prepare mode.
        gradient = repository/'unity/Assets/Official/Skills/skill-quality-gradient.json'
        gradient.parent.mkdir(parents=True)
        gradient.write_bytes(b'{}\n')
        (gradient.parent/'source-manifest.json').write_text(json.dumps({'files': [{
            'path': 'Assets/Official/Skills/skill-quality-gradient.json', 'bytes': 3,
            'sha256': hashlib.sha256(b'{}\n').hexdigest()}]}))
        equipment = repository/'unity/Assets/Official/Equipment/Runtime'
        equipment.mkdir(parents=True)
        (equipment/'equipment-runtime-rules.json').write_bytes(b'{}\n')
        (equipment/'source-manifest.json').write_text(json.dumps({'output': {
            'bytes': 3, 'sha256': hashlib.sha256(b'{}\n').hexdigest()}}))
        ui = gradient.parent/'skill-ui.json'
        ui.write_bytes(b'{"native":true}\n')
        meta = Path(str(ui) + '.meta')
        meta.write_bytes(b'fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n')
        expected_ui, expected_meta = ui.read_bytes().replace(b'\n', b'\r\n'), meta.read_bytes().replace(b'\n', b'\r\n')
        (repository/'assets/reference/skill-ui-native-data-source-manifest.json').write_text(json.dumps({'inputs': [{
            'path': 'Assets/Official/Skills/skill-ui.json', 'bytes': len(expected_ui),
            'sha256': hashlib.sha256(expected_ui).hexdigest(), 'meta_sha256': hashlib.sha256(expected_meta).hexdigest()}]}))
        asset = repository/'unity/Assets/source.txt'
        asset.write_text('committed')
        git('add', '.')
        git('commit', '-qm', 'fixture')
        shell = shutil.which('pwsh') or 'powershell'
        def prepare(ok=True):
            result = subprocess.run([shell, '-NoProfile', '-File', str(scripts/'release_windows.ps1'), '-PrepareOnly', '-SourceSnapshot'],
                                    capture_output=True, text=True, encoding='utf-8', errors='replace')
            if ok:
                self.assertEqual(result.returncode, 0, result.stdout+result.stderr)
            else:
                self.assertNotEqual(result.returncode, 0)
        asset.write_text('uncommitted')
        prepare()
        workspace = Path(json.loads((repository/'output/windows-release-snapshot.json').read_text(encoding='utf-8-sig'))['workspace'])
        self.assertFalse((workspace/'source/.git').exists())
        self.assertEqual(git('worktree', 'list', '--porcelain').count('worktree '), 1)
        copied = workspace/'source/unity/Assets/source.txt'
        self.assertEqual(copied.read_text(), 'committed')
        copied_ui = workspace/'source/unity/Assets/Official/Skills/skill-ui.json'
        self.assertEqual(copied_ui.read_bytes(), expected_ui)
        self.assertEqual(Path(str(copied_ui)+'.meta').read_bytes(), expected_meta)
        ui_stamp = copied_ui.stat().st_mtime_ns
        stamp = copied.stat().st_mtime_ns
        cache = workspace/'source/unity/Library/cache'
        cache.parent.mkdir()
        cache.write_text('retained')
        prepare()
        self.assertEqual(copied.stat().st_mtime_ns, stamp)
        self.assertEqual(copied_ui.stat().st_mtime_ns, ui_stamp)
        asset.write_text('next commit')
        git('add', '.')
        git('commit', '-qm', 'next')
        prepare()
        self.assertEqual(copied.read_text(), 'next commit')
        self.assertEqual(cache.read_text(), 'retained')
        # Unexpected input must stop publication rather than enter the build.
        (copied.parent/'unexpected.cs').write_text('class Unexpected {}')
        prepare(ok=False)
        # A second invocation must not enter a project owned by another build.
        (copied.parent/'unexpected.cs').unlink()
        import msvcrt
        with (repository/'output/windows-release.lock').open('r+b') as lock:
            msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
            try:
                prepare(ok=False)
            finally:
                msvcrt.locking(lock.fileno(), msvcrt.LK_UNLCK, 1)

    def test_source_corruption_cannot_change_expected_hash(self):
        asset = self.root/'rules.json'
        expected = b'{"official":true}\r\n'
        digest = hashlib.sha256(expected).hexdigest()
        asset.write_bytes(expected.replace(b'\r\n', b'\n'))
        self.assertTrue(restore(asset, digest, len(expected)))
        self.assertEqual(asset.read_bytes(), expected)
        asset.write_bytes(b'{"official":false}\n')
        with self.assertRaisesRegex(ValueError, 'verified receipt'):
            restore(asset, digest, len(expected))
        self.assertEqual(asset.read_bytes(), b'{"official":false}\n')


if __name__ == '__main__':
    unittest.main()
