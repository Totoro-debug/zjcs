import hashlib,json,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

class OriginalCameraProvenance(unittest.TestCase):
    def test_original_code_and_runtime_configuration_are_hash_verified(self):
        receipt=json.loads((ROOT/'assets/reference/official-map/map13-camera-controller-source-manifest.json').read_bytes())
        self.assertEqual(receipt['manifest'],'871_156598')
        self.assertEqual(len(receipt['officialCode']),45)
        self.assertEqual(len(receipt['outputs']),94)
        for record in receipt['officialCode']+receipt['inputs']+receipt['dependencyContracts']+receipt['tools']+receipt['outputs']:
            with self.subTest(path=record['path']):
                self.assertEqual(hashlib.sha256((ROOT/record['path']).read_bytes()).hexdigest(),record['sha256'])
        source=json.loads((ROOT/'unity/Assets/Official/World/Map/13/Source/world-camera.json').read_bytes())
        original=next(o['fields'] for o in source['objects'] if o['identity']==source['settings'])
        runtime=json.loads((ROOT/'unity/Assets/Game/Resources/Map13/Native/camera-settings.json').read_bytes())
        self.assertEqual(runtime,{k:v for k,v in original.items() if not k.startswith('m_')})
        # The asset deliberately overrides class defaults for far zoom, recovery,
        # complex-mode padding and dance mode. Never reconstruct these from defaults.
        self.assertEqual(runtime['WorldCameraControlSetting']['BaseSetting']['ViewLeaveLimitFactor'],1)
        self.assertEqual(runtime['WorldCameraControlSetting']['BaseSetting']['ViewOverLeaveLimitResetSpeed'],2)
        self.assertEqual(runtime['WorldCameraControlSetting']['ComplexSetting']['ViewMoveRangePadding'],{'x':0,'y':0})
        self.assertEqual(runtime['DanceSettings']['DanceSoftViewSizeMax'],10)
        bindings=json.loads((ROOT/'unity/Assets/Game/Resources/Map13/Native/camera-controller-bindings.json').read_bytes())
        self.assertEqual(len(bindings['records']),9)
        self.assertEqual(len({(r['path'],r['type']) for r in bindings['records']}),9)

if __name__=='__main__':unittest.main()
