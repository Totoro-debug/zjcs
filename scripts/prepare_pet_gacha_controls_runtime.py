"""Record the original gacha-control source boundary and independent validation."""
import argparse
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
NATIVE = 'assets/reference/pet-gacha-controls-native-source-manifest.json'
RECEIPT = ROOT / 'assets/reference/pet-gacha-controls-runtime-source-manifest.json'


def digest(relative):
    return hashlib.sha256((ROOT / relative).read_bytes()).hexdigest()


def build():
    native = json.loads((ROOT / NATIVE).read_bytes())
    for group in ('inputs', 'outputs', 'runtime_bindings'):
        for relative, expected in native[group].items():
            if digest(relative) != expected:
                raise ValueError('Original gacha-control source or binding differs: ' + relative)
    prefix = next(relative.rsplit('/client-source/', 1)[0]
                  for relative in native['inputs'] if relative.endswith('/client-source/PlayerOpenSkillChestBtn.cs'))
    sources = [prefix + '/client-source/QuickBuyUtils.cs', prefix + '/common-source/Common/ChestInfo.cs']
    validation = ['scripts/test_home_fruit_tree_weather.ps1',
                  'unity/Packages/manifest.json',
                  'unity/Assets/Official/Zjcs.Official.ConvertedSourceDescriptors.asmdef',
                  'unity/Assets/Official/Zjcs.Official.ConvertedSourceDescriptors.asmdef.meta',
                  'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.cs',
                  'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.PetGachaControls.cs',
                  'unity/Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef']
    adapter = 'unity/Assets/Game/Runtime/Offline/HomeCurrencyUI/OfflineHomeCurrencyImageServices.cs'
    return dict(native_source=digest(NATIVE), original_sources={path: digest(path) for path in sources},
                runtime_bindings=dict(native['runtime_bindings'], **{adapter: digest(adapter), adapter + '.meta': digest(adapter + '.meta')}),
                validation_inputs={path: digest(path) for path in validation},
                generator_sha256=digest('scripts/prepare_pet_gacha_controls_runtime.py'))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = build()
    if args.check:
        if json.loads(RECEIPT.read_bytes()) != result:
            raise SystemExit('Pet gacha control runtime provenance differs')
        print('Pet gacha control runtime provenance verified')
    else:
        RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
