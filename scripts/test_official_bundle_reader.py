"""Exercise source priority, integrity, identity, lifetime and parser failures."""
import hashlib
import json
from pathlib import Path
from types import SimpleNamespace
import unittest
import zipfile

from official_bundle_reader import OfficialBundleReader
from validation_workspace import temporary_validation_directory


class BundleReaderTests(unittest.TestCase):
    def setUp(self):
        self.workspace = temporary_validation_directory('bundle-reader-')
        self.addCleanup(self.workspace.cleanup)
        self.root = Path(self.workspace.name)
        self.data = b'official raw bundle bytes'
        self.bundle = dict(name='sample.bundle', file='sample.bundle', size=len(self.data),
                           hash=hashlib.md5(self.data).hexdigest())
        self.apk = self.root / 'source.apk'
        self.cloud = self.root / 'cloud'
        self.cloud.mkdir()
        self.write_apk(self.data)

    def write_apk(self, payload):
        with zipfile.ZipFile(self.apk, 'w') as archive:
            if payload is not None:
                archive.writestr('assets/yoo/DefaultPackage/sample.bundle', payload)
        self.source = SimpleNamespace(apk=self.apk, apk_sha256=hashlib.sha256(self.apk.read_bytes()).hexdigest(),
            version='selected', apk_version='apk', cloud_path=self.cloud, profile={},
            manifests={'DefaultPackage': {'version': 'selected'}},
            manifest=lambda package, archive: {'bundles': [self.bundle]})

    def write_cloud(self, data=None):
        data = self.data if data is None else data
        (self.cloud / 'DefaultPackage').mkdir(exist_ok=True)
        (self.cloud / 'DefaultPackage/sample.bundle').write_bytes(data)
        (self.cloud / 'inventory.json').write_text(json.dumps({'manifests': [
            {'package': 'DefaultPackage', 'version': 'selected'}]}))
        self.receipt = dict(file='DefaultPackage/sample.bundle', url='https://official.example/sample.bundle',
                            sha256=hashlib.sha256(data).hexdigest(), md5=self.bundle['hash'], size=self.bundle['size'])
        self.write_receipt()

    def write_receipt(self):
        (self.cloud / 'receipts.jsonl').write_text(json.dumps(self.receipt) + '\n')

    def read(self):
        with OfficialBundleReader(self.source) as reader:
            result = reader.read('DefaultPackage', 'sample.bundle')
        self.assertIsNone(reader.archive.fp)
        return result

    def test_apk_priority_and_provenance(self):
        self.write_cloud(b'invalid cloud must not be read')
        result = self.read()
        self.assertEqual(result.data, self.data)
        self.assertEqual(result.provenance['origin'], 'apk_zip')
        self.assertEqual(result.provenance['raw_sha256'], hashlib.sha256(self.data).hexdigest())

    def test_cloud_hit_and_missing_actual_file(self):
        self.write_apk(None)
        with self.assertRaisesRegex(FileNotFoundError, 'Not packaged or cached'):
            self.read()
        self.write_cloud()
        result = self.read()
        self.assertEqual(result.data, self.data)
        self.assertEqual(result.provenance['origin'], 'official_cdn')
        self.assertEqual(result.provenance['manifest'], 'selected')

    def test_corrupt_apk_entry_never_uses_cloud(self):
        self.write_cloud()
        for payload in [b'short', b'X' * len(self.data)]:
            with self.subTest(payload=payload):
                self.write_apk(payload)
                with self.assertRaisesRegex(ValueError, 'size/MD5'):
                    self.read()

    def test_bad_apk_identity(self):
        self.source.apk_sha256 = 'wrong'
        with self.assertRaisesRegex(ValueError, 'APK checksum'):
            self.read()

    def test_cloud_credentials_integrity_and_version(self):
        self.write_apk(None)
        self.write_cloud()
        for field in ['url', 'sha256', 'md5', 'size']:
            with self.subTest(field=field):
                original = self.receipt[field]
                self.receipt[field] = '' if field != 'size' else -1
                self.write_receipt()
                with self.assertRaises(ValueError): self.read()
                self.receipt[field] = original
        self.write_receipt()
        (self.cloud / 'inventory.json').write_text(json.dumps({'manifests': [
            {'package': 'DefaultPackage', 'version': 'other'}]}))
        with self.assertRaisesRegex(ValueError, 'version mismatch'):
            self.read()

    def test_verified_bytes_do_not_hide_parser_failure(self):
        result = self.read()
        def parser(data):
            self.assertEqual(data, self.data)
            raise ValueError('decoder failed after verified read')
        with self.assertRaisesRegex(ValueError, 'decoder failed'):
            parser(result.data)

    def test_missing_receipt_and_inventory_are_not_verified(self):
        self.write_apk(None)
        self.write_cloud()
        (self.cloud / 'receipts.jsonl').unlink()
        with self.assertRaises(ValueError): self.read()
        self.write_receipt()
        (self.cloud / 'inventory.json').unlink()
        with self.assertRaisesRegex(ValueError, 'version mismatch'): self.read()

    def test_caller_cannot_change_pinned_record_or_provenance(self):
        with OfficialBundleReader(self.source) as reader:
            reader.manifest('DefaultPackage')['bundles'][0]['hash'] = 'caller mutation'
            result = reader.read('DefaultPackage', 'sample.bundle')
            self.assertEqual(result.data, self.data)
            with self.assertRaises(TypeError): result.provenance['origin'] = 'changed'


if __name__ == '__main__':
    unittest.main()
