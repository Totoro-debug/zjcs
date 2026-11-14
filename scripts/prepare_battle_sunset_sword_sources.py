"""Verify original 10053 five-hit graph and its existing Windows native binding."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, value in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    definitions = {key: entities[key] for key in ('11250', '11251', '10022')}
    skill = definitions['11250']['FightSkillComponent']
    assert skill['SkillTargetType'] == 0 and skill['TargetType'] == 1 and skill['ElementType'] == 0
    assert skill['ActionType'] == 2 and not skill['SourceMoveList']
    assert [entry['ClassId'] for entry in skill['HitList']] == [11251] * 5
    assert all(entry['ActOnSouce'] == 1 for entry in skill['HitList'])
    assert [round(entry['Delay'], 3) for entry in skill['HitList']] == [.726, .861, .987, 1.14, 1.39]
    hit = definitions['11251']['FightHitFixedComponent']
    assert hit['DamageId'] == 10022 and hit['DamageBasicProp'] == 1
    assert hit['DamageProp'] == 200 and hit['FixedDamageProp'] == 190 and not hit['IsTrueDamage']
    assert [(entry['Pos']['X'], entry['Pos']['Y']) for entry in hit['Scope']] == [
        (-1, 2), (-1, 1), (0, 2), (0, 1), (1, 2), (1, 1)]
    assert all(entry['Delay'] == 0 for entry in hit['Scope'])
    damage = definitions['10022']['FightDamageComponent']
    assert not damage['StatusList'] and damage['MoveCfg']['MoveBehaviourId'] == 0
    assert 'ExcludeCharacterTypes' not in damage['DisperseStatus']
    damage['DisperseStatus']['ExcludeCharacterTypes'] = []
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    item, = (row for row in rules['skill'] if row['ClassId'] == 10053)
    assert item['EcEntityId'] == 11250
    configuration = verify_selected_components(definitions, [item])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / 'common-source/Common' / (name + '.cs')) for name in (
        'FightSkillComponentInfo', 'FightSkillComponentImplement', 'FightHitFixedComponentInfo',
        'FightDamageComponentImplement', 'HitScopCfg')]
    originals.extend(identity(code / 'client-source' / (name + '.cs')) for name in (
        'MyFightHitBase', 'MyFightHitFixed', 'MyFightActionSkill'))
    source = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    document = json.loads(source.read_bytes())
    selected = lambda entry: '/11250/' in entry['path'] or '/Global/10022.prefab' in entry['path']
    native = [entry for entry in document['native_inputs'] if selected(entry)]
    outputs = [entry for entry in document['outputs'] if selected(entry)]
    assert len(native) == len(outputs) == 8
    for entry in native + outputs:
        assert identity(ROOT / entry['path']) == entry
    binding = ROOT / 'unity/Assets/Game/Resources/Combat/skill-playables.json'
    cue, = (row for row in json.loads(binding.read_bytes())['skills'] if row['skill_entity_id'] == 11250)
    assert any(row['kind'] == 1 and row['animation'] == '100/skill_27' for row in cue['playables'])
    proof = dict(manifest='884_156692', configuration=configuration,
        explicit_empty_dispel_defaults=['10022/DisperseStatus/ExcludeCharacterTypes'],
        original_code=originals,
        original_assemblies=[identity(code / 'managed/decoded/Assembly-CSharp.dll')],
        native_inputs=native, runtime_inputs=[identity(source)] + outputs + [identity(binding)] + [
            identity(ROOT / f'unity/Assets/Official/Combat/{name}.json')
            for name in ('skill-rules', 'battle-rules', 'rules')],
        verifier=identity(Path(__file__)),
        operation='APK-first verification of original 10053 five-hit area graph and existing Windows native assets. '
            'Historical runtime inputs are retained; behavior and validation limits belong to docs/features/skills-fighter.md.')
    (ROOT / 'assets/reference/battle-rules/sunset-sword-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified original 10053/11250 five-hit scope, 10022 damage and eight native inputs/outputs.')


if __name__ == '__main__':
    main()
