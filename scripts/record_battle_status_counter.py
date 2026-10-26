"""Record the recovered client status-icon clock and text visibility branches."""
import argparse
import hashlib
import json
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(output_root):
    files = (
        'unity/Assets/Game/Runtime/Official/Combat/FightStatusLifecycle.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleUnitWorldHudView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleStatusCounterAcceptance.cs',
        'unity/Assets/Game/Editor/Combat/OfficialBattleHudAssetTools.cs',
        'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs',
        'scripts/test_battle_status_lifecycle.ps1',
    )
    code = (
        'client-source/FightStatusController.cs',
        'client-source/FightStatusItemBase.cs',
        'client-source/FightStatusUIItem.cs',
        'client-source/FightStatusItem.cs',
        'common-source/Common/FightStatusComponent.cs',
        'common-source/Common/FightStatusUtils.cs',
        'common-source/Common/FightStatusUtilsHandler.cs',
    )
    inputs = ('assets/reference/battle-hud/status-icons-source-manifest.json',
              'unity/Assets/Game/Resources/Combat/battle-status-icons.json')
    record = dict(manifest='854_156383', generator_sha256=sha(Path(__file__)),
                  operation='Port FightStatusController.CalcStatusRemainRound and FightStatusItemBase.GetCountText; resolve RoundComp through the complete roster without changing combat clocks or status duration.',
                  official_code={p: sha(ROOT / '.tools/source-packages/official-code' / p) for p in code},
                  inputs={p: sha(ROOT / p) for p in inputs},
                  runtime={p: sha(output_root / p) for p in files},
                  rules=dict(clock='RoundComp belongs to the configured creator, applicator or master; do not require its HP to be positive.',
                             remaining='DurationRound < 0 or missing RoundComp: DurationRound; otherwise Max(0, CurDurationRound - (RoundComp.Round - StartRound)).',
                             text='Positive count enables the original count node and displays the count, including one; zero or negative disables it.'),
                  remaining='Round-start invocation/custom counts, forbidden-state ordering and status detail input remain incomplete.')
    path = output_root / 'assets/reference/battle-hud/status-counter-source-manifest.json'
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        temporary.write_bytes((json.dumps(record, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)
    print(path)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-root', type=Path, default=ROOT)
    build(parser.parse_args().output_root.resolve())
