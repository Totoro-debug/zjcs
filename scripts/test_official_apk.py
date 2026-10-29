"""CAB aliases must resolve through the pinned manifest without hash changes."""
import unittest
from official_apk import canonical_bundle_name


class CabAliasTests(unittest.TestCase):
    def test_truncated_materials_alias_resolves_by_original_md5(self):
        expected='defaultpackage_assets_textmesh_pro_assets_fonts_&_materials.bundle'
        digest='bf62732f37a5a423ddbac02681e054a4'
        record={'bundle':'_materials.bundle','hash':digest}
        self.assertEqual(canonical_bundle_name(record,{expected:digest}),expected)
        self.assertEqual(record,{'bundle':'_materials.bundle','hash':digest})

    def test_existing_name_with_wrong_hash_is_rejected(self):
        with self.assertRaises(ValueError):
            canonical_bundle_name({'bundle':'original.bundle','hash':'new-version'},{'original.bundle':'pinned'})

    def test_missing_or_ambiguous_hash_is_rejected(self):
        for index in [{'one.bundle':'different'},{'one.bundle':'same','two.bundle':'same'}]:
            with self.subTest(index=index),self.assertRaises(ValueError):
                canonical_bundle_name({'bundle':'alias.bundle','hash':'same'},index)


if __name__=='__main__':unittest.main()
