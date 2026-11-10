"""Verify the six professions' configured EC references against the selected APK."""
import copy
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk
from official_tables import Constructors
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]
PROFESSIONS = {1, 2, 6, 10, 11, 15}
REFERENCE_FIELDS = {
    'AirComboStatusId', 'BlockedSkillId', 'CanTriggerStatusEntityIds', 'ClassId',
    'DamageId', 'DestroyedSkillId', 'DieId', 'FixSpeedMoveSkillId', 'GridItemStatus',
    'IgnoreHitSkills', 'LifespanStatusId', 'MoveBehaviourId', 'MoveSkillId',
    'RootSkillIds', 'ScopeGridItemId', 'SkillId', 'StatusEntClassId', 'StatusId',
    'StatusList', 'SummonId', 'SummonStatus', 'SummonStatusId',
}


def references(value, entities, field=''):
    if isinstance(value, dict):
        for key, child in value.items():
            yield from references(child, entities, key)
    elif isinstance(value, list):
        for child in value:
            yield from references(child, entities, field)
    elif type(value) is int and value > 0:
        if field in REFERENCE_FIELDS:
            assert str(value) in entities, ('Missing configured entity', field, value)
            yield str(value)
        elif str(value) in entities:
            raise AssertionError(('Review a new possible entity-reference field', field, value))


def main():
    combat = ROOT / 'unity/Assets/Official/Combat'
    rules = json.loads((combat / 'rules.json').read_bytes())
    item_ids = {item for row in rules['profession_base'] if row['Profession'] in PROFESSIONS
                for item in row['Skills'] + row['FightUsedSkills']}
    skills = [row for row in rules['skill'] if row['ClassId'] in item_ids]
    assert len(item_ids) == len(skills) == 104, 'Review the six-profession skill scope.'
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, components in json.loads((combat / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(components)
    roots = {str(row['EcEntityId']) for row in skills if row['EcEntityId'] > 0}
    roots.update(str(status) for row in skills for status in row['PassiveStatusIdList'])
    roots.update(str(entity) for row in skills for entity in row['DirectionEcEntityId'].values())
    pending = list(roots)
    selected = set()
    while pending:
        key = pending.pop()
        if key in selected:
            continue
        selected.add(key)
        pending.extend(set(references(entities[key], entities)) - selected)

    definitions = {key: copy.deepcopy(entities[key]) for key in selected}
    empty_defaults = 0
    for key, components in definitions.items():
        damage = components.get('FightDamageComponent')
        if damage is not None:
            disperse = damage['DisperseStatus']
            assert 'ExcludeCharacterTypes' not in disperse
            # Selected 884 has one nonempty list, consumed by the catalog adapter.
            disperse['ExcludeCharacterTypes'] = [7, 3] if key == '11552' else []
            if key != '11552':
                empty_defaults += 1
    for key in ('25019', '25022'):
        hp = definitions[key]['FightStatusHpLessThanComponent']
        assert 'ReportSkillTriggerCount' not in hp
        hp['ReportSkillTriggerCount'] = False
    # The selected-version adapter is independently exercised by revival tests.
    protection = definitions['24083']['FightStatusComponent']
    assert protection['ForbiddenOnApplicatorDie'] is False
    protection['ForbiddenOnApplicatorDie'] = True
    verified = verify_selected_components(definitions, skills)

    apk = Apk.current()
    try:
        parser = Constructors(ROOT / '.tools/source-packages/current' / apk.version / 'official-code')
        tables = {name: cls for cls, name in re.findall(
            r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source('Tables'))}
        obj = apk.asset('config_luban', '/skill.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        original = {row['ClassId']: row for row in parser.table(raw, tables['skill'])
                    if row['ClassId'] in item_ids}
        assert set(original) == item_ids
        for row in skills:
            actual = json.loads(json.dumps(original[row['ClassId']]))
            assert all(key in actual and actual[key] == value for key, value in row.items()), (
                'Selected skill table differs', row['ClassId'])
        table_source = dict(source=apk.provenance(obj),
            object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
            payload_sha256=hashlib.sha256(raw).hexdigest(),
            runtime_fields=sorted(skills[0]),
            verified_projection_sha256=hashlib.sha256(json.dumps(sorted(skills,
                key=lambda row: row['ClassId']), sort_keys=True, separators=(',', ':')).encode()).hexdigest())
    finally:
        apk.archive.close()
    print(f'Selected APK source check passed: {len(skills)} complete skill rows; '
          f'{len(selected)} configured entity references; {empty_defaults} explicit empty '
          'dispel defaults, two false report defaults and the 11552/24083 adapters. '
          'No runtime payload or historical source hash was changed. '
          'This does not verify server scheduling or native presentation.')
    code = ROOT / '.tools/source-packages/current/884_156692/official-code/common-source/Common'
    return dict(manifest='884_156692', professions=sorted(PROFESSIONS),
        skill_item_ids=sorted(item_ids), configured_entity_ids=sorted(map(int, selected)),
        skill_table=table_source, configuration=verified,
        explicit_empty_dispel_defaults=empty_defaults,
        explicit_false_report_defaults=[25019, 25022],
        selected_adapters={'11552/DisperseStatus/ExcludeCharacterTypes': [7, 3],
            '24083/FightStatusComponent/ForbiddenOnApplicatorDie': True},
        original_code=[identity(code / name) for name in ('DamageDisperseStatusCfg.cs', 'GameCharacterType.cs')],
        adapter=identity(ROOT / 'unity/Assets/Game/Runtime/Offline/Combat/FightOfficialCombatCatalog.Disperse.cs'),
        verifier=identity(Path(__file__)),
        operation='APK-first comparison of all consumed fields in the 104 skill rows and the '
            'configured EC references, including explicit direction entities. Only named immutable '
            'version defaults/adapters are projected; original runtime payloads and hashes remain intact. '
            'This source record does not establish server scheduling or native presentation coverage.')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write-source-manifest', action='store_true')
    args = parser.parse_args()
    proof = main()
    if args.write_source_manifest:
        (ROOT / 'assets/reference/battle-rules/first-promotion-source-manifest.json').write_text(
            json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
