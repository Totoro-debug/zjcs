import unittest
import json
import struct
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from prepare_current_resource_inputs import union
from validation_workspace import temporary_validation_directory
from prepare_current_aot_metadata import parser_view


class ResourceUnionTests(unittest.TestCase):
    def test_new_overrides_matching_identity_and_retains_both_unmatched_sides(self):
        old = [dict(package='A', name='shared', hash='old'), dict(package='A', name='removed', hash='keep')]
        new = [dict(package='A', name='shared', hash='new'), dict(package='A', name='added', hash='add')]
        actual = union(old, new, lambda b: (b['package'], b['name']))
        self.assertEqual({r['name']: r['hash'] for r in actual}, dict(shared='new', removed='keep', added='add'))
        self.assertEqual(old[0]['hash'], 'old')

    def test_equal_names_in_different_packages_are_independent(self):
        old = [dict(package='A', name='shared')]
        new = [dict(package='B', name='shared')]
        self.assertEqual(len(union(old, new, lambda b: (b['package'], b['name']))), 2)

    def test_missing_new_catalog_does_not_remove_old_assets(self):
        old = [dict(path='Assets/old.prefab', guid='original')]
        self.assertEqual(union(old, [], lambda a: a['path']), old)

    def test_ambiguous_identity_does_not_silently_discard_an_asset(self):
        with self.assertRaisesRegex(ValueError, 'Ambiguous'):
            union([dict(path='same', guid='a'), dict(path='same', guid='b')], [], lambda a: a['path'])

    def test_code_cache_merge_preserves_unmatched_files_and_prior_bytes(self):
        import prepare_official_sources as converter
        with temporary_validation_directory('resource-merge-') as temporary:
            root = Path(temporary)
            source, cache = root/'prepared', root/'cache'
            for base in (source, cache):
                (base/'official-code').mkdir(parents=True)
                (base/'official-data').mkdir()
            (source/'code-manifest.json').write_text(json.dumps(dict(manifest='test', assemblies={})))
            (source/'official-code/shared.cs').write_bytes(b'new')
            (source/'official-code/added.cs').write_bytes(b'added')
            (cache/'official-code/shared.cs').write_bytes(b'old')
            (cache/'official-code/old-only.cs').write_bytes(b'retain')
            preserved = []
            def preserve(path):
                preserved.append(path.read_bytes())
                return dict(path=path.relative_to(root).as_posix())
            with patch.object(converter, 'ROOT', root), patch.object(converter, 'CACHE', cache), \
                 patch('official_source.OfficialSource', return_value=SimpleNamespace(version='test')), \
                 patch('refresh_current_character_meshes.preserve', side_effect=preserve):
                converter.activate_prepared(source)
            self.assertEqual((cache/'official-code/shared.cs').read_bytes(), b'new')
            self.assertEqual((cache/'official-code/added.cs').read_bytes(), b'added')
            self.assertEqual((cache/'official-code/old-only.cs').read_bytes(), b'retain')
            self.assertEqual(preserved, [b'old'])

    def test_flat_vm_parser_view_preserves_native_bytes_and_maps_dynamic_table(self):
        raw = bytearray(1024)
        raw[:4] = b'PACK'
        struct.pack_into('<Q', raw, 32, 64)
        struct.pack_into('<H', raw, 56, 2)
        struct.pack_into('<IIQQQQQQ', raw, 64, 1, 5, 256, 512, 512, 512, 768, 4096)
        struct.pack_into('<IIQQQQQQ', raw, 120, 2, 6, 384, 900, 900, 32, 32, 8)
        raw[640:672] = bytes(range(32))
        result = parser_view(raw)
        self.assertEqual(result[:4], b'\x7fELF')
        self.assertEqual(result[176:], raw[176:])
        dynamic = struct.unpack_from('<IIQQQQQQ', result, 120)
        self.assertEqual(dynamic[2:5], (640, 640, 640))
        load = struct.unpack_from('<IIQQQQQQ', result, 64)
        self.assertEqual(load[2:4], (512, 512))

    def test_flat_vm_parser_view_rejects_incomplete_recovered_segments(self):
        raw = bytearray(512)
        struct.pack_into('<Q', raw, 32, 64)
        struct.pack_into('<H', raw, 56, 1)
        struct.pack_into('<IIQQQQQQ', raw, 64, 1, 5, 256, 512, 512, 512, 768, 4096)
        with self.assertRaisesRegex(ValueError, 'does not cover'):
            parser_view(raw)


if __name__ == '__main__':
    unittest.main()
