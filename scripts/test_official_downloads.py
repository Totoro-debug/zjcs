"""Verify live CDN selection and exact ranged manifest reconstruction."""
import json
import hashlib
from pathlib import Path
import tempfile
import unittest
from unittest.mock import MagicMock, patch

import fetch_official_cloud_assets as cloud
from fetch_official_runtime_assets import read_url
from fetch_latest_official_sources import acquire_bundle
from validation_workspace import validation_directory


def response(data, headers=None, status=200):
    result = MagicMock()
    result.headers = headers or {}
    result.status = status
    result.read.side_effect = lambda limit=-1: data if limit < 0 else data[:limit]
    result.__enter__.return_value = result
    return result


class LiveConfigurationTests(unittest.TestCase):
    def test_apk_build_selects_only_its_current_resource_directory(self):
        config = {'42': {'ResVersion': '123456',
                         'ReserveJson': json.dumps({'PredownloadConfig': {'ResVersion': '999999'}}),
                         'CdnUrl': 'https://zhangjcsdl.leiting.com/patch',
                         'CdnFallbackUrl': 'https://zhangjcsdl.roguelike.com/patch'},
                  'default': {'ResVersion': '777777'}}
        with patch.object(cloud, 'OfficialSource') as source, \
                patch.object(cloud, 'DESTINATION'), \
                patch.object(cloud.urllib.request, 'urlopen', return_value=response(json.dumps(config).encode())):
            source.return_value.profile = {'apk': {'build_version': 42}, 'resource_version': '123456'}
            roots, provenance = cloud.load_config()
        self.assertEqual(roots, ['https://zhangjcsdl.leiting.com/patch/123456/Android',
                                 'https://zhangjcsdl.roguelike.com/patch/123456/Android'])
        self.assertEqual(provenance['current_resource_version'], '123456')

    def test_changed_resource_directory_requires_refresh(self):
        with patch.object(cloud, 'OfficialSource') as source, \
                patch.object(cloud.urllib.request, 'urlopen', return_value=response(b'{"default":{"ResVersion":"222"}}')):
            source.return_value.profile = {'apk': {'build_version': 42}, 'resource_version': '111'}
            with self.assertRaisesRegex(ValueError, 'fetch_latest_official_sources.py'):
                cloud.load_config()


class ManifestRangeTests(unittest.TestCase):
    def test_large_manifest_is_reconstructed_from_exact_ranges(self):
        payload = bytes(range(251)) * 13000

        def open_response(request, timeout):
            if isinstance(request, str):
                return response(b'', {'Content-Length': str(len(payload))})
            start, end = map(int, request.get_header('Range').removeprefix('bytes=').split('-'))
            return response(payload[start:end + 1],
                            {'Content-Range': f'bytes {start}-{end}/{len(payload)}'}, 206)

        with patch.object(cloud.urllib.request, 'urlopen', side_effect=open_response):
            self.assertEqual(read_url('https://example.invalid/manifest'), payload)

    def test_mismatched_range_is_rejected(self):
        def open_response(request, timeout):
            if isinstance(request, str):
                return response(b'', {'Content-Length': str(3 * 1024 * 1024)})
            return response(b'x', {'Content-Range': 'bytes 0-0/1'}, 206)

        with patch.object(cloud.urllib.request, 'urlopen', side_effect=open_response):
            with self.assertRaisesRegex(ValueError, 'range mismatch'):
                read_url('https://example.invalid/manifest')


class LargeResourceTests(unittest.TestCase):
    def transfer(self, directory, corrupt=False):
        payload = b'x' * (8 * 1024 * 1024)
        bundle = dict(package='WwiseBnk', file='audio.rawfile', name='audio.rawfile',
                      size=len(payload), hash=hashlib.md5(payload).hexdigest())

        def open_response(request, timeout):
            start, end = map(int, request.get_header('Range').removeprefix('bytes=').split('-'))
            chunk = payload[start:end + 1]
            if corrupt and start == 0:
                chunk = b'z' + chunk[1:]
            return response(chunk, {'Content-Range': f'bytes {start}-{end}/{len(payload)}'}, 206)

        with patch.object(cloud, 'DESTINATION', Path(directory)), \
                patch.object(cloud.urllib.request, 'urlopen', side_effect=open_response):
            return acquire_bundle(bundle, ['https://example.invalid/Android'], {})

    def test_large_resource_is_published_only_after_whole_file_hash_verification(self):
        with tempfile.TemporaryDirectory(prefix='cdn-ranges-', dir=validation_directory()) as directory:
            result = self.transfer(directory)
            target = Path(directory) / 'WwiseBnk/audio.rawfile'
            self.assertEqual(result['status'], 'downloaded')
            self.assertEqual(hashlib.sha256(target.read_bytes()).hexdigest(), result['sha256'])
            self.assertEqual(target.stat().st_size, result['size'])
            self.assertFalse(target.with_suffix('.rawfile.part').exists())

    def test_corrupted_range_never_becomes_a_completed_resource(self):
        with tempfile.TemporaryDirectory(prefix='cdn-ranges-', dir=validation_directory()) as directory:
            result = self.transfer(directory, corrupt=True)
            self.assertEqual(result['status'], 'failed')
            self.assertFalse((Path(directory) / 'WwiseBnk/audio.rawfile').exists())


if __name__ == '__main__':
    unittest.main()
