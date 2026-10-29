import json
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
import unity_asset_pack as pack


class UnityAssetPackTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name) / 'unity'
        self.payload = self.root / 'Assets' / 'Official' / 'fixture'
        self.payload.mkdir(parents=True)
        (self.root / 'ProjectSettings').mkdir()
        (self.root / 'ProjectSettings/ProjectVersion.txt').write_text(
            'm_EditorVersion: 2022.3.57f1\n', encoding='utf8')
        (self.payload / 'source-manifest.json').write_text('{}\n', encoding='utf8')
        (self.payload / 'sprite.png').write_bytes(b'official image bytes')
        (self.payload / 'sprite.png.meta').write_text('guid: stable-guid\n', encoding='utf8')
        self.output = Path(self.temp.name) / 'packs'

    def tearDown(self):
        self.temp.cleanup()

    def test_snapshot_verify_and_restore(self):
        result = pack.snapshot(self.root, self.output)
        archive = Path(result['archive'])
        self.assertEqual(pack.verify(archive, self.root)['errors'], [])

        restored = Path(self.temp.name) / 'restored'
        (restored / 'ProjectSettings').mkdir(parents=True)
        (restored / 'ProjectSettings/ProjectVersion.txt').write_text(
            'm_EditorVersion: 2022.3.57f1\n', encoding='utf8')
        self.assertEqual(pack.restore(archive, restored)['restored'], 3)
        self.assertEqual(pack.verify(archive, restored)['errors'], [])

    def test_restore_rejects_conflicting_files_without_replace(self):
        archive = Path(pack.snapshot(self.root, self.output)['archive'])
        (self.payload / 'sprite.png').write_bytes(b'local edit')
        with self.assertRaisesRegex(ValueError, 'Existing Unity assets differ'):
            pack.restore(archive, self.root)
        self.assertEqual((self.payload / 'sprite.png').read_bytes(), b'local edit')
        pack.restore(archive, self.root, replace=True)
        self.assertEqual((self.payload / 'sprite.png').read_bytes(), b'official image bytes')

    def test_archive_hashes_are_verified_before_restore(self):
        archive = Path(pack.snapshot(self.root, self.output)['archive'])
        corrupt = self.output / 'corrupt.zip'
        with zipfile.ZipFile(archive) as source, zipfile.ZipFile(corrupt, 'w') as target:
            for item in source.infolist():
                data = source.read(item.filename)
                if item.filename.endswith('sprite.png'):
                    data = b'altered image bytes!'
                target.writestr(item.filename, data)
        with self.assertRaisesRegex(ValueError, 'SHA256 mismatch'):
            pack.read_archive(corrupt)

    def test_archive_rejects_paths_outside_official_payload(self):
        archive = self.output / 'outside.zip'
        self.output.mkdir(parents=True, exist_ok=True)
        manifest = {'format': 1, 'unity_version': '2022.3.57f1',
                    'files': {'Assets/Resources/payload.bin': {'bytes': 1, 'sha256': '0' * 64}}}
        with zipfile.ZipFile(archive, 'w') as target:
            target.writestr(pack.MANIFEST, json.dumps(manifest))
            target.writestr('Assets/Resources/payload.bin', b'x')
        with self.assertRaisesRegex(ValueError, 'Invalid Unity official payload path'):
            pack.read_archive(archive)

    def test_restore_rejects_different_unity_editor_version(self):
        archive = Path(pack.snapshot(self.root, self.output)['archive'])
        (self.root / 'ProjectSettings/ProjectVersion.txt').write_text(
            'm_EditorVersion: 2022.3.57f1c2\n', encoding='utf8')
        with self.assertRaisesRegex(ValueError, 'editor version differs'):
            pack.restore(archive, self.root)


if __name__ == '__main__':
    unittest.main()
