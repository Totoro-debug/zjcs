"""Source isolation and cache reuse tests for the worktree-free Unity exporter."""
import json
import hashlib
import io
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch

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

    def test_first_archive_drains_padding_before_waiting_for_git(self):
        archive = subprocess.check_output(['git', '-C', str(self.repository), 'archive', 'HEAD', '--', 'unity/Assets'])
        payload = archive + bytes(64 * 1024)

        class PaddedArchive:
            def __init__(self):
                self.stdout = io.BytesIO(payload)
                self.returncode = None

            def wait(self):
                if self.returncode is None:
                    # A Windows pipe writer can still be blocked on this tail
                    # after tarfile has recognized the archive terminator.
                    if self.stdout.tell() != len(payload):
                        raise AssertionError('Git archive output was not drained before waiting')
                    self.returncode = 0
                return self.returncode

            def poll(self):
                return self.returncode

            def terminate(self):
                self.returncode = -1

        process = PaddedArchive()
        original_popen = subprocess.Popen

        def start(arguments, *args, **kwargs):
            if 'archive' in arguments:
                return process
            return original_popen(arguments, *args, **kwargs)

        with patch('committed_unity_snapshot.subprocess.Popen', side_effect=start):
            root = snapshot(self.repository, self.workspace)
        self.assertEqual(process.returncode, 0)
        self.assertTrue(process.stdout.closed)
        self.assertEqual((root / self.asset.relative_to(self.repository)).read_bytes(), b'// original\nclass Test {}\n')

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

    def test_links_are_rejected_with_cached_parents(self):
        root = self.workspace
        verified = set()
        name = 'source/unity/Assets/Native/asset.cs'
        target = checked_path(root, name, verified)
        # Inventory may already have checked the parent when a source file is
        # replaced by a link. Each subsequent read/write must still refuse it.
        for predicate in ('is_symlink', 'is_junction'):
            with self.subTest(predicate=predicate):
                with patch.object(type(root), predicate, new=lambda path: path == target):
                    with self.assertRaisesRegex(ValueError, 'Linked snapshot path'):
                        checked_path(root, name, verified)
                # A new subtree below a verified directory needs its own check.
                linked = target.parent / 'linked'
                with patch.object(type(root), predicate, new=lambda path: path == linked):
                    with self.assertRaisesRegex(ValueError, 'Linked snapshot path'):
                        checked_path(root, 'source/unity/Assets/Native/linked/asset.cs', verified)
        for name in ('', '.', 'source/unity/Assets/Native/other.cs'):
            self.assertEqual(checked_path(root, name, verified), root / name)

    def test_resume_interrupted_incremental_export(self):
        root = snapshot(self.repository, self.workspace)
        added = self.asset.with_name('Added.cs')
        added.write_bytes(b'class Added {}\n')
        commit = self.commit('add source')
        with patch('committed_unity_snapshot.restore_lfs', side_effect=RuntimeError('interrupted')):
            with self.assertRaisesRegex(RuntimeError, 'interrupted'):
                snapshot(self.repository, self.workspace, commit)
        self.assertTrue((root / added.relative_to(self.repository)).exists())
        snapshot(self.repository, self.workspace, commit)
        self.assertEqual((root / added.relative_to(self.repository)).read_bytes(), b'class Added {}\n')


if __name__ == '__main__':
    unittest.main()
