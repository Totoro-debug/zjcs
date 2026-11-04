"""Verify complete original roots, shared windows and their runtime bindings."""
import hashlib
import json
import unittest

from official_apk import ROOT


class MainlineMiniMapNativeBindingsTests(unittest.TestCase):
    def test_selected_native_sources_outputs_and_integration(self):
        names = [f'map{map_id}-current-minimap-root' for map_id in (10, 11, 12, 13, 14, 16)]
        names += [f'mainline-current-minimap-{kind}' for kind in ('hud', 'renderer', 'panel')]
        for name in names:
            receipt = json.loads((ROOT / f'assets/reference/official-map/{name}-source-manifest.json').read_bytes())
            self.assertEqual(receipt['manifest'], '884_156692')
            hashes = {**receipt['input_receipts'], **receipt['outputs']}
            for row in receipt['scripts'].values():
                hashes[row['path']] = row['sha256']
                hashes[row['path'] + '.meta'] = row['meta_sha256']
            for row in receipt['integration']:
                hashes[row['path']] = row['sha256']
            for path, expected in hashes.items():
                with self.subTest(receipt=name, path=path):
                    self.assertEqual(hashlib.sha256((ROOT / path).read_bytes()).hexdigest(), expected)


if __name__ == '__main__':
    unittest.main()
