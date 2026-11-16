"""Verify directional skill inputs against the selected APK; retain legacy identities."""
import hashlib
import json
import re
from pathlib import Path
from verify_battle_skill_graph_rules import verify_selected_components
from official_apk import Apk
from official_tables import Constructors

ROOT = Path(__file__).resolve().parents[1]


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    apk = Apk.current()
    if apk.version != '884_156692':
        raise ValueError('Review the selected directional skill source version')
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    parser = Constructors(code)
    mapping = {name: cls for cls, name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source('Tables'))}
    runtime = ROOT / 'unity/Assets/Official/Combat/rules.json'
    rules = json.loads(runtime.read_bytes())
    professions = {1, 10, 2, 6, 11, 15}
    rows = [row for row in rules['profession_base'] if row['Profession'] in professions]
    wanted = {item for row in rows for item in row['Skills'] + row['FightUsedSkills']}
    originals = {}
    try:
        for name, key, ids in [('profession_base', 'Profession', professions), ('skill', 'ClassId', wanted), ('item', 'ClassId', wanted)]:
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            selected = json.loads(json.dumps([row for row in parser.table(raw, mapping[name]) if row[key] in ids]))
            migrated = [row for row in rules[name] if row[key] in ids]
            if sorted(selected, key=lambda row: row[key]) != sorted(migrated, key=lambda row: row[key]):
                raise ValueError('Selected original table differs from runtime: ' + name)
            if len(selected) != len(ids):
                raise ValueError('Incomplete selected original table: ' + name)
            originals[name] = dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(raw).hexdigest(), rows=selected)
    finally:
        apk.archive.close()
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((runtime.parent / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    skill = next(row for row in rules['skill'] if row['ClassId'] == 10026)
    entity_ids = {skill['EcEntityId'], *skill['DirectionEcEntityId'].values()}
    definitions = {}
    pending = list(entity_ids)
    while pending:
        key = str(pending.pop())
        if key in definitions:
            continue
        definitions[key] = entities[key]
        for component in entities[key].values():
            for link in component.get('HitList', []):
                pending.append(link['ClassId'])
            if component.get('DamageId', 0):
                pending.append(component['DamageId'])
    # The selected formatter explicitly restores this immutable empty default.
    for key in ('10007', '10023'):
        definitions[key]['FightDamageComponent']['DisperseStatus']['ExcludeCharacterTypes'] = []
    verification = verify_selected_components(definitions, [skill])
    sources = [identity(code / name) for name in [
        'common-source/Common/FightSkillData.cs', 'common-source/Common/SkillEntityData.cs',
        'common-source/Common/Pos2d.cs', 'common-source/Common/Direction.cs', 'common-source/Common/SkillInfo.cs']]
    graph_source = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    graph = json.loads(graph_source.read_bytes())
    def skill_graph(entry):
        return any('/' + str(entity) + '/' in entry['path'] for entity in entity_ids)
    native_inputs = [entry for entry in graph['native_inputs'] if skill_graph(entry)]
    native_outputs = [entry for entry in graph['outputs'] if skill_graph(entry)]
    assert len(native_inputs) == len(native_outputs) == 32
    for entry in native_inputs + native_outputs:
        assert identity(ROOT / entry['path']) == entry
    sound = ROOT / ('unity/Assets/Official/Combat/SkillGraphRuntime/Resources/Combat/SkillGraphs/'
        'Wwise/ScriptableObjects/Event/9E556E1D-BAA6-4D09-87A8-4051FFE64935.asset')
    assert 'objectName: Skill_10026\n  id: 3761013056\n' in sound.read_text(encoding='utf8')
    event_guid = re.search(r'^guid: (\w+)$', sound.with_suffix('.asset.meta').read_text(), re.M)[1]
    for entity in entity_ids:
        prefab = ROOT / ('unity/Assets/Official/Combat/SkillGraphRuntime/Resources/Combat/'
            f'SkillGraphs/Prefabs/EcEntity/Fight/FightSkill/{entity}/playable{entity}.prefab')
        text = prefab.read_text(encoding='utf8')
        block, = (part for part in text.split('--- !u!114') if 'SoundEvent:' in part)
        file_id = re.match(r' &(\d+)', block)[1]
        assert 'guid: ' + event_guid in block
        assert re.search(r'myTime: 0\s+myPlayable: \{fileID: ' + file_id + r'\}', text)
    sounds = ROOT / 'unity/Assets/Official/Combat/NativeSound/Resources/Combat/Wwise/catalog.json'
    assert dict(id=3761013056, name='Skill_10026') in json.loads(sounds.read_bytes())['events']
    record = dict(manifest=apk.version, official_tables=originals, official_code=sources,
        runtime_input=identity(runtime), selected_configuration=verification,
        explicit_empty_dispel_defaults=['10007/DisperseStatus/ExcludeCharacterTypes', '10023/DisperseStatus/ExcludeCharacterTypes'],
        native_inputs=native_inputs, native_outputs=native_outputs,
        native_source_manifest=identity(graph_source),
        native_sound_inputs=[identity(sound), identity(sound.with_suffix('.asset.meta')), identity(sounds)],
        validation_inputs=[identity(ROOT / name) for name in (
            'scripts/test_battle_directional_skills.ps1', 'scripts/verify_fire_slash_receipt.py',
            'scripts/test_first_promotion_skill_player.ps1',
            'scripts/test_acquired_active_skill_flow.ps1',
            'unity/Assets/Game/Runtime/Presentation/Home/SkillDamageTestValidation.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/SkillDamageTestValidation.AcquiredActive.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/SkillDamageTestValidation.AcquiredActive.cs.meta',
            'unity/Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGraphCapabilityValidation.FirstPromotion.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGraphCapabilityValidation.FireSlash.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGraphCapabilityValidation.FireSlash.cs.meta')],
        generator=identity(Path(__file__)),
        operation='APK-first full-field verification of six professions and 104 direct skill/item rows. '
        'Verify default and three directional skill entities, fixed hits and damage dependencies. '
        'Compare all 32 original native graph inputs and bound runtime outputs. '
        'Original dictionary construction, per-entity properties and Pos2d.LookDirection are reused; '
        'target-based directional selection is a user-confirmed offline rule, not recovered server code.')
    target = ROOT / 'assets/reference/battle-rules/directional-skill-source-manifest.json'
    target.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
    print('Verified six professions, 104 direct skills/items and directional skill/hit configuration.')


if __name__ == '__main__':
    main()
