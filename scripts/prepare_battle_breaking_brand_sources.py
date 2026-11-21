"""Verify original 10042 damage, 24052 mark and existing native bindings."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, value in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    definitions = {key: entities[key] for key in ('11010', '11011', '11012', '24052')}
    skill = definitions['11010']['FightSkillComponent']
    assert skill['ElementType'] == 102 and skill['SkillTargetType'] == 0 and skill['TargetType'] == 1
    hit_ref, = skill['HitList']
    assert hit_ref['ClassId'] == 11011 and hit_ref['ActOnSouce'] == 1
    hit = definitions['11011']['FightHitFixedComponent']
    assert hit['DamageId'] == 11012 and hit['DamageBasicProp'] == 1
    assert hit['DamageProp'] == 200 and hit['FixedDamageProp'] == 190 and not hit['IsTrueDamage']
    assert hit['Scope'][0]['Pos'] == dict(X=0, Y=1) and len(hit['Scope']) == 1
    assert hit['Scope'][0]['Delay'] == .11999999731779099
    application, = definitions['11012']['FightDamageComponent']['StatusList']
    assert application['StatusId'] == 24052 and application['TargetType'] == 2
    assert application['BasePercent'] == 1 and not application['AffectedByProp']
    mark = definitions['24052']['FightStatusComponent']
    assert mark['DurationRound'] == -1 and not mark['CanDispersed'] and mark['StatusType'] == 2
    assert mark['AI']['MoreHit'] and not mark['IsOpenStack']
    assert definitions['24052']['FightStatusPropBySkillComponent']['SkillType'] == [2, 6]
    consumer = definitions['24052']['FightStatusDamageSkillComponent']
    assert consumer['MaxCount'] == consumer['ConditionCount'] == 1 and consumer['AutoDestory']
    damage = definitions['11012']['FightDamageComponent']
    assert 'ExcludeCharacterTypes' not in damage['DisperseStatus']
    damage['DisperseStatus']['ExcludeCharacterTypes'] = []
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    item, = (row for row in rules['skill'] if row['ClassId'] == 10042)
    assert item['EcEntityId'] == 11010
    configuration = verify_selected_components(definitions, [item])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / 'common-source/Common' / (name + '.cs')) for name in (
        'FightSkillComponentInfo', 'FightHitFixedComponentInfo', 'FightDamageComponentImplement',
        'FightStatusComponentImplement', 'FightStatusPropBySkillComponent',
        'FightStatusPropBySkillComponentInfo', 'FightStatusDamageSkillComponent',
        'FightStatusDamageSkillComponentInfo', 'HitScopCfg')]
    originals.append(identity(code / 'client-source/MyFightStatusPropBySkill.cs'))
    originals.append(identity(code / 'client-source/MyFightHitBase.cs'))
    native, outputs, sources = [], [], []
    for name, entity, count in (
        ('skill-native-graph-assets-source-manifest.json', 11010, 10),
        ('first-promotion-status-assets-source-manifest.json', 24052, 2)):
        path = ROOT / 'assets/reference/battle-animation' / name
        document = json.loads(path.read_bytes())
        inputs = [entry for entry in document['native_inputs'] if f'/{entity}/' in entry['path']]
        views = [entry for entry in document['outputs'] if f'/{entity}/' in entry['path']]
        assert len(inputs) == len(views) == count
        for entry in inputs + views:
            assert identity(ROOT / entry['path']) == entry
        native.extend(inputs)
        outputs.extend(views)
        sources.append(identity(path))
    binding = ROOT / 'unity/Assets/Game/Resources/Combat/first-promotion-statuses.json'
    assert any(row['id'] == 24052 for row in json.loads(binding.read_bytes())['graphs'])
    proof = dict(manifest='884_156692', configuration=configuration,
        explicit_empty_dispel_defaults=['11012/DisperseStatus/ExcludeCharacterTypes'],
        original_code=originals,
        original_assemblies=[identity(code / 'managed/decoded/Assembly-CSharp.dll')],
        native_inputs=native, runtime_inputs=sources + outputs + [identity(binding)] + [identity(ROOT / path) for path in (
            'unity/Assets/Official/Combat/skill-rules.json',
            'unity/Assets/Official/Combat/battle-rules.json',
            'unity/Assets/Official/Combat/rules.json',
            'unity/Assets/Official/Skills/fidelity-texts.json',
            'unity/Assets/Game/Resources/Combat/skill-playables.json')],
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of 10042/11010/11011/11012 damage and '
            '24052 mark with existing native graph/status bindings. Runtime historical '
            'input bytes are retained. Behavior and validation limits belong to '
            'docs/character/skills-fighter.md.')
    (ROOT / 'assets/reference/battle-rules/breaking-brand-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified original 10042/11010 damage, 24052 mark and 12 native bindings.')


if __name__ == '__main__':
    main()
