"""Verify complete original roots, shared windows and their runtime bindings."""
import hashlib
import json
import unittest

from official_apk import ROOT


class MainlineMiniMapNativeBindingsTests(unittest.TestCase):
    def test_selected_native_sources_outputs_and_integration(self):
        names = [f'map{map_id}-current-minimap-root' for map_id in (10, 11, 12, 13, 14, 16)]
        names += [f'mainline-current-minimap-{kind}' for kind in ('hud', 'renderer', 'panel')]
        names += ['mainline-current-minimap-markers']
        for name in names:
            receipt = json.loads((ROOT / f'assets/reference/official-map/{name}-source-manifest.json').read_bytes())
            self.assertEqual(receipt['manifest'], '884_156692')
            hashes = {**receipt['input_receipts'], **receipt['outputs']}
            for row in receipt['scripts'].values():
                hashes[row['path']] = row['sha256']
                hashes[row['path'] + '.meta'] = row['meta_sha256']
            for row in receipt.get('integration', []):
                hashes[row['path']] = row['sha256']
            for path, expected in hashes.items():
                with self.subTest(receipt=name, path=path):
                    self.assertEqual(hashlib.sha256((ROOT / path).read_bytes()).hexdigest(), expected)

    def test_selected_marker_query_sources_and_integration(self):
        receipt = json.loads((ROOT / 'assets/reference/official-map/mainline-minimap-marker-catalog-source-manifest.json').read_bytes())
        self.assertEqual(receipt['manifest'], '884_156692')
        for row in receipt['inputs'] + receipt['outputs'] + receipt['integration'] + [receipt['native_markers']]:
            with self.subTest(path=row['path']):
                self.assertEqual(hashlib.sha256((ROOT / row['path']).read_bytes()).hexdigest(), row['sha256'])

    def test_selected_world_data_sources_and_bindings(self):
        receipt = json.loads((ROOT / 'assets/reference/official-map/mainline-minimap-world-data-source-manifest.json').read_bytes())
        self.assertEqual(receipt['manifest'], '884_156692')
        self.assertEqual(sum(row['cells'] for row in receipt['bindings']), 258400)
        for row in receipt['sources'] + receipt['outputs'] + receipt['integration'] + [row['definition'] for row in receipt['bindings']]:
            with self.subTest(path=row['path']):
                self.assertEqual(hashlib.sha256((ROOT / row['path']).read_bytes()).hexdigest(), row['sha256'])


if __name__ == '__main__':
    unittest.main()
