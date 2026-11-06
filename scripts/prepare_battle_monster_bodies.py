"""Record verified monster footprints for initial assembly and legacy checkpoint repair."""
from hashlib import sha256
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]


def identity(name):
    return dict(path=name, sha256=sha256((ROOT / name).read_bytes()).hexdigest())


def build():
    rules = 'unity/Assets/Official/Combat/battle-rules.json'
    receipt = 'unity/Assets/Official/Combat/battle-rules-source-manifest.json'
    source = json.loads((ROOT / rules).read_bytes())
    provenance = json.loads((ROOT / receipt).read_bytes())
    assert identity(rules)['sha256'] == provenance['output_sha256']
    bodies = {}
    for monster in source['tables']['monster']:
        template = monster['TempEntityId']
        body = source['entities'].get(str(template), {}).get('GridTransformComponent', {}).get('BodyRangeInfos', [])
        if body:
            bodies[monster['Id']] = dict(template=template, body=body)
    assert len(bodies[3803]['body']) == 25
    proof = dict(manifest=provenance['manifest'],
        generator=identity('scripts/prepare_battle_monster_bodies.py'),
        inputs=[identity(rules), identity(receipt)],
        original_code=[identity(name) for name in (
            '.tools/source-packages/official-code/common-source/Common/GridTransformComponentInfo.cs',
            '.tools/source-packages/official-code/common-source/Common/GridTransformComponentBodyPosInfo.cs',
            '.tools/source-packages/current/884_156692/official-code/client-source/GameCameraController.cs')],
        resolved_monsters=len(bodies),
        resolved_body_config_sha256=sha256(json.dumps(bodies, sort_keys=True, separators=(',', ':')).encode()).hexdigest(),
        example_3803=bodies[3803],
        bindings=[identity(name) for name in (
            'unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleUnitAssembler.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillAssembler.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialCombatAssetCatalog.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/WeeklyChallengeBattleRunner.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleCameraView.cs',
            'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.Presentation.cs',
            'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.MonsterBodies.cs',
            'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.cs',
            'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.LegacyMonsterBodies.cs',
            'unity/Assets/Game/Editor/Combat/OfficialBattleLegacyBodyRestoreValidation.cs')],
        operation='Resolve the existing verified monster TempEntityId and GridTransformComponent.BodyRangeInfos '
            'at initial formal-monster assembly before skills/passives. Arena role players and configured summons '
            'retain their separate identity/footprint assembly. Offline legacy restoration repairs only the '
            'known missing-template/default-single-cell signature, preserving explicit bodies/templates '
            'and all other serialized combat state. Saved weekly counters/timelines select restoration '
            'rather than fresh battle initialization; pre-initialization saves still initialize normally. '
            'Reuse the current source camera radius formula without changing original art transforms or lens settings.')
    path = ROOT / 'assets/reference/battle-rules/monster-bodies-source-manifest.json'
    path.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode())
    print('Recorded original monster footprints:', len(bodies))


if __name__ == '__main__':
    build()
