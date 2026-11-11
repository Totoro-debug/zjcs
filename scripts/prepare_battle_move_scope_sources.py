"""Verify original movement-burning terrain and its independent round-start chain."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components
ROOT = Path(__file__).resolve().parents[1]

def main():
    entities = json.loads((ROOT / 'unity/Assets/Official/Combat/battle-rules.json').read_bytes())['entities']
    definitions = {key: entities[key] for key in ('25027', '11630', '11631', '11632', '3320')}
    move = definitions['25027']['FightStatusMoveScopeComponent']
    assert move['Rate'] == 1 and move['MaxCount'] == -1 and move['TryAtStart']
    assert move['ScopeGridItemId'] == 3320 and move['GridItemStatus'] == [11632]
    assert [(v['Scope']['X'], v['Scope']['Y']) for v in move['Scopes']] == [(0,0),(1,0),(0,1),(-1,0),(0,-1)]
    assert all(abs(v['Rate']-.3) < 1e-7 for v in move['Scopes'])
    assert definitions['25027']['FightStatusRoundStartComponent']['StatusTriggerSkillCfgs'][0]['SkillId'] == 11630
    configuration = verify_selected_components(definitions)
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusMoveScopeComponent.cs',
        'common-source/Common/FightStatusMoveScopeComponentInfo.cs',
        'client-source/MyFightStatusMoveScope.cs',
        'common-source/Common/FightRoleCharacterComponentInfo.cs')]
    proof = dict(manifest='884_156692', configuration=configuration, original_code=originals,
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of move scopes, original per-cell probability, terrain/status chain, independent round-start skill and original small-jump role binding. Confirmed offline event timing is maintained in docs/features/skills.md.')
    (ROOT / 'assets/reference/battle-rules/move-scope-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print('Verified original 25027 movement-burning terrain, independent round-start chain and four code inputs.')

if __name__ == '__main__':
    main()
