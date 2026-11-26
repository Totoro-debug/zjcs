"""Restore Potential Rebirth's original status and recovery-skill graphs."""
import hashlib
import json
import re
import struct
from pathlib import Path

import yaml
from Crypto.Hash import MD4

from battle_native_script_bindings import shared_bindings
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_movement_effects import SCRIPTS
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_status_orbit_assets import build as build_graphs


FAMILY = 'PassiveRevival'
DEST = ROOT / 'unity/Assets/Official/Combat/PassiveRevival/Resources/Combat/PassiveRevival'
STATUS_DATA = ROOT / 'unity/Assets/Game/Resources/Combat/passive-revival-statuses.json'
GRAPH_DATA = ROOT / 'unity/Assets/Game/Resources/Combat/passive-revival-current-skill-graphs.json'
ASSET_RECEIPT = ROOT / 'assets/reference/battle-animation/passive-revival-assets-source-manifest.json'
GRAPH_RECEIPT = ROOT / 'assets/reference/battle-animation/passive-revival-current-skill-graphs-source-manifest.json'
SOURCE_INDEX = ROOT / 'unity/Assets/Game/Resources/Combat/skill-native-graph-sources.json'
GUID = re.compile(r'(?m)^guid: ([a-f0-9]{32})$')
SCRIPT_BLOCK = re.compile(r'(?ms)^--- !u!114 &(-?\d+)\nMonoBehaviour:\n(.*?)(?=^--- !u!|\Z)')
GUID_FIELD = re.compile(r'guid: ([a-f0-9]{32})')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def script_file_id(name, namespace=''):
    raw = ('s\0\0\0' + namespace + name).encode()
    return struct.unpack('<i', MD4.new(raw).digest()[:4])[0]


def selected_cache(source):
    base = ROOT / '.tools/source-packages/current' / source.version
    wanted = {
        'defaultpackage_assets_prefabs_ecentity_fight_fightskill_25064.bundle',
        'defaultpackage_assets_prefabs_ecentity_fight_fightskill_11770.bundle',
    }
    matches = []
    for cache in base.glob('passive-revival-10581-full-*'):
        receipt_path = cache / 'native-export-source-manifest.json'
        inputs_path = cache / 'source-manifest.json'
        texture_path = cache / 'all-assets/windows-textures/source-manifest.json'
        shader_path = cache / 'all-assets/windows-shaders/source-manifest.json'
        if not all(path.is_file() for path in (receipt_path, inputs_path, texture_path, shader_path)):
            continue
        receipt = json.loads(receipt_path.read_bytes())
        inputs = json.loads(inputs_path.read_bytes())
        textures = json.loads(texture_path.read_bytes())
        shaders = json.loads(shader_path.read_bytes())
        bundle_names = {Path(row['output']).name for row in inputs.get('inputs', [])}
        if (receipt.get('manifest') == source.version and receipt.get('completed')
                and inputs.get('manifest') == source.version and wanted <= bundle_names
                and textures.get('manifest') == source.version and textures.get('completed')
                and shaders.get('manifest') == source.version and shaders.get('completed')):
            matches.append(cache)
    if len(matches) != 1:
        raise ValueError('Expected one complete current conversion cache for 10581: ' + repr(matches))
    return matches[0]


def verify_official_rules(source):
    combat = ROOT / 'unity/Assets/Official/Combat'
    current_tables = ROOT / '.tools/source-packages/current' / source.version / 'official-data/tables.json'
    tables = json.loads(current_tables.read_bytes())
    proofs = {'official_tables': identity(current_tables), 'manifest': source.version}
    skill = next(row for row in tables['skill'] if row['ClassId'] == 10581)
    if skill['EcEntityId'] != 25064 or skill['PassiveStatusIdList'] != [25064]:
        raise ValueError('Official 10581 passive status binding changed')
    entities = {}
    for filename in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((combat / filename).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    status = entities['25064']['FightStatusHpLimitComponent']
    trigger, = status['SkillList']
    if (status['LowerLimit'] != 1 or status['MaxCount'] != 1 or status['AutoRemove']
            or status['StatusList'] or status['InvokeCfgs'] or trigger['SkillId'] != 11770
            or trigger['BasePercent'] != 1.0 or trigger['AffectedByProp']
            or trigger['Source'] != 2 or trigger['Target'] != 2 or trigger['TargetPos'] != 1):
        raise ValueError('Official 25064 fatal-listener rule changed')
    skill_hit, = entities['11770']['FightSkillComponent']['HitList']
    fixed_hit = entities['11771']['FightHitFixedComponent']
    disperse = entities['11772']['FightDamageComponent']['DisperseStatus']
    if (skill_hit['ClassId'] != 11771 or skill_hit['Delay'] != 0.0
            or fixed_hit['DamageId'] != 11772 or fixed_hit['DamageActionType'] != 5
            or fixed_hit['DamageType'] != 1 or fixed_hit['DamageBasicProp'] != 1
            or fixed_hit['DamageProp'] != 210 or fixed_hit['FixedDamageProp'] != 170
            or fixed_hit['UseDamageFactor'] or disperse['BasePercent'] != 1.0
            or disperse['AffectedByProp'] or disperse['Count'] != -1
            or disperse['IsRandom'] or disperse['TargetTypes'] != [2, 3, 1]):
        raise ValueError('Official 11770 -> 11771 -> 11772 recovery rules changed')
    return dict(manifest=source.version, source_rules=proofs, skill=skill,
        fatal_listener={key: status[key] for key in ('LowerLimit', 'MaxCount', 'AutoRemove', 'StatusList', 'SkillList', 'InvokeCfgs', 'ListenerSetting')},
        recovery_skill={key: entities['11770']['FightSkillComponent'][key]
                        for key in ('SkillType', 'SkillTargetType', 'HitList', 'ActionType', 'Duration', 'AllEndedDuration')},
        recovery_hit=fixed_hit, recovery_damage=entities['11772']['FightDamageComponent'])


def runtime_graph_record(entity_id, group, client_type, runtime, guid_paths, code_root,
                         prefab_entity_id=None):
    source_script = Path(runtime + '.meta')
    script_guid = GUID.search((ROOT / source_script).read_text(encoding='utf-8-sig'))[1]
    prefab_entity_id = entity_id if prefab_entity_id is None else prefab_entity_id
    prefab = DEST / f'Prefabs/EcEntity/Fight/FightSkill/{prefab_entity_id}/{entity_id}.prefab'
    text = prefab.read_text(encoding='utf-8-sig')
    blocks = []
    for component_id, block in SCRIPT_BLOCK.findall(text):
        if re.search(r'm_Script: \{fileID: 11500000, guid: ' + script_guid + r', type: 3\}', block):
            fields = yaml.safe_load('MonoBehaviour:\n' + block)['MonoBehaviour']
            blocks.append((int(component_id), fields))
    if len(blocks) != 1:
        raise ValueError('Expected one original ' + client_type + ' in skill graph ' + str(entity_id))
    component_id, fields = blocks[0]
    playables = []
    for field, pointer in fields.items():
        if not isinstance(pointer, dict) or pointer.get('guid') not in guid_paths:
            continue
        target = guid_paths[pointer['guid']]
        if target.suffix != '.prefab':
            continue
        playables.append(dict(field=field, resource='Combat/' + FAMILY + '/'
            + target.relative_to(DEST).with_suffix('').as_posix()))
    if group == 'skills' and not any(row['field'] == 'myPlayablePrefab' for row in playables):
        raise ValueError('Original 11770 playable prefab binding is absent')
    id_field = {
        'skills': 'skill_entity_id',
        'hits': 'hit_entity_id',
        'damages': 'damage_entity_id',
    }[group]
    return {id_field: entity_id,
        'entity_resource': 'Combat/' + FAMILY + '/'
            + prefab.relative_to(DEST).with_suffix('').as_posix(),
        'playables': playables, 'component_file_id': component_id}


def build():
    source = OfficialSource()
    rule_proof = verify_official_rules(source)
    cache = selected_cache(source)
    root = dict(shared_bindings())
    mapped = {}
    mapped.update(root.pop('MAPPED'))
    native_bindings = {}
    for file_id, (_, name) in list(mapped.items()):
        if name in {'ApplyEnableOnPlay', 'MyPlayableFollowBone', 'ApplyOnStopFadeOut',
                    'ApplyByGroundType', 'MyPlayableSequence'}:
            targets = list((ROOT / 'unity/Assets/Game/Runtime/Official').rglob(name + '.cs'))
            if len(targets) != 1:
                raise ValueError('Missing local migrated script for native graph component: ' + name)
            mapped.pop(file_id)
            native_bindings[file_id] = {'runtime': targets[0].relative_to(ROOT).as_posix()}
    code = cache.parent / 'official-code'
    native_scripts = [
        'unity/Assets/Game/Runtime/Official/StoryPlayables/ApplyEnableOnPlay.cs',
        'unity/Assets/Game/Runtime/Official/StoryFollowBone/MyPlayableFollowBone.cs',
        'unity/Assets/Game/Runtime/Official/StoryFollowBone/ApplyOnStopFadeOut.cs',
        'unity/Assets/Game/Runtime/Official/StoryGroundEffects/ApplyByGroundType.cs',
        'unity/Assets/Game/Runtime/Official/StoryPlayables/MyPlayableSequence.cs']
    for file_id, (folder, name) in list(mapped.items()):
        original = [code / 'client-source' / (name + '.cs'),
                    code / 'game-framework-source/GameFramework' / (name + '.cs')]
        if not any(path.is_file() for path in original):
            targets = list((ROOT / 'unity/Assets/Game/Runtime/Official').rglob(name + '.cs'))
            if len(targets) != 1:
                raise ValueError('Missing local migrated script for native graph component: ' + name)
            mapped.pop(file_id)
            native_scripts.append(targets[0].relative_to(ROOT).as_posix())
            native_bindings[file_id] = {'runtime': targets[0].relative_to(ROOT).as_posix()}
    source_scripts = root.get('source_scripts', {})
    for file_id, binding in list(source_scripts.items()):
        original_path = code / binding.get('source_root', 'client-source') / binding['source']
        if not original_path.is_file():
            target = ROOT / binding['runtime']
            if not target.is_file():
                raise ValueError('Missing local migrated source script: ' + binding['name'])
            source_scripts.pop(file_id)
            native_scripts.append(target.relative_to(ROOT).as_posix())
            native_bindings[file_id] = {'runtime': target.relative_to(ROOT).as_posix()}
    reference_scripts = root.get('reference_scripts', {})
    exported_assets = cache / 'native-export/ExportedProject/Assets'
    for file_id, binding in list(reference_scripts.items()):
        if not (exported_assets / binding['native_asset']).is_file():
            reference_scripts.pop(file_id)
    mapped[-1481699058] = ('BattleStatusPlayables', 'MyFightStatus')
    mapped[-773406488] = ('BattleGridItemEntities', 'MyFightStatusHpLimit')
    graph_roots = {
        25064: 'Prefabs/EcEntity/Fight/FightSkill/25064/25064.prefab',
        11770: 'Prefabs/EcEntity/Fight/FightSkill/11770/11770.prefab',
        11771: 'Prefabs/EcEntity/Fight/FightSkill/11770/11771.prefab',
        11772: 'Prefabs/EcEntity/Fight/FightSkill/11770/11772.prefab',
    }
    build_graphs(DEST=DEST, DATA=STATUS_DATA, RECEIPT=ASSET_RECEIPT,
        ROOTS={}, graph_roots=graph_roots, MAPPED=mapped, family=FAMILY,
        shader_prefix='PassiveRevival_', generator=Path(__file__).resolve(),
        limits=['The migrated graph set contains original status 25064 and its native 11770 -> 11771 -> 11772 recovery chain.'],
        native_manifest=source.version, native_cache=cache, native_export_name='native-export',
        native_scripts=sorted(set(native_scripts)),
        native_script_bindings=native_bindings,
        native_preprocess=lambda text, _path: text.replace(
            '0000000deadbeef15deadf00d0000000',
            '00000000000000000000000000000000'),
        **root)

    guid_paths = {}
    for path in DEST.rglob('*.meta'):
        match = GUID.search(path.read_text(encoding='utf-8-sig'))
        if match:
            if match[1] in guid_paths:
                raise ValueError('Duplicate restored original graph GUID: ' + match[1])
            guid_paths[match[1]] = path.with_suffix('')
    code = cache.parent / 'official-code'
    skill_rows = [runtime_graph_record(11770, 'skills', 'MyFightSkill',
        'unity/Assets/Game/Runtime/Official/BattleSkillPlayables/MyFightSkill.cs', guid_paths, code)]
    hit_rows = [runtime_graph_record(11771, 'hits', 'MyFightHitFixed',
        'unity/Assets/Game/Runtime/Official/BattleSkillPlayables/MyFightHitFixed.cs', guid_paths, code,
        prefab_entity_id=11770)]
    damage_rows = [runtime_graph_record(11772, 'damages', 'MyFightDamage',
        'unity/Assets/Game/Runtime/Official/BattleSkillPlayables/MyFightDamage.cs', guid_paths, code,
        prefab_entity_id=11770)]
    graph_catalog = dict(manifest=source.version, skills=skill_rows, hits=hit_rows,
        damages=damage_rows, moves=[])
    write_changed(GRAPH_DATA, json.dumps(graph_catalog, ensure_ascii=False, indent=2) + '\n')
    meta(GRAPH_DATA)
    graph_meta = GRAPH_DATA.with_name(GRAPH_DATA.stem + '-source-manifest.json')
    graph_digest = identity(GRAPH_DATA)['sha256']
    write_changed(graph_meta, json.dumps(dict(manifest=source.version,
        output_sha256=graph_digest), indent=2) + '\n')
    meta(graph_meta)
    graph_outputs = [identity(GRAPH_DATA), identity(Path(str(GRAPH_DATA) + '.meta')),
        identity(graph_meta), identity(Path(str(graph_meta) + '.meta'))]
    graph_record = dict(manifest=source.version, official_rules=rule_proof,
        native_export=identity(cache / 'native-export-source-manifest.json'),
        conversion_inputs=identity(cache / 'source-manifest.json'),
        textures=identity(cache / 'all-assets/windows-textures/source-manifest.json'),
        shaders=identity(cache / 'all-assets/windows-shaders/source-manifest.json'),
        selected_entities=[25064, 11770, 11771, 11772], outputs=graph_outputs,
        generated_by=identity(Path(__file__).resolve()),
        operation='Restore current official native entity hierarchies and prefab bindings for the fatal listener, cure hit, dispersal damage, and authored recovery playable.')
    write_changed(GRAPH_RECEIPT, json.dumps(graph_record, ensure_ascii=False, indent=2) + '\n')
    print('Potential Rebirth original status and recovery graph assets restored.')


if __name__ == '__main__':
    build()
