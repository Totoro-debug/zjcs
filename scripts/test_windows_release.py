"""Release isolation, incremental reuse, ownership and save protection checks."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import unittest

from publish_windows import publish
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
        for name in ('release_windows.ps1', 'unity_internal_display.ps1', 'validation_workspace.ps1'):
            shutil.copyfile(Path(__file__).parent/name, scripts/name)
        (repository/'unity/Assets').mkdir(parents=True)
        (repository/'assets/reference').mkdir(parents=True)
        (repository/'assets/reference/receipt.json').write_text('{}')
        asset = repository/'unity/Assets/source.txt'
        asset.write_text('committed')
        git('add', '.')
        git('commit', '-qm', 'fixture')
        shell = shutil.which('pwsh') or 'powershell'
        def prepare(ok=True):
            result = subprocess.run([shell, '-NoProfile', '-File', str(scripts/'release_windows.ps1'), '-PrepareOnly'],
                                    capture_output=True, text=True, encoding='utf-8', errors='replace')
            if ok:
                self.assertEqual(result.returncode, 0, result.stdout+result.stderr)
            else:
                self.assertNotEqual(result.returncode, 0)
        asset.write_text('uncommitted')
        prepare()
        workspace = Path(json.loads((repository/'output/windows-release.json').read_text(encoding='utf-8-sig'))['workspace'])
        copied = workspace/'source/unity/Assets/source.txt'
        self.assertEqual(copied.read_text(), 'committed')
        stamp = copied.stat().st_mtime_ns
        cache = workspace/'source/unity/Library/cache'
        cache.parent.mkdir()
        cache.write_text('retained')
        prepare()
        self.assertEqual(copied.stat().st_mtime_ns, stamp)
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


if __name__ == '__main__':
    unittest.main()
