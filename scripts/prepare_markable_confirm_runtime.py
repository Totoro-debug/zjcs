"""Record shared original hundred-reminder branches and local popup integration."""
import argparse
import hashlib
import json
from pathlib import Path

from prepare_markable_confirm_native import check as check_native

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT / 'assets/reference/markable-confirm-native-source-manifest.json'
RECEIPT = ROOT / 'assets/reference/markable-confirm-runtime-source-manifest.json'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    check_native()
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    paths = [path for area in ('Official/GachaUI', 'Offline/GachaUI')
             for path in (runtime / area).iterdir() if path.is_file()]
    paths += [runtime / path for path in ('Offline/HomeCurrencyUI/OfflineHomeCurrencyWindows.cs',
        'Presentation/Home/HomeHudView.cs', 'Presentation/Home/HomeHudView.PetHundredReminder.cs',
        'Presentation/Home/HomeHudView.PetHundredReminder.cs.meta', 'Offline/Progression/OfflinePetGachaService.cs')]
    validation = [ROOT / path for path in ('scripts/test_home_fruit_tree_weather.ps1',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.PetGachaControls.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.PetHundredConfirm.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.PetHundredConfirm.cs.meta',
        'unity/Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef',
        'unity/Packages/manifest.json', 'unity/Assets/Official/Zjcs.Official.ConvertedSourceDescriptors.asmdef',
        'unity/Assets/Official/Zjcs.Official.ConvertedSourceDescriptors.asmdef.meta')]
    return dict(native_source=digest(NATIVE), generator_sha256=digest(Path(__file__)),
        runtime_bindings={path.relative_to(ROOT).as_posix(): digest(path) for path in paths},
        validation_inputs={path.relative_to(ROOT).as_posix(): digest(path) for path in validation})


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = build()
    if args.check:
        if json.loads(RECEIPT.read_bytes()) != result:
            raise SystemExit('Markable confirmation runtime provenance differs')
        print('Shared markable confirmation runtime provenance verified')
    else:
        RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
