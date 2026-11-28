"""Asset tools operate without retaining historical conversion payloads."""
import hashlib
import json
from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import zipfile

import conversion_history
import fetch_official_assets
from finalize_current_asset_snapshot import finalize
from validation_workspace import temporary_validation_directory


class RetiredSourceCacheTests(unittest.TestCase):
    def test_previous_identity_is_recorded_without_writing_any_copy(self):
        with temporary_validation_directory('source-cache-') as directory:
            root = Path(directory)
            source = root / 'original.asset'
            source.write_bytes(b'original bytes')
            with patch.object(conversion_history, 'ROOT', root), \
                 patch.object(Path, 'mkdir', side_effect=AssertionError('Unexpected directory creation')), \
                 patch.object(Path, 'write_bytes', side_effect=AssertionError('Unexpected payload copy')):
                result = conversion_history.preserve(source)
            self.assertEqual(result, {
                'path': 'original.asset',
                'sha256': hashlib.sha256(b'original bytes').hexdigest(),
            })
            self.assertEqual(list(root.iterdir()), [source])

    def test_finalization_verifies_archive_without_creating_a_source_cache(self):
        with temporary_validation_directory('source-cache-') as directory:
            root = Path(directory)
            (root / 'asset-pack').mkdir()
            (root / 'scripts').mkdir()
            (root / 'scripts/finalize_current_asset_snapshot.py').write_bytes(b'# fixture')
            payload_name = 'assets/official/sample.bin'
            payload = root / payload_name
            payload.parent.mkdir(parents=True)
            payload.write_bytes(b'official payload')
            layout = dict(format=1, source_manifests=['fixture'],
                          payload_roots=['assets/official'], excluded_paths=[],
                          excluded_suffixes=[], code_files=[])
            (root / 'asset-pack/layout.json').write_text(json.dumps(layout), encoding='utf8')
            packs = root / '.tools/asset-packs'
            packs.mkdir(parents=True)
            candidate = packs / 'candidate.zip'
            with zipfile.ZipFile(candidate, 'w') as archive:
                archive.writestr(payload_name, b'official payload')

            result = finalize(root, candidate)

            self.assertEqual(result['archive_payloads_verified'], 1)
            lock = json.loads((root / 'asset-pack/lock.json').read_bytes())
            self.assertEqual(lock['code_sha256'], {
                'scripts/finalize_current_asset_snapshot.py': hashlib.sha256(b'# fixture').hexdigest(),
            })
            self.assertNotIn('preserved_code_sources', lock)
            self.assertEqual(list((root / '.tools').iterdir()), [packs])
            with zipfile.ZipFile(result['archive']) as archive:
                self.assertEqual(archive.read(payload_name), b'official payload')

    def test_inactive_raw_extraction_is_rejected_before_creating_directories(self):
        def source(manifest=None):
            return SimpleNamespace(version=manifest or 'active')

        with patch('official_source.OfficialSource', side_effect=source), \
             patch.object(Path, 'mkdir', side_effect=AssertionError('Unexpected directory creation')):
            with self.assertRaisesRegex(ValueError, 'active official source'):
                fetch_official_assets.extract('.*', manifest='inactive')


if __name__ == '__main__':
    unittest.main()
