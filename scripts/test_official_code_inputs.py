import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import official_source
from validation_workspace import validation_root


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


class OfficialCodeInputsTests(unittest.TestCase):
    def setUp(self):
        self.work = tempfile.TemporaryDirectory(prefix='official-code-', dir=validation_root())
        self.addCleanup(self.work.cleanup)
        self.root = Path(self.work.name)
        self.addCleanup(patch.stopall)
        patch.object(official_source, 'ROOT', self.root).start()
        self.source = official_source.OfficialSource.__new__(official_source.OfficialSource)
        self.source.version = 'selected'
        self.source.apk_sha256 = 'a' * 64
        self.source.prepared_path = self.root / '.tools/source-packages/current/selected'
        self.code = self.source.prepared_path / 'official-code/common-source/Rule.cs'
        self.code.parent.mkdir(parents=True)
        self.code.write_bytes(b'class Rule {}')
        self.assembly = self.source.prepared_path / 'official-code/managed/decoded/Console.dll'
        self.assembly.parent.mkdir(parents=True)
        self.assembly.write_bytes(b'original decoded IL input')
        self.code_receipt = self.source.prepared_path / 'code-manifest.json'
        self.archive_receipt = self.source.prepared_path / 'client-decompilation-manifest.json'
        self.selection = dict(manifest='selected', apk_sha256=self.source.apk_sha256,
                              assemblies={'Console.dll': dict(decoded_sha256=sha(self.assembly.read_bytes()))})
        self.code_receipt.write_text(json.dumps(self.selection), 'utf8')
        self.archive = dict(self.selection,
            input_receipt=dict(path=self.code_receipt.relative_to(self.root).as_posix(),
                               sha256=sha(self.code_receipt.read_bytes())),
            projects=[dict(assembly='Console.dll', files=[dict(
                path=self.code.relative_to(self.root).as_posix(), bytes=self.code.stat().st_size,
                sha256=sha(self.code.read_bytes()))])])
        self.save_archive()

    def save_archive(self):
        self.archive_receipt.write_text(json.dumps(self.archive), 'utf8')

    def test_verified_inputs_include_consumed_code_and_assembly_chain(self):
        result = self.source.verify_code_files([self.code])
        for path in (self.code, self.assembly, self.code_receipt, self.archive_receipt):
            self.assertEqual(result[path.relative_to(self.root).as_posix()], sha(path.read_bytes()))

    def test_changed_source_and_assembly_are_rejected(self):
        original = self.code.read_bytes()
        self.code.write_bytes(b'class Fake {}')
        with self.assertRaisesRegex(ValueError, 'Consumed code differs'):
            self.source.verify_code_files([self.code])
        self.code.write_bytes(original)
        self.assembly.write_bytes(b'altered IL')
        with self.assertRaisesRegex(ValueError, 'assembly changed'):
            self.source.verify_code_files([self.code])

    def test_other_version_and_apk_are_rejected(self):
        for key, value in [('manifest', 'older'), ('apk_sha256', 'b' * 64)]:
            original = self.archive[key]
            self.archive[key] = value
            self.save_archive()
            with self.assertRaisesRegex(ValueError, 'different source'):
                self.source.verify_code_files([self.code])
            self.archive[key] = original

    def test_unrecorded_or_outside_code_is_rejected(self):
        unrecorded = self.code.with_name('Unrecorded.cs')
        unrecorded.write_bytes(self.code.read_bytes())
        with self.assertRaisesRegex(ValueError, 'Consumed code differs'):
            self.source.verify_code_files([unrecorded])
        outside = self.root / 'Elsewhere.cs'
        outside.write_bytes(self.code.read_bytes())
        with self.assertRaisesRegex(ValueError, 'outside the selected source'):
            self.source.verify_code_files([outside])

    def test_mixed_or_duplicate_receipts_are_rejected(self):
        self.archive['input_receipt']['sha256'] = '0' * 64
        self.save_archive()
        with self.assertRaisesRegex(ValueError, 'receipts differ'):
            self.source.verify_code_files([self.code])
        self.archive['input_receipt']['sha256'] = sha(self.code_receipt.read_bytes())
        self.archive['projects'][0]['files'] *= 2
        self.save_archive()
        with self.assertRaisesRegex(ValueError, 'Duplicate official code identity'):
            self.source.verify_code_files([self.code])


if __name__ == '__main__':
    unittest.main()
