"""Safety and provenance checks for sharing original native texture payloads."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import deduplicate_unity_textures as dedup
from validation_workspace import validation_directory


class SharedTextureTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='texture-unit-', dir=validation_directory())
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.addCleanup(patch.stopall)
        patch.object(dedup, 'ROOT', self.root).start()
        patch.object(dedup, 'RECEIPT', self.root / 'assets/reference/shared-textures-source-manifest.json').start()

    def texture(self, name, guid, *, readable=0, data=b'a', importer_extra=b''):
        path = self.root / ('unity/Assets/Official/' + name + '.asset')
        path.parent.mkdir(parents=True, exist_ok=True)
        raw = b'%YAML 1.1\n--- !u!28 &2800000\nTexture2D:\n  m_IsReadable: ' + str(readable).encode() + b'\n' + data
        path.write_bytes(raw)
        meta = b'fileFormatVersion: 2\nguid: ' + guid.encode() + b'\nNativeFormatImporter:\n  externalObjects: {}\n' + importer_extra
        Path(str(path) + '.meta').write_bytes(meta)
        return dict(path=path.relative_to(self.root).as_posix(), sha256=dedup.digest(raw),
                    meta_sha256=dedup.digest(meta), guid=guid, bytes=len(raw))

    def test_preserves_resources_addresses_and_writable_texture_state(self):
        a = self.texture('A/Resources/Atlas', 'a' * 32)
        b = self.texture('B/Resources/Atlas', 'b' * 32)
        c = self.texture('C/Atlas', 'c' * 32)
        x = self.texture('X/Atlas', 'd' * 32, readable=1)
        y = self.texture('Y/Atlas', 'e' * 32, readable=1)
        with patch.object(dedup.subprocess, 'check_output', side_effect=lambda args, **kw:
                          '\0'.join(r['path'] for r in (a, b, c, x, y)).encode() if 'ls-files' in args and '--others' not in args else b''):
            receipt = dedup.plan([[a, b, c]])
            self.assertEqual([r['source'] for r in receipt['textures']], [c])
            self.assertEqual(dedup.plan([[x, y]])['textures'], [])

    def test_refuses_changed_payload_and_different_importer(self):
        a = self.texture('A/Atlas', 'a' * 32)
        b = self.texture('B/Atlas', 'b' * 32, importer_extra=b'  userData: different\n')
        receipt = dict(textures=[dict(source=a, canonical=b)])
        with self.assertRaisesRegex(ValueError, 'importer differs'):
            dedup.validate_rows(receipt)
        (self.root / a['path']).write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError, 'changed texture'):
            dedup.validate_rows(receipt)

    def test_keeps_texture_referenced_by_another_sessions_new_asset(self):
        a = self.texture('A/Atlas', 'a' * 32)
        b = self.texture('B/Atlas', 'b' * 32)
        host = self.root / 'unity/Assets/Official/New/prefab.prefab'
        host.parent.mkdir(parents=True)
        host.write_bytes(b'm_Texture: {fileID: 2800000, guid: ' + b'a' * 32 + b', type: 2}')
        def git(args, **kw):
            if '--others' in args:
                return host.relative_to(self.root).as_posix().encode() + b'\0'
            return (a['path'] + '\0' + b['path'] + '\0').encode() if 'ls-files' in args else b''
        with patch.object(dedup.subprocess, 'check_output', side_effect=git):
            result = dedup.plan([[a, b]])
        self.assertEqual(result['textures'][0]['source'], b)
        self.assertEqual(result['textures'][0]['canonical'], a)

    def test_rewrites_serialized_pointer_without_changing_source_identity(self):
        a = self.texture('A/Atlas', 'a' * 32)
        b = self.texture('B/Atlas', 'b' * 32)
        path = self.root / 'unity/Assets/Official/A/material.mat'
        path.write_bytes(b'm_Texture: {fileID: 2800000, guid: ' + b'a' * 32 + b', type: 2}\noriginalGuid: ' + b'a' * 32)
        patches = list(dedup.references(dict(textures=[dict(source=a, canonical=b)])))
        self.assertEqual(len(patches), 1)
        self.assertIn(b'guid: ' + b'b' * 32, patches[0][2])
        self.assertIn(b'originalGuid: ' + b'a' * 32, patches[0][2])

    def test_updates_verified_output_hashes_preserving_unrelated_drift(self):
        value = dict(outputs={'unity/Assets/Official/A.asset.meta': 'old-meta'},
                     inputs={'original.apk': 'original'}, unrelated={'changed.cs': 'unreviewed'})
        changes = {'unity/Assets/Official/A.asset.meta': ('old-meta', 'new-meta'), 'changed.cs': ('previous', 'latest')}
        result = dedup.update_output_records(value, {'unity/Assets/Official/A.asset.meta': 'unity/Assets/Official/B.asset.meta'}, changes, {})
        self.assertEqual(result['outputs'], {'unity/Assets/Official/B.asset.meta': 'new-meta'})
        self.assertEqual(result['inputs'], value['inputs'])
        self.assertEqual(dedup.update_hash_links(result, changes)['unrelated'], value['unrelated'])

    def test_verify_rejects_recreated_duplicate(self):
        a = self.texture('A/Atlas', 'a' * 32)
        b = self.texture('B/Atlas', 'b' * 32)
        with self.assertRaisesRegex(ValueError, 'Regenerated duplicate'):
            dedup.verify(dict(textures=[dict(source=a, canonical=b)]))

    def test_transaction_updates_hash_dependency_chain_and_can_restore(self):
        a = self.texture('A/Atlas', 'a' * 32)
        b = self.texture('B/Atlas', 'b' * 32)
        material = self.root / 'unity/Assets/Official/A/material.mat'
        original_material = b'm_Texture: {fileID: 2800000, guid: ' + b'a' * 32 + b', type: 2}\n'
        material.write_bytes(original_material)
        reference = self.root / 'assets/reference'
        reference.mkdir(parents=True)
        upstream = reference / 'upstream.json'
        upstream.write_text(json.dumps(dict(outputs={a['path']: a['sha256'], a['path'] + '.meta': a['meta_sha256'],
            material.relative_to(self.root).as_posix(): dedup.digest(original_material)}, inputs={'official.apk': 'unchanged'})))
        original_upstream = upstream.read_bytes()
        downstream = reference / 'downstream.json'
        downstream.write_text(json.dumps(dict(input_receipts={'assets/reference/upstream.json': dedup.sha(upstream)})))
        original_downstream = downstream.read_bytes()
        work = self.root / 'output/validation/transaction'
        work.mkdir(parents=True)
        (work / '.zjcs-validation.json').write_text('{}')
        script = self.root / 'scripts/check_unity_project.py'
        script.parent.mkdir()
        script.write_bytes(b'')
        with patch.object(dedup.subprocess, 'check_output', side_effect=lambda args, **kw:
                          b'assets/reference/upstream.json\0assets/reference/downstream.json\0' if 'ls-files' in args else b''):
            dedup.apply(dict(textures=[dict(source=a, canonical=b)]), work)
        self.assertFalse((self.root / a['path']).exists())
        result = json.loads(upstream.read_bytes())
        for path, expected in result['outputs'].items():
            self.assertEqual(dedup.sha(self.root / path), expected)
        self.assertEqual(result['inputs'], {'official.apk': 'unchanged'})
        self.assertEqual(json.loads(downstream.read_bytes())['input_receipts']['assets/reference/upstream.json'], dedup.sha(upstream))
        dedup.verify(json.loads(dedup.RECEIPT.read_bytes()))
        saved_receipt = dedup.RECEIPT.read_bytes()
        dedup.RECEIPT.write_bytes(saved_receipt + b'\n')
        with self.assertRaisesRegex(ValueError, 'concurrent change to shared texture recipe'):
            dedup.rollback(work)
        dedup.RECEIPT.write_bytes(saved_receipt)
        dedup.rollback(work)
        self.assertEqual(upstream.read_bytes(), original_upstream)
        self.assertEqual(downstream.read_bytes(), original_downstream)
        self.assertEqual(material.read_bytes(), original_material)
        self.assertEqual(dedup.sha(self.root / a['path']), a['sha256'])
        self.assertFalse(dedup.RECEIPT.exists())
        previous_receipt = b'{"format": 1, "textures": [], "operation": "previous"}\n'
        dedup.RECEIPT.write_bytes(previous_receipt)
        second = work.with_name('second-transaction')
        second.mkdir()
        (second / '.zjcs-validation.json').write_text('{}')
        with patch.object(dedup.subprocess, 'check_output', side_effect=lambda args, **kw:
                          b'assets/reference/upstream.json\0assets/reference/downstream.json\0' if 'ls-files' in args else b''):
            dedup.apply(dict(textures=[dict(source=a, canonical=b)]), second)
        dedup.rollback(second)
        self.assertEqual(dedup.RECEIPT.read_bytes(), previous_receipt)


if __name__ == '__main__':
    unittest.main()
