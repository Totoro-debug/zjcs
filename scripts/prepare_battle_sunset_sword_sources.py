"""Verify original 10053 five-hit graph and its existing Windows native binding."""
import json
import re
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def sound_cues(path, events, asset_paths):
    text = path.read_text(encoding='utf-8-sig')
    components = {}
    for block in text.split('--- '):
        if '\n  SoundEvent:' not in block:
            continue
        component = re.match(r'!u!114 &(\d+)\n', block).group(1)
        guid = re.search(r'\n  SoundEvent:.*?guid: ([0-9a-f]{32})', block, re.S).group(1)
        event, = (entry for entry in events if
                  re.search(r'guid: (\w+)', Path(str(asset_paths[entry['id']]) + '.meta').read_text()).group(1) == guid)
        assert '\n  LoopInterval: -1\n' in block and '\n  Duration: 0\n' in block
        assert 'StopSoundEvent:\n    idInternal: 0\n    valueGuidInternal:\n    WwiseObjectReference: {fileID: 0}' in block
        components[component] = dict(event_id=event['id'], name=event['name'], asset_guid=guid)
    cues = [dict(components[component], delay=float(delay)) for delay, component in
            re.findall(r'- myTime: ([\d.]+)\n    myPlayable: \{fileID: (\d+)\}', text) if component in components]
    assert [(entry['name'], entry['event_id'], entry['delay']) for entry in cues] == [
        ('Skill_10053_1', 3496259616, 0), ('Skill_10053_2', 3496259619, .5), ('Skill_10053_3', 3496259618, 1.25)]
    return cues


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
        'MyFightHitBase', 'MyFightHitFixed', 'MyFightActionSkill', 'MyPlayableSound'))
    source = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    document = json.loads(source.read_bytes())
    selected = lambda entry: '/11250/' in entry['path'] or '/Global/10022.prefab' in entry['path']
    native = [entry for entry in document['native_inputs'] if selected(entry)]
    outputs = [entry for entry in document['outputs'] if selected(entry)]
    assert len(native) == len(outputs) == 8
    for entry in native + outputs:
        assert identity(ROOT / entry['path']) == entry
    sound_source = ROOT / 'assets/reference/battle-animation/native-sound-sources-source-manifest.json'
    sound_events = [entry for entry in json.loads(sound_source.read_bytes())['events']
                    if entry['name'] in ('Skill_10053_1', 'Skill_10053_2', 'Skill_10053_3')]
    assert len(sound_events) == 3 and all(entry['banks'] == ['Battle_SFX_02'] for entry in sound_events)
    sound_assets = []
    for event in sound_events:
        source_asset = ROOT / event['source']['path']
        assert identity(source_asset) == {key: event['source'][key] for key in ('path', 'sha256')}
        assert source_asset.stat().st_size == event['source']['bytes']
        sound_assets.extend([identity(source_asset), identity(ROOT / (event['source']['path'] + '.meta'))])
    original_graph, = (ROOT / entry['path'] for entry in native if entry['path'].endswith('/playable11250.prefab'))
    windows_graph, = (ROOT / entry['path'] for entry in outputs if entry['path'].endswith('/playable11250.prefab'))
    original_sound_assets = {entry['id']: original_graph.parents[5] /
        ('Wwise/ScriptableObjects/Event/' + entry['guid'] + '.asset') for entry in sound_events}
    for event in sound_events:
        original_asset = original_sound_assets[event['id']]
        original_text = original_asset.read_text()
        assert re.search(r'\n  id: (\d+)', original_text).group(1) == str(event['id'])
        assert re.search(r'\n  objectName: (\S+)', original_text).group(1) == event['name']
        assert re.search(r'\n  guid: (\S+)', original_text).group(1) == event['guid']
        native.extend([identity(original_asset), identity(Path(str(original_asset) + '.meta'))])
    original_cues = sound_cues(original_graph, sound_events, original_sound_assets)
    cues = sound_cues(windows_graph, sound_events, {entry['id']: ROOT / entry['source']['path'] for entry in sound_events})
    assert [{key: cue[key] for key in ('event_id', 'name', 'delay')} for cue in original_cues] == [
        {key: cue[key] for key in ('event_id', 'name', 'delay')} for cue in cues]
    binding = ROOT / 'unity/Assets/Game/Resources/Combat/skill-playables.json'
    cue, = (row for row in json.loads(binding.read_bytes())['skills'] if row['skill_entity_id'] == 11250)
    assert any(row['kind'] == 1 and row['animation'] == '100/skill_27' for row in cue['playables'])
    proof = dict(manifest='884_156692', configuration=configuration,
        explicit_empty_dispel_defaults=['10022/DisperseStatus/ExcludeCharacterTypes'],
        original_code=originals,
        original_assemblies=[identity(code / 'managed/decoded/Assembly-CSharp.dll')],
        native_inputs=native, original_sound_cues=original_cues, windows_sound_cues=cues, original_sound_events=sound_events,
        runtime_inputs=[identity(source), identity(sound_source)] + sound_assets + outputs + [identity(binding)] + [
            identity(ROOT / f'unity/Assets/Official/Combat/{name}.json')
            for name in ('skill-rules', 'battle-rules', 'rules')],
        verifier=identity(Path(__file__)),
        operation='APK-first verification of original 10053 five-hit area graph and existing Windows native assets. '
            'Historical runtime inputs are retained; behavior and validation limits belong to docs/features/character/skills-fighter.md.')
    (ROOT / 'assets/reference/battle-rules/sunset-sword-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified original 10053/11250 five-hit scope, three authored sound events, 10022 damage, fourteen native inputs and eight graph outputs.')


if __name__ == '__main__':
    main()
