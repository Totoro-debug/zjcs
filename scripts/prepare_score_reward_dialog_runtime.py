"""Record source and assets staged into the independent pet-score Unity flow."""
import argparse
import hashlib
import json
from pathlib import Path
from prepare_score_reward_dialog_native import check as check_detail
from prepare_markable_confirm_native import check as check_confirm

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/score-reward-dialog-runtime-source-manifest.json'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build():
    check_detail()
    check_confirm()
    inputs = [Path(__file__), ROOT / 'scripts/test_pet_score_dialog.ps1', ROOT / 'scripts/test_home_fruit_tree_weather.ps1',
        ROOT / 'assets/reference/pet-score-reward-source-manifest.json',
        ROOT / 'assets/reference/markable-confirm-runtime-source-manifest.json',
        ROOT / 'assets/reference/shared-achievement-panel-source-manifest.json']
    staged = {}
    for name in ('score-reward-dialog-native', 'markable-confirm-native', 'pet-gacha-controls-native'):
        path = ROOT / 'assets/reference' / (name + '-source-manifest.json')
        inputs.append(path)
        receipt = json.loads(path.read_bytes())
        for relative, expected in receipt['outputs'].items():
            if sha(ROOT / relative) != expected:
                raise ValueError('Native score validation payload differs: ' + relative)
            staged[relative] = expected
        for row in receipt['script_bindings'].values():
            path = ROOT / row['path']
            if sha(path) != row['sha256'] or sha(Path(str(path) + '.meta')) != row['meta_sha256']:
                raise ValueError('Native score validation script differs: ' + str(path))
            if row['path'].startswith('unity/Assets/'):
                staged[row['path']] = row['sha256']
                staged[row['path'] + '.meta'] = row['meta_sha256']
    paths = [path for area in ('Official/GachaUI', 'Offline/GachaUI')
             for path in (ROOT / 'unity/Assets/Game/Runtime' / area).iterdir() if path.is_file()]
    paths += [ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI' / (name + suffix)
        for name in ('AchievementPanel', 'AchievementPanelBase', 'AchievementPanelItem', 'AchievementPanelData', 'ProfessionAchievementPanelSource', 'UIBase')
        for suffix in ('.cs', '.cs.meta')]
    paths += [ROOT / 'unity/Assets/Game/Runtime' / path for path in (
        'Presentation/Home/HomeHudView.cs', 'Presentation/Home/HomeHudView.PetGacha.cs',
        'Presentation/Home/HomeHudView.PetHundredReminder.cs', 'Presentation/Home/HomeHudView.PetHundredReminder.cs.meta',
        'Presentation/Home/HomeHudView.PetScoreDialog.cs', 'Presentation/Home/HomeHudView.PetScoreDialog.cs.meta',
        'Presentation/Home/HomeAcceptanceDriver.cs', 'Presentation/Home/HomeAcceptanceDriver.PetGachaControls.cs',
        'Presentation/Home/HomeAcceptanceDriver.PetHundredConfirm.cs', 'Presentation/Home/HomeAcceptanceDriver.PetHundredConfirm.cs.meta',
        'Presentation/Home/HomeAcceptanceDriver.PetScoreDialog.cs', 'Presentation/Home/HomeAcceptanceDriver.PetScoreDialog.cs.meta',
        'Presentation/Home/Zjcs.Presentation.Home.asmdef', 'Offline/HomeCurrencyUI/OfflineHomeCurrencyWindows.cs',
        'Offline/Progression/OfflinePetGachaService.cs')]
    paths += [ROOT / 'unity/Assets/Game/Resources/PetGacha' / path for path in ('score-reward-ui.json', 'score-reward-ui.json.meta')]
    staged.update({path.relative_to(ROOT).as_posix(): sha(path) for path in paths})
    return dict(inputs={path.relative_to(ROOT).as_posix(): sha(path) for path in inputs}, staged_sources=staged,
        scope='Independent source-project main-city run: existing native gacha controls, hundred reminder and production score detail popup, regular task progress, local group claim, original states, mask closure and isolated profile restore.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    result = build()
    if args.check:
        if json.loads(RECEIPT.read_bytes()) != result:
            raise SystemExit('Score reward dialog runtime source differs')
        print('Score reward dialog independent runtime inputs verified')
    else:
        RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
