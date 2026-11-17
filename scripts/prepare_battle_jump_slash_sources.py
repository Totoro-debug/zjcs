"""Verify original jump slash rules, armor break, native playables and sounds."""
import argparse
import copy
import hashlib
import json
import re
from pathlib import Path

from map14_audio_source import AudioSource
from official_apk import Apk
from official_tables import Constructors
from validation_workspace import validation_directory
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'assets/reference/battle-rules/jump-slash-source-manifest.json'


def cue(prefab, asset):
    guid = re.search(r'^guid: (\w+)$', Path(str(asset) + '.meta').read_text(encoding='utf8'), re.M)[1]
    text = prefab.read_text(encoding='utf-8-sig')
    components = [re.match(r' &(\d+)', block)[1] for block in text.split('--- !u!114')
                  if '\n  SoundEvent:' in block and 'guid: ' + guid in block]
    component, = components
    times = [float(value) for value in re.findall(
        r'- myTime: ([\d.]+)\s+myPlayable: \{fileID: ' + component + r'\}', text)]
    if times != [0.0]:
        raise ValueError('Original jump slash native sound cue changed')
    return times


def build():
    combat = ROOT / 'unity/Assets/Official/Combat'
    entities = {}
    skill_data = json.loads((combat / 'skill-rules.json').read_bytes())
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, components in json.loads((combat / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(components)
    defs = {key: copy.deepcopy(entities[key]) for key in ('10670', '10671', '10672', '10673', '24113')}
    skill = defs['10670']['FightSkillComponent']
    hit = defs['10671']['FightHitFixedComponent']
    move = defs['10672']['FightActionSkillMoveComponent']
    damage = defs['10673']['FightDamageComponent']
    status = defs['24113']['FightStatusComponent']
    offsets = {(p['Pos']['X'], p['Pos']['Y']) for p in hit['Scope']}
    application, = damage['StatusList']
    root_hit, = skill['HitList']
    source_move, = skill['SourceMoveList']
    if (skill['SkillTargetType'] != 2 or skill['TargetType'] != 1 or skill['ElementType'] != 0
            or root_hit['ClassId'] != 10671 or abs(root_hit['Delay'] - .65) > .00001
            or root_hit['ActOnSouce'] != 2 or source_move['MoveBehaviourId'] != 10672
            or abs(source_move['Delay'] - .1) > .00001 or not source_move['TowardsTargetPos']
            or not source_move['AiMustMoveTo'] or move['PassHeight'] != 3 or abs(move['MoveDuration'] - .2) > .00001
            or hit['DamageId'] != 10673 or hit['DamageType'] != 1 or hit['IsTrueDamage']
            or offsets != {(-1, 0), (1, 0), (0, -1), (0, 1)}
            or application['StatusId'] != 24113 or application['TargetType'] != 2
            or application['BasePercent'] != .5 or not application['AffectedByProp']
            or status['DurationRound'] != 2 or status['RoundTarget'] != 2 or status['RoundUpdateTiming'] != 1):
        raise ValueError('Review the original jump, hit, status or timing configuration')
    normalized = []
    if 'ExcludeCharacterTypes' not in damage['DisperseStatus']:
        damage['DisperseStatus']['ExcludeCharacterTypes'] = []
        normalized.append('10673/DisperseStatus/ExcludeCharacterTypes')
    item, = (row for row in json.loads((combat / 'rules.json').read_bytes())['skill'] if row['ClassId'] == 10015)
    if item['EcEntityId'] != 10670:
        raise ValueError('The original jump slash item binding changed')
    configuration = verify_selected_components(defs, [item])
    prop, = (row for row in skill_data['tables']['entity_prop'] if row['EntityId'] == 24113)
    if prop['PropFactors'] != {'252': -3000}:
        raise ValueError('The original armor-break defense factor changed')
    with Apk.current() as apk:
        version = apk.version
        code = ROOT / '.tools/source-packages/current' / version / 'official-code'
        constructors = Constructors(code)
        mapping = {name: cls for cls, name in re.findall(
            r'new (Tb\w+)\(loader\("([^\"]+)"\)\)', constructors.source('Tables'))}
        obj = apk.asset('config_luban', '/entity_prop.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        rows = json.loads(json.dumps(constructors.table(raw, mapping['entity_prop'])))
        selected, = (row for row in rows if row['EntityId'] == 24113)
        if not all(selected.get(key) == value for key, value in prop.items()):
            raise ValueError('Runtime armor-break properties differ from the official table')
        property_source = dict(source=apk.provenance(obj), payload_sha256=hashlib.sha256(raw).hexdigest(),
                               row=selected, runtime_row=prop)
    originals = [identity(code / 'common-source/Common' / (name + '.cs')) for name in (
        'FightSkillComponentInfo', 'FightSkillComponentImplement', 'FightHitFixedComponentInfo',
        'FightDamageComponentImplement', 'FightActionSkillMoveComponent', 'FightActionSkillMoveComponentInfo',
        'FightStatusComponent', 'FightStatusPropComponent', 'SkillTargetType', 'HitScopCfg')]
    originals += [identity(code / 'client-source' / (name + '.cs')) for name in (
        'MyFightHitBase', 'MyFightHitFixed', 'MyFightActionSkill', 'MyFightActionSkillMove', 'MyPlayableSound')]
    graph_path = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    graph = json.loads(graph_path.read_bytes())
    guids = ('54422349-E48C-45BA-9C21-5E7CD5ACA0A5', '11CD22BD-263D-460B-839A-890625AE5696')
    select = lambda row: '/10670/' in row['path'] or any(g in row['path'] for g in guids)
    native = [entry for entry in graph['native_inputs'] if select(entry)]
    outputs = [entry for entry in graph['outputs'] if select(entry)]
    if len(native) != 18 or len(outputs) != 18:
        raise ValueError('The original jump slash graph or sound references are incomplete')
    status_path = ROOT / 'assets/reference/battle-animation/first-promotion-status-assets-source-manifest.json'
    status_graph = json.loads(status_path.read_bytes())
    status_native = [entry for entry in status_graph['native_inputs'] if '/24113/24113.prefab' in entry['path']]
    status_outputs = [entry for entry in status_graph['outputs'] if '/24113/24113.prefab' in entry['path']]
    if len(status_native) != 2 or len(status_outputs) != 2:
        raise ValueError('The original armor-break status prefab is incomplete')
    for entry in native + outputs + status_native + status_outputs:
        if identity(ROOT / entry['path']) != entry:
            raise ValueError('Original jump slash asset identity differs: ' + entry['path'])
    sound_events = []
    with AudioSource(expected_version=version, work_directory=validation_directory()) as audio:
        for name, event_id, guid, prefab in (
            ('Skill_10015_1', 1557672118, guids[0], 'playable10670_move.prefab'),
            ('Skill_10015_2', 1557672117, guids[1], 'playable10670.prefab')):
            original_graph, = (ROOT / e['path'] for e in native if e['path'].endswith('/' + prefab))
            runtime_graph, = (ROOT / e['path'] for e in outputs if e['path'].endswith('/' + prefab))
            original_sound, = (ROOT / e['path'] for e in native if e['path'].endswith('/' + guid + '.asset'))
            runtime_sound, = (ROOT / e['path'] for e in outputs if e['path'].endswith('/' + guid + '.asset'))
            for asset in (original_sound, runtime_sound):
                text = asset.read_text(encoding='utf8')
                if (re.search(r'\n  id: (\d+)', text)[1] != str(event_id)
                        or re.search(r'\n  objectName: (\S+)', text)[1] != name
                        or re.search(r'\n  guid: (\S+)', text)[1] != guid):
                    raise ValueError('The original jump slash sound identity differs')
            if cue(original_graph, original_sound) != cue(runtime_graph, runtime_sound):
                raise ValueError('The original jump slash sound cue differs')
            actual_id, banks = audio.load_event(name)
            if actual_id != event_id or banks != ['Battle_SFX_02']:
                raise ValueError('Original jump slash event is absent from its official bank')
            sound_events.append(dict(name=name, id=event_id, guid=guid, banks=banks, prefab=prefab))
        sound_packages = audio.receipts()
    catalog = ROOT / 'unity/Assets/Official/Combat/NativeSound/Resources/Combat/Wwise/catalog.json'
    if not any(row['bank'] == 'Battle_SFX_02' for row in json.loads(catalog.read_bytes())['packages']):
        raise ValueError('The original jump slash sound bank is not loaded at runtime')
    bindings = ROOT / 'unity/Assets/Game/Resources/Combat/skill-playables.json'
    binding, = (row for row in json.loads(bindings.read_bytes())['skills'] if row['skill_entity_id'] == 10670)
    if not any(row['kind'] == 1 and row['animation'] == '100/skill_02' for row in binding['playables']):
        raise ValueError('The original jump slash character motion is not bound')
    return dict(manifest=version, configuration=configuration, explicit_empty_dispel_defaults=normalized,
                status_property=property_source, original_code=originals, native_source_manifest=identity(graph_path),
                native_inputs=native, native_outputs=outputs, status_source_manifest=identity(status_path),
                status_inputs=status_native, status_outputs=status_outputs, sound_events=sound_events,
                sound_packages=sound_packages, sound_catalog=identity(catalog), character_binding=identity(bindings),
                generator=identity(Path(__file__)))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    proof = build()
    if args.check:
        if not OUTPUT.exists() or json.loads(OUTPUT.read_bytes()) != proof:
            raise ValueError('The jump slash source receipt differs')
    else:
        OUTPUT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
    print('Verified original jump slash, armor-break, native graphs and both sound events.')
