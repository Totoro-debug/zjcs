"""Restore complete grid/status prefabs and their original managed view registry."""
import hashlib
import json
import re
import struct
from pathlib import Path

from Crypto.Hash import MD4
from UnityPy.classes import PPtr
import yaml

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from prepare_battle_grid_entity_runtime import main as prepare_runtime
from prepare_battle_grid_item_views import ROOTS as SOURCE_ROOTS
from prepare_battle_ground_effect_assets import EXPORTED, GUID
from prepare_battle_scene_appearance_runtime import identity, write_changed
from prepare_battle_status_orbit_assets import build, SCRIPTS
from prepare_battle_cast_assets import EVENT_ASSET

RECEIPT = ROOT / 'assets/reference/battle-animation/grid-entity-assets-source-manifest.json'
DEST = ROOT / 'unity/Assets/Official/Combat/GridEntities/Resources/Combat/GridEntities'
GRID_ENTITIES = (3050, 3051, 3054)
ROOTS = {3050: 'Prefabs/EcEntity/Fight/FightGridItem/3050.prefab',
         3051: 'Prefabs/EcEntity/Fight/FightGridItem/3051.prefab',
         3054: 'Prefabs/EcEntity/Fight/FightGridItem/3054.prefab',
         **{key: 'Prefabs/EcEntity/Fight/FightSkill/'
                  + re.fullmatch(r'fightskill_(\d+)\.bundle', bundle)[1]
                  + '/' + str(key) + '.prefab'
            for key, bundle in SOURCE_ROOTS.items() if key not in GRID_ENTITIES}}


def script_id(name, namespace=''):
    return struct.unpack('<i', MD4.new(('s\0\0\0' + namespace + name).encode()).digest()[:4])[0]


def main():
    prepare_runtime()
    source_receipt = ROOT / 'assets/reference/battle-animation/grid-item-views-source-manifest.json'
    proof = json.loads(source_receipt.read_bytes())
    for row in proof['outputs']:
        assert identity(ROOT / row['path'])['sha256'] == row['sha256']
    raw = {key: json.loads((ROOT / 'unity/Assets/Official/Combat/GridItemSources' /
                           (str(key) + '-source.json')).read_bytes()) for key in ROOTS}
    assert all(value['manifest'] == '884_156692' for value in raw.values())
    external = {}
    for original, target in [
        ('Prefabs/EcEntity/Fight/FightSkill/13050/playable13052.prefab',
         'unity/Assets/Official/Combat/StatusBurns/Resources/Combat/StatusBurns/Prefabs/EcEntity/Fight/FightSkill/13050/playable13052.prefab'),
        ('Prefabs/EcEntity/Fight/FightSkill/13480/playable13482.prefab',
         'unity/Assets/Official/Combat/StatusBurns/Resources/Combat/StatusBurns/Prefabs/EcEntity/Fight/FightSkill/13480/playable13482.prefab')]:
        guid = GUID.search(Path(str(EXPORTED / original) + '.meta').read_text())[1]
        assert (ROOT / target).is_file(), target
        external[guid] = dict(source=original, target=target)
    restored, registrations, registry_objects, field_restorations = {}, [], [], []
    with Apk.current() as apk:
        schemas = ScriptSchemas(code_root=ROOT / '.tools/source-packages/current/884_156692/official-code')
        for key in GRID_ENTITIES:
            driver = next(c for node in raw[key]['nodes'] for c in node['components']
                          if c.get('script', {}).get('name') == 'ECViewDriver')
            component = next(obj for obj in apk.objects(driver['source']['bundle'])
                             if str(obj.path_id) == driver['source']['path_id'])
            assert hashlib.sha256(component.get_raw_data()).hexdigest() == driver['payload_sha256']
            fields = schemas.read(component)
            assert fields == driver['fields']
            refs = fields['references']['RefIds']
            assert [row['type']['class'] for row in refs] == ['ECFightRoleCharacterView', 'ECFightRoleStatusDisplayView']

            def convert(value):
                if isinstance(value, dict) and set(value) == {'m_FileID', 'm_PathID'}:
                    if not value['m_PathID']:
                        return dict(fileID=0)
                    target = PPtr(**value, assetsfile=component.assets_file).deref()
                    assert target.type.name == 'GameObject'
                    source = apk.provenance(target)
                    skill = re.fullmatch(r'defaultpackage_assets_prefabs_ecentity_fight_fightskill_(\d+)\.bundle', source['bundle'])
                    assert skill, source
                    native = EXPORTED / 'Prefabs/EcEntity/Fight/FightSkill' / skill[1] / (target.read().m_Name + '.prefab')
                    sidecar = Path(str(native) + '.meta')
                    native_meta = sidecar.read_text()
                    assert re.search(r'(?m)^  assetBundleName: (.+)$', native_meta)[1] + '.bundle' == source['bundle']
                    guid = GUID.search(native_meta)[1]
                    file_id = int(re.search(r'(?m)^--- !u!1 &(-?\d+)$', native.read_text())[1])
                    registry_objects.append(dict(source=source, object_sha256=hashlib.sha256(target.get_raw_data()).hexdigest(),
                        native=identity(native), native_meta=identity(sidecar), guid=guid, file_id=file_id))
                    return dict(fileID=file_id, guid=guid, type=3)
                if isinstance(value, dict): return {k: convert(v) for k, v in value.items()}
                if isinstance(value, list): return [convert(v) for v in value]
                return value

            registry = convert(fields['references'])
            for row in registry['RefIds']:
                row['type']['ns'] = 'Zjcs.Official.BattleGridItemEntities'
                row['type']['asm'] = 'Zjcs.Official.BattleGridItemEntities'
            restored[key] = yaml.safe_dump(dict(mECViews=fields['mECViews'], references=registry),
                                            sort_keys=False, allow_unicode=True, width=160)
            registrations.append(dict(entity_id=key, original=driver,
                                      conversion='Original order, RIDs and data; each nonnull native source pointer resolved through its actual source bundle and object identity.'))

    def restore_native_fields(text, path):
        key = next((key for key in restored if path == EXPORTED / ROOTS[key]), None)
        if key is None: return text
        blocks = re.split(r'(?m)(?=^--- !u!)', text)
        count, attach_count = 0, 0
        attach = next(c for node in raw[key]['nodes'] for c in node['components']
                      if c.get('script', {}).get('name') == 'AttachAgent')
        names = attach['fields']['myBoneNameList']
        assert names == ['', '3300'] and len(attach['fields']['myBoneList']) == 2
        for index, block in enumerate(blocks):
            if re.search(r'm_Script: \{fileID: ' + str(script_id('ECViewDriver')) + r',', block):
                assert 'mECViews:' not in block and 'references:' not in block
                blocks[index] = block.rstrip() + '\n' + ''.join('  ' + line + '\n' for line in restored[key].splitlines())
                count += 1
            elif re.search(r'm_Script: \{fileID: ' + str(script_id('AttachAgent')) + r',', block):
                # The exporter emits the empty string as a bare list item.
                # Quote both original strings, including the numeric bone name.
                values = ''.join('  ' + line + '\n' for line in yaml.safe_dump(names).splitlines())
                blocks[index], replaced = re.subn(r'(?m)^  myBoneNameList:\n(?:  -[^\n]*\n)+',
                                                  '  myBoneNameList:\n' + values, block)
                assert replaced == 1, (key, replaced)
                attach_count += 1
        assert count == 1 and attach_count == 1, (key, count, attach_count)
        field_restorations.append(dict(entity_id=key, component=attach,
            fields=['myBoneNameList'], conversion='Original empty and numeric bone names serialized explicitly as strings.'))
        return ''.join(blocks)

    folders = {name: 'BattleGridItemEntities' for name in (
        'GameCharacterZSet', 'MyActionAgent', 'MyFightRoleGridItem', 'MyFightRoleCharacter', 'MyFightRoleGrid',
        'MyFightRound', 'MyFightProp', 'MyFightGridMover', 'MyFightGameplayTag',
        'MyFightStatusMoveNear', 'MyStatusEnd', 'MyStatusAutoRemove', 'MyFightStatusItemTrigger')}
    folders.update(MyEntity='EntityLifecycle', MyTransform='EntityLifecycle',
                   MyGridTransform='EntityLifecycle', AttachAgent='StoryAttachment',
                   MyAction='EntityActions',
                   MyFightStatus='BattleStatusPlayables', MyFightStatusAgent='BattleStatusPlayables',
                   ECViewDriver='EntityViews')
    bindings = {}
    for name, folder in folders.items():
        path = ROOT / 'unity/Assets/Game/Runtime/Official' / folder / (name + '.cs')
        assert path.is_file(), path
        namespace = re.search(r'(?m)^namespace ([\w.]+)', path.read_text(encoding='utf-8-sig'))[1]
        bindings[script_id(name)] = dict(name=name, source=name + '.cs', namespace='',
            target_namespace=namespace, runtime=path.relative_to(ROOT).as_posix())
    mapped = dict(SCRIPTS)
    mapped[script_id('MyPlayableAttachToBone')] = ('StoryAttachment', 'MyPlayableAttachToBone')
    mapped.update({1640720687: ('StorySounds', 'MyPlayableSound'),
                   -638476292: ('BattleGridItemPlayables', 'AutoStopOnSourceDie')})
    build(DEST=DEST, DATA=ROOT / 'unity/Assets/Game/Resources/Combat/grid-entities.json',
          RECEIPT=RECEIPT, ROOTS={}, graph_roots=ROOTS, family='GridEntities',
          shader_prefix='GridEntity_', MAPPED=mapped, source_scripts=bindings,
          external_assets=external, native_preprocess=restore_native_fields,
          reference_scripts={-210918009: dict(name='WwiseEventReference', native_asset=EVENT_ASSET,
              runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
              receipt='assets/reference/profession-ui/audio-source-manifest.json')},
          native_scripts=[('unity/Assets/Game/Runtime/Official/BattleGridItemEntities/' + name + '.cs')
                          for name in ('ECFightRoleCharacterView', 'ECFightRoleStatusDisplayView')],
          generator=Path(__file__), limits=[
              'Complete native grid/status prefab inputs and original managed view registry; production binding and actual public battle/Player remain required.',
              'HUD/appearance dependencies in the selected original grid views are null; no replacement UI is generated.'])
    result = json.loads(RECEIPT.read_bytes())
    result['managed_view_restoration'] = registrations
    result['managed_view_object_bindings'] = registry_objects
    result['native_component_field_restoration'] = field_restorations
    result['source_view_inputs'] = identity(source_receipt)
    result['component_runtime'] = identity(ROOT / 'assets/reference/battle-animation/grid-entity-runtime-source-manifest.json')
    write_changed(RECEIPT, json.dumps(result, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    main()
