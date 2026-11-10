"""Recover selected daily-result chest data for the shared independent Canvas backend."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import PartnerNativeRecovery, pointer, identity
from prepare_native_character_geometry import atlas_regions
from prepare_partner_native_battle import package
from convert_official_skeletons import reader_type
from prepare_daily_dungeon_result_ui import EXPORT, RESOURCES, write_folder_tree, meta_guid
from prepare_daily_dungeon_result_bindings import write_meta
from prepare_current_partner_native import documents


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def build():
    recovery = PartnerNativeRecovery()
    apk = recovery.apk
    table = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
    outputs, sources, rows = [], {}, []
    destination = RESOURCES / 'Chests'
    write_folder_tree(destination)

    def record(obj):
        sources[identity(obj)] = dict(source=apk.provenance(obj), sha256=digest(obj.get_raw_data()))
        return obj

    def locate(address):
        entry, = [row for row in table if row['path'] == address]
        return record(apk.asset(entry['bundle'], entry['path']))

    def text(obj):
        return record(obj).read(check_read=False).m_Script.encode('utf8', 'surrogateescape')

    def save(path, raw, importer):
        path.write_bytes(raw)
        meta = write_meta(path, importer)
        outputs.extend((path, meta))
        return meta_guid(meta)

    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativeUiSkeletonAsset.cs.meta'
    reader_class, _ = reader_type()
    try:
        for chest in range(1, 5):
            name = 'map_baoXiang_0' + str(chest)
            address = 'Assets/AppearanceAssets/Map/' + name + '/' + name + '_SkeletonData.asset'
            asset = locate(address)
            fields = recovery.fields(asset)
            binary = text(pointer(fields['skeletonJSON'], asset))
            reader = reader_class()
            data = reader.convert(binary)
            if reader.inp.index != len(binary):
                raise ValueError('Unconsumed original chest skeleton: ' + name)
            atlas, = fields['atlasAssets']
            atlas = record(pointer(atlas, asset))
            atlas_fields = recovery.fields(atlas)
            regions = atlas_regions(text(pointer(atlas_fields['atlasFile'], atlas)).decode('utf8'))
            materials = [record(pointer(value, atlas)) for value in atlas_fields['materials']]
            pages = []
            for page in dict.fromkeys(value['page'] for value in regions):
                textures = [record(env.m_Texture.deref()) for material in materials
                    for prop, env in material.read().m_SavedProperties.m_TexEnvs
                    if prop == '_MainTex' and env.m_Texture.path_id]
                texture, = [obj for obj in textures if obj.read().m_Name == page]
                native = EXPORT / address.removeprefix('Assets/').rsplit('/', 1)[0] / (page + '.png')
                from PIL import Image
                import io
                pixels = texture.read().image
                encoded = io.BytesIO()
                pixels.save(encoded, format='PNG')
                target = destination / (page + '.png')
                target.write_bytes(encoded.getvalue())
                meta = Path(str(target) + '.meta')
                texture_guid = digest(('zjcs-daily-chest-texture:' + page).encode())[:32]
                meta.write_text(re.sub(r'^guid: \w+$', 'guid: ' + texture_guid,
                    Path(str(native) + '.meta').read_text('utf8'), flags=re.M), 'utf8', newline='\n')
                outputs.extend((target, meta))
                pages.append('  - Name: ' + page + '\n    Placeholder: {fileID: 2800000, guid: ' + texture_guid + ', type: 3}')
            payload = b'ZJCSPNUI' + package(0, fields['scale'],
                json.dumps(data, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf8'),
                json.dumps(regions, separators=(',', ':'), allow_nan=False).encode('utf8'))[8:]
            payload_guid = save(destination / (name + '.bytes'), payload, 'TextScriptImporter')
            asset_text = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
                '  m_ObjectHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ' + meta_guid(script) + ', type: 3}\n'
                '  m_Name: ' + name + '_SkeletonData\n  payload: {fileID: 4900000, guid: ' + payload_guid + ', type: 3}\n'
                '  npcId: 0\n  combineSkins: []\n  defaultMix: ' + str(fields['defaultMix']) + '\n  pages:\n'
                + '\n'.join(pages) + '\n  splitAnimationNames: []\n')
            asset_guid = save(destination / (name + '.asset'), asset_text.encode('utf8'), 'NativeFormatImporter')
            rows.append(dict(chestId=chest, resource='Dungeons/UI/884_156692/Native/Chests/' + name,
                source=address, guid=asset_guid, animations=list(data['animations'])))

        prefab = RESOURCES / 'Prefabs/UI/DailyDungeon/DailyDungeonEndPanelItem.prefab'
        original = EXPORT / 'Prefabs/UI/DailyDungeon/DailyDungeonEndPanelItem.prefab'
        docs = documents(original.read_text('utf8'))
        graphic_id = 114571718729274231
        go_id = docs[graphic_id]['MonoBehaviour']['m_GameObject']['fileID']
        actor_id = 114999999000000001
        graphic_guid = meta_guid(ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeUiGraphic.cs.meta')
        actor_guid = meta_guid(ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativeUiActor.cs.meta')
        sequence_guid = meta_guid(ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Detail/SkeletonGraphicAnimSequence.cs.meta')
        content = prefab.read_text('utf8')
        pattern = r'(--- !u!114 &' + str(graphic_id) + r'\n.*?)(?=--- !u!|\Z)'
        def replace_graphic(match):
            value = re.sub(r'm_Script: \{[^\n]+\}', 'm_Script: {fileID: 11500000, guid: ' + graphic_guid + ', type: 3}', match[0])
            return re.sub(r'skeletonDataAsset: \{[^\n]+\}', 'skeletonDataAsset: {fileID: 0}', value)
        content, count = re.subn(pattern, replace_graphic, content, flags=re.S)
        if count != 1: raise ValueError('Original result chest graphic was not found')
        sequence_id = 114581786255902400
        content = re.sub(r'(--- !u!114 &' + str(sequence_id) + r'\n.*?)m_Script: \{[^\n]+\}',
            lambda m: m[1] + 'm_Script: {fileID: 11500000, guid: ' + sequence_guid + ', type: 3}', content, flags=re.S)
        transform_id = docs[go_id]['GameObject']['m_Component'][0]['component']['fileID']
        content = content.replace('  - component: {fileID: ' + str(actor_id) + '}\n', '')
        content = re.sub(r'(--- !u!1 &' + str(go_id) + r'\n.*?m_Component:\n  - component: \{fileID: ' + str(transform_id) + r'\}\n)',
            lambda m: m[1] + '  - component: {fileID: ' + str(actor_id) + '}\n', content, flags=re.S)
        if not re.search(r'--- !u!114 &' + str(actor_id) + r'\n', content):
            content += ('--- !u!114 &' + str(actor_id) + '\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n'
                '  m_GameObject: {fileID: ' + str(go_id) + '}\n  m_Enabled: 1\n'
                '  m_Script: {fileID: 11500000, guid: ' + actor_guid + ', type: 3}\n'
                '  graphic: {fileID: ' + str(graphic_id) + '}\n  renderRoot: {fileID: ' + str(transform_id) + '}\n')
        auto_guid = meta_guid(ROOT / 'unity/Assets/Game/Runtime/Presentation/Home/DailyDungeonAwardAutoScroll.cs.meta')
        scroll_guid = meta_guid(ROOT / 'unity/Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/ScrollRect.cs.meta')
        for source_id, target_guid in ((-1149036886, auto_guid), (1367256648, scroll_guid)):
            content = re.sub(r'm_Script: \{fileID: ' + str(source_id) + r', guid: [a-f0-9]+, type: 3\}',
                'm_Script: {fileID: 11500000, guid: ' + target_guid + ', type: 3}', content)
        prefab.write_text(content, 'utf8', newline='\n')
        outputs.append(prefab)
        settings_obj = locate('Assets/ScriptableConfig/Modules/DailyDungeon/DailyDungeonSettings.asset')
        settings = recovery.fields(settings_obj)
        config = dict(manifest=apk.version, chests=rows, grades=[dict(grade=row['GradeType'], chestId=row['ChestId'])
            for row in settings['GradeDatas']])
        from prepare_dungeon_ui_bindings import relative_transform_path
        from current_component_schemas import CurrentSchemas
        schemas = CurrentSchemas()
        root = locate('Assets/Prefabs/UI/DailyDungeon/DailyDungeonEndPanelItem.prefab')
        nodes = list(relative_transform_path(root.read()))
        component, = [record(pair.component.deref()) for pair in root.read().m_Component
            if pair.component.deref().type.name == 'MonoBehaviour'
            and pair.component.deref().read(check_read=False).m_Script.read().m_ClassName == 'DailyDungeonEndPanelItem']
        fields = schemas.read(component)
        def node_path(ref):
            if not ref['m_PathID']: return None
            obj = pointer(ref, component)
            go = obj.path_id if obj.type.name == 'GameObject' else obj.read(check_read=False).m_GameObject.path_id
            return next('/'.join(map(str, indices)) for item, indices, _, _ in nodes if item.object_reader.path_id == go)
        config['row'] = dict(awardRoot=node_path(fields['mAwardSlotRoot']),
            showInterval=fields['mAwardShowInterval'], scrollMinCount=fields['mAwardScrollMinCount'],
            defaultSlotScale=fields['mDefaultAwardSlotScale'])
        save(destination / 'catalog.json', (json.dumps(config, ensure_ascii=False, indent=2) + '\n').encode('utf8'), 'TextScriptImporter')
        receipt = dict(manifest=apk.version, operation='APK-first original skeleton binary, atlas regions, pixels, grade selection and sequence; shared independent native Canvas backend.',
            inputs=sources, generator=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=digest(Path(__file__).read_bytes())),
            outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=digest(p.read_bytes())) for p in outputs])
        target = ROOT / 'assets/reference/dungeon-ui/884_156692/daily-dungeon-chest-native-source-manifest.json'
        target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Restored four original daily dungeon chests.')
    finally:
        apk.close()


if __name__ == '__main__':
    build()
