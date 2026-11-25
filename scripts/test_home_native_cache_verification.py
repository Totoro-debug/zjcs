import hashlib
import json
from pathlib import Path
import unittest

from home_native_cache_verification import verify_native_cache
from validation_workspace import temporary_validation_directory


def digest(data):
    return hashlib.sha256(data).hexdigest()


class NativeCacheVerificationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = temporary_validation_directory('home-cache-test-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.cache = self.root / 'cache'
        self.export = self.cache / 'native-export'
        self.prefab = 'Prefabs/Home/Test.prefab'
        self.output = 'ExportedProject/Assets/' + self.prefab
        self.write(self.export / self.output, b'original prefab')
        self.write(self.export / 'dependency.asset', b'original dependency')
        self.write(self.cache / 'inputs/original.bundle', b'official input')
        self.write(self.cache / 'inputs/Managed/Test.dll', b'assembly')
        self.write(self.cache / 'types/Test.json', b'descriptor')
        source = {'inputs': [{'output': 'inputs/original.bundle', 'decoded_sha256': digest(b'official input')}]}
        code = {'assemblies': {'Test.dll': {'decoded_sha256': digest(b'assembly')}}}
        source_bytes = json.dumps(source).encode()
        code_bytes = json.dumps(code).encode()
        self.write(self.cache / 'source-manifest.json', source_bytes)
        self.write(self.root / 'code-manifest.json', code_bytes)
        proof = {
            'completed': True, 'manifest': 'source-id',
            'input_manifest_sha256': digest(source_bytes), 'code_manifest_sha256': digest(code_bytes),
            'outputs': [{'path': self.output, 'sha256': digest(b'original prefab')},
                        {'path': 'dependency.asset', 'sha256': digest(b'original dependency')}],
            'type_dependencies': [{'output': 'types/Test.json', 'sha256': digest(b'descriptor')}],
        }
        self.write(self.cache / 'native-export-source-manifest.json', json.dumps(proof).encode())

    @staticmethod
    def write(path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def verify(self):
        return verify_native_cache(self.cache, [self.prefab], 'source-id')

    def test_accepts_intact_cache_and_rejects_changed_dependency(self):
        self.assertEqual(self.verify(), self.cache)
        self.write(self.export / 'dependency.asset', b'modified dependency')
        with self.assertRaisesRegex(ValueError, 'Native export file differs'):
            self.verify()

    def test_rejects_extra_export_and_modified_official_input(self):
        self.write(self.export / 'unlisted.asset', b'unlisted')
        with self.assertRaisesRegex(ValueError, 'files differ'):
            self.verify()
        (self.export / 'unlisted.asset').unlink()
        self.write(self.cache / 'inputs/original.bundle', b'modified input')
        with self.assertRaisesRegex(ValueError, 'conversion input differs'):
            self.verify()

    def test_rejects_mismatched_source_and_missing_target_prefab(self):
        with self.assertRaisesRegex(ValueError, 'official source inputs'):
            verify_native_cache(self.cache, [self.prefab], 'other-source')
        with self.assertRaisesRegex(ValueError, 'prefab is missing'):
            verify_native_cache(self.cache, ['Prefabs/Home/Missing.prefab'], 'source-id')


if __name__ == '__main__':
    unittest.main()
