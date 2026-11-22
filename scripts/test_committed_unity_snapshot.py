"""Source isolation and cache reuse tests for the worktree-free Unity exporter."""
import json
import hashlib
from pathlib import Path
import subprocess
import unittest

from committed_unity_snapshot import checked_path, snapshot
from validation_workspace import temporary_validation_directory


class SnapshotTests(unittest.TestCase):
    def setUp(self):
        self.temp = temporary_validation_directory('unity-snapshot-')
        self.addCleanup(self.temp.cleanup)
        self.repository = Path(self.temp.name) / 'repository'
        self.repository.mkdir()
        self.git('init', '-q')
        self.git('config', 'user.name', 'Snapshot fixture')
        self.git('config', 'user.email', 'snapshot@example.invalid')
        self.git('config', 'core.autocrlf', 'true')
        self.workspace = self.repository / 'output/validation/snapshot-fixture'
        self.workspace.mkdir(parents=True)
        (self.workspace / '.zjcs-validation.json').write_text(json.dumps(
            dict(format=1, repository=str(self.repository))), 'utf8')
        (self.repository / '.gitignore').write_text('/output/\n')
        self.asset = self.repository / 'unity/Assets/测试.cs'
        self.asset.parent.mkdir(parents=True)
        self.asset.write_bytes(b'// original\nclass Test {}\n')
        self.commit('initial')

    def git(self, *arguments):
        return subprocess.check_output(['git', '-C', str(self.repository), *arguments]).decode().strip()

    def commit(self, message):
        self.git('add', '.')
        self.git('commit', '-qm', message)
        return self.git('rev-parse', 'HEAD')

    def test_committed_bytes_incremental_restore_and_no_worktree(self):
        before = self.git('worktree', 'list', '--porcelain')
        self.asset.write_bytes(b'// uncommitted\r\n')
        root = snapshot(self.repository, self.workspace)
        copied = root / self.asset.relative_to(self.repository)
        self.assertEqual(copied.read_bytes(), b'// original\nclass Test {}\n')
        self.assertFalse((root / '.git').exists())
        self.assertEqual(self.git('worktree', 'list', '--porcelain'), before)
        stamp = copied.stat().st_mtime_ns
        cache = root / 'unity/Library/cache'
        cache.parent.mkdir()
        cache.write_bytes(b'independent import cache')
        snapshot(self.repository, self.workspace)
        self.assertEqual(copied.stat().st_mtime_ns, stamp)
        copied.write_bytes(b'// mutated by build')
        snapshot(self.repository, self.workspace)
        self.assertEqual(copied.read_bytes(), b'// original\nclass Test {}\n')
        extra = copied.with_name('unexpected.cs')
        extra.write_bytes(b'class Unexpected {}')
        with self.assertRaisesRegex(ValueError, 'Unexpected snapshot input'):
            snapshot(self.repository, self.workspace)
        extra.unlink()
        self.asset.unlink()
        replacement = self.asset.with_name('Next.cs')
        replacement.write_bytes(b'class Next {}\n')
        commit = self.commit('replace source')
        snapshot(self.repository, self.workspace, commit)
        self.assertFalse(copied.exists())
        self.assertEqual((copied.parent / 'Next.cs').read_bytes(), b'class Next {}\n')
        self.assertEqual(cache.read_bytes(), b'independent import cache')
        self.assertEqual(self.git('worktree', 'list', '--porcelain').replace(commit, 'COMMIT'),
                         before.replace(before.split('HEAD ')[1].splitlines()[0], 'COMMIT'))

    def test_paths_and_workspace_ownership(self):
        for name in ('../outside', '/absolute', 'C:/outside', 'unity\\outside'):
            with self.assertRaises(ValueError):
                checked_path(self.workspace, name)
        with self.assertRaises(ValueError):
            snapshot(self.repository, self.repository)
        (self.workspace / '.zjcs-validation.json').write_text(json.dumps(
            dict(format=1, repository=str(self.workspace))), 'utf8')
        with self.assertRaisesRegex(ValueError, 'another repository'):
            snapshot(self.repository, self.workspace)

    def test_explicit_line_endings_and_lfs_payload(self):
        (self.repository / '.gitattributes').write_text('*.cs text eol=crlf\n')
        payload = b'local native texture payload\0' * 100
        digest = hashlib.sha256(payload).hexdigest()
        lfs = self.repository / '.git/lfs/objects' / digest[:2] / digest[2:4] / digest
        lfs.parent.mkdir(parents=True)
        lfs.write_bytes(payload)
        asset = self.asset.with_name('Texture.asset')
        asset.write_bytes(('version https://git-lfs.github.com/spec/v1\n'
                           f'oid sha256:{digest}\nsize {len(payload)}\n').encode())
        self.commit('native payload')
        root = snapshot(self.repository, self.workspace)
        self.assertEqual((root / self.asset.relative_to(self.repository)).read_bytes(),
                         b'// original\nclass Test {}\n')
        self.assertEqual((root / asset.relative_to(self.repository)).read_bytes(), payload)


if __name__ == '__main__':
    unittest.main()
