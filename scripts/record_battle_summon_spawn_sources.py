"""Record native summon appearance scheduling without re-exporting original assets."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    raw = (subprocess.check_output(['git', 'show', ':' + path], cwd=ROOT)
           if '--index' in sys.argv and not path.startswith('.tools/') else (ROOT / path).read_bytes())
    return dict(path=path, bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())


def main():
    runtime = 'unity/Assets/Game/Runtime/'
    changed = [runtime + path for path in (
        'Official/Combat/FightBattleExecutor.cs', 'Offline/Combat/FightBattleHitSelectionSnapshot.cs',
        'Presentation/Combat/OfficialBattleSceneView.cs', 'Presentation/Combat/OfficialBattleSceneView.Summons.cs',
        'Presentation/Combat/OfficialBattleSceneView.UnitEnds.cs', 'Presentation/Combat/OfficialBattleUnitSpawnPlayback.cs',
        'Presentation/Combat/OfficialBattleSkillGraphPlayback.cs', 'Presentation/Combat/OfficialBattleLiveMonsterModelView.cs')]
    original = '.tools/source-packages/current/884_156692/official-code/'
    proof = dict(code_manifest='884_156692', configuration_manifest='854_156383',
        original=[identity(original + path) for path in (
            'common-source/Common/FightHitSummonComponentInfo.cs',
            'common-source/Common/HitScopCfg.cs', 'client-source/MyFightHitBase.cs',
            'client-source/ECFightRoleCharacterView.cs')],
        configuration=[identity('unity/Assets/Official/Combat/' + path) for path in (
            'skill-rules.json', 'skill-rules-source-manifest.json', 'battle-rules.json', 'battle-rules-source-manifest.json')],
        migration=[identity(path) for path in changed],
        assets=[identity(path) for path in (
            'assets/reference/battle-animation/live-monsters-source-manifest.json',
            'assets/reference/battle-animation/summon-role-views-source-manifest.json',
            'assets/reference/battle-animation/native-monster-appearance-source-manifest.json',
            'assets/reference/battle-animation/monster-animator-bindings-source-manifest.json',
            'assets/reference/battle-animation/monster-animation-controllers-source-manifest.json',
            'assets/reference/battle-animation/monster-attachments-source-manifest.json',
            'assets/reference/battle-animation/monster-additional-shadows-source-manifest.json')],
        validation=[identity(path) for path in (
            'scripts/test_battle_unit_spawns.ps1', 'scripts/fixtures/battle_unit_spawn_harness.cs',
            'scripts/test_battle_skill_graph_dispatch.ps1', 'scripts/fixtures/battle_skill_graph_dispatch_harness.cs',
            'scripts/prepare_battle_damage_playback_validation.py',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleSummonSpawnAcceptance.cs')],
        generator=identity('scripts/record_battle_summon_spawn_sources.py'),
        binding='Successful SpawnSummons -> owning HitSelectionSnapshot.SpawnedUnits -> shared scene -> native graph clock -> original model/generation graph',
        operation='Add presentation-only identity, initial cell and cast-local flight plus original sorted Base scope delay for each successful role summon. '
            'Retain eager settlement, RNG, placement and status order. Delay model/HUD creation on the existing native graph clock. '
            'Restore existing roles without replaying generation. Position the native monster before original birth graph initialization. '
            'No source prefab, texture, mesh, shader or animation asset is changed.')
    for name, key in (
        ('assets/reference/battle-animation/summon-role-views-source-manifest.json', 'runtime_bindings'),
        ('assets/reference/battle-rules/custom-targets-source-manifest.json', 'migration')):
        path = ROOT / name
        receipt = json.loads(path.read_bytes())
        for row in receipt[key]:
            if row['path'] not in changed:
                continue
            current = identity(row['path'])
            for field in tuple(row):
                if field in current:
                    row[field] = current[field]
        path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    # Hash the refreshed lifecycle credentials, retaining the immutable asset payloads.
    proof['assets'] = [identity(row['path']) for row in proof['assets']]
    (ROOT / 'assets/reference/battle-animation/summon-spawn-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
