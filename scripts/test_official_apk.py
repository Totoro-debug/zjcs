"""CAB aliases must resolve through the pinned manifest without hash changes."""
import unittest
from unittest.mock import MagicMock, patch
import fetch_official_assets
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


class ApkSourceIdentityTests(unittest.TestCase):
    def test_replaced_official_download_is_rejected_before_any_ranges_are_reused(self):
        response = MagicMock()
        response.status = 206
        response.headers = {'Content-Range': f'bytes 0-0/{fetch_official_assets.SIZE + 1}'}
        response.__enter__.return_value = response
        with patch.object(fetch_official_assets, 'CACHE'), \
                patch.object(fetch_official_assets, 'APK') as apk, \
                patch.object(fetch_official_assets.urllib.request, 'urlopen', return_value=response), \
                patch.object(fetch_official_assets.concurrent.futures, 'ThreadPoolExecutor') as pool:
            apk.exists.return_value = False
            with self.assertRaisesRegex(ValueError, 'no longer serves the selected size'):
                fetch_official_assets.download()
            pool.assert_not_called()
            apk.open.assert_not_called()
        self.assertEqual(fetch_official_assets.SIZE, fetch_official_assets._APK_SOURCE['size'])
        self.assertEqual(fetch_official_assets.SHA256, fetch_official_assets._APK_SOURCE['sha256'])


if __name__=='__main__':unittest.main()
