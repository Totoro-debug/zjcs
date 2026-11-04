"""Verify the selected original map-resource catalogs and their provenance."""
import hashlib
import json
from pathlib import Path
import unittest

from official_apk import ROOT
from prepare_current_partner_native import documents


class MainlineResourcePointsTests(unittest.TestCase):
    def test_original_inputs_and_installed_outputs(self):
        for map_id in (10, 11, 12, 13, 14, 16):
            receipt = json.loads((ROOT / f'assets/reference/official-map/map{map_id}-current-minimap-resources-source-manifest.json').read_bytes())
            self.assertEqual((receipt['manifest'], receipt['mapClassId']), ('884_156692', map_id))
            for name, expected in {**receipt['input_receipts'], **receipt['outputs']}.items():
                with self.subTest(map=map_id, path=name):
                    self.assertEqual(hashlib.sha256((ROOT / name).read_bytes()).hexdigest(), expected)
            for row in receipt['scripts'].values():
                for name, expected in ((row['path'], row['sha256']), (row['path'] + '.meta', row['meta_sha256'])):
                    with self.subTest(map=map_id, script=name):
                        self.assertEqual(hashlib.sha256((ROOT / name).read_bytes()).hexdigest(), expected)

    def test_original_layer_to_prefab_and_configuration_closure(self):
        for map_id, count in ((10, 0), (11, 18), (12, 23), (13, 23), (14, 30), (16, 0)):
            root = ROOT / f'unity/Assets/Official/World/Map/{map_id}/NativeMiniMapResourcesCurrent'
            catalog, = documents((root / f'Resources/Map{map_id}/Native/current-resource-point-assets.asset').read_text('utf8')).values()
            catalog = catalog['MonoBehaviour']
            fields = json.loads((root / 'resource-point-manager-fields.json').read_bytes())
            data = json.loads((root / 'resource-point-data.json').read_bytes())
            layers = fields['mResourceLayerPrefabGuidDict']['mSerializeBuffer']
            self.assertEqual(len(catalog['Boards']), count)
            self.assertEqual([(r['RefreshGroup'], r['OriginalGuid']) for r in catalog['Boards']],
                [(r['Key'], r['Value']['mAssetGuid']) for r in layers])
            self.assertEqual({r['Id'] for r in data['Groups']}, {r['Key'] for r in layers})
            self.assertEqual({r['Path'] for r in catalog['Icons']}, {r['Icon'] for r in data['Groups']})
            self.assertEqual({r['ClassId'] for r in data['Regions']}, {map_id})
            self.assertEqual(len(data['ItemLocations']), 106)
            self.assertEqual(len(data['WorldLocations']), 212)
            for board in catalog['Boards']:
                metadata, = [p for p in (root / 'Boards').glob('*.prefab.meta') if board['Prefab']['guid'] in p.read_text('utf8')]
                prefab = documents(Path(str(metadata)[:-5]).read_text('utf8'))
                self.assertIn(board['Prefab']['fileID'], prefab)
                group, = [doc['MonoBehaviour']['mRefreshGroup'] for doc in prefab.values()
                    if 'mRefreshGroup' in doc.get('MonoBehaviour', {})]
                self.assertEqual(group, board['RefreshGroup'])


if __name__ == '__main__':
    unittest.main()
