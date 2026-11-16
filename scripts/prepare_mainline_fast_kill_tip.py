"""Restore the selected official fast-kill tip, preserving native fields/curves."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from prepare_unity_map13_prop_parts import dump, meta
from prepare_unity_map13_story_runtime import source_record, write_json

DEST = ROOT / 'unity/Assets/Official/World/Mainline/FastKillTip'
RECEIPT = ROOT / 'assets/reference/official-map/mainline-fast-kill-tip-source-manifest.json'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Presentation/World/Mainline'


def guid(path):
    return re.search(r'^guid: (\w+)', Path(str(path) + '.meta').read_text(), re.M)[1]


def build():
    apk = Apk.current()
    schemas = ScriptSchemas(code_root=ROOT / '.tools/source-packages/current' / apk.version / 'official-code')
    root = apk.asset('defaultpackage_assets_prefabs_map_common_entityui.bundle', '/StageFastKillTip.prefab')
    objects = {}
    def visit(go):
        objects[go.path_id] = go
        for c in go.read().m_Component:
            obj = c.component.deref()
            objects[obj.path_id] = obj
            if obj.type.name == 'Transform':
                for child in obj.read().m_Children:
                    visit(child.read().m_GameObject.deref())
    visit(root)
    clips = {obj.path_id: obj for obj in apk.objects('defaultpackage_assets_animation_ui_mapradartipfade.bundle')
             if obj.type.name == 'AnimationClip' and obj.read().m_Name.startswith('StageFastKillTip')}
    if len(objects) != 7 or len(clips) != 2:
        raise ValueError('Original fast-kill hierarchy changed')
    DEST.mkdir(parents=True, exist_ok=True)
    output = []
    clip_refs = {}
    # Unity's editable legacy clip fields are recovered from the original binary.
    # The unconsumed muscle-stream block is not exported by Unity for legacy clips.
    template_path = ROOT / 'unity/Assets/Official/World/Map/14/NativeRadarTips/Animation/UI/MapRadarTipFade/StageCostTipFadeIn.anim'
    from official_native_yaml_verification import documents
    template = next(iter(documents(template_path.read_text()).values()))[1]
    for pid, obj in clips.items():
        raw = obj.read_typetree()
        body = json.loads(json.dumps(template))
        for key in ('m_Name', 'm_Legacy', 'm_Compressed', 'm_UseHighQualityCurve', 'm_RotationCurves',
                    'm_CompressedRotationCurves', 'm_EulerCurves', 'm_PositionCurves', 'm_ScaleCurves',
                    'm_FloatCurves', 'm_PPtrCurves', 'm_SampleRate', 'm_WrapMode', 'm_Bounds',
                    'm_ClipBindingConstant', 'm_HasGenericRootTransform', 'm_HasMotionFloatCurves', 'm_Events'):
            body[key] = raw[key]
        def fix(value):
            if isinstance(value, dict):
                if set(value) == {'m_FileID', 'm_PathID'}:
                    if value['m_PathID']: raise ValueError('Unexpected clip dependency')
                    return {'fileID': 0}
                return {k: fix(v) for k, v in value.items()}
            if isinstance(value, list): return [fix(v) for v in value]
            return value
        body = fix(body)
        body['m_AnimationClipSettings']['m_StopTime'] = max(
            frame['time'] for curve in body['m_FloatCurves'] for frame in curve['curve']['m_Curve'])
        for curve in body['m_FloatCurves']:
            curve['serializedVersion'] = 2
            curve['curve']['serializedVersion'] = 2
            for frame in curve['curve']['m_Curve']:
                frame['serializedVersion'] = 3
                frame['tangentMode'] = 0
        path = DEST / (raw['m_Name'] + '.anim')
        path.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!74 &7400000\n' + dump({'AnimationClip': body}), encoding='utf8', newline='\n')
        meta(path, 'NativeFormatImporter')
        clip_refs[pid] = {'fileID': 7400000, 'guid': guid(path), 'type': 2}
        output += [path, Path(str(path) + '.meta')]
    material_receipt = ROOT / 'assets/reference/official-map/map14-native-entity-materials-native-source-manifest.json'
    rows = json.loads(material_receipt.read_bytes())['materials']
    materials = {(r['cab'].lower(), int(r['path_id'])): r for r in rows}
    sprite_proof = ROOT / 'assets/reference/official-map/map14-native-entity-sprites-source-manifest.json'
    for row in json.loads(sprite_proof.read_bytes())['default_materials']:
        cab, pid = row['identity'].split(':')
        materials[cab, int(pid)] = dict(payload_sha256=row['original_payload_sha256'], output=dict(path=row['output']))
    script = RUNTIME.parent.parent.parent / 'Official/EntityRadarFastKill/MapControllerEntityRadarTipFastKill.cs'
    binder = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14HeroStatues/NativeMap14HeroStatueSprites.cs'
    sprite_dependencies = []
    def remap(value, owner, field=''):
        if isinstance(value, dict):
            if set(value) == {'m_FileID', 'm_PathID'}:
                pid = value['m_PathID']
                if not pid: return {'fileID': 0}
                if not value['m_FileID'] and pid in objects: return {'fileID': pid}
                from UnityPy.classes import PPtr
                target = PPtr(m_FileID=value['m_FileID'], m_PathID=pid, assetsfile=owner.assets_file).deref()
                if field == 'm_Script': return {'fileID': 11500000, 'guid': guid(script), 'type': 3}
                if target.type.name == 'AnimationClip': return clip_refs[pid]
                if target.type.name == 'Sprite':
                    sprite_dependencies.append(target)
                    return {'fileID': 0}
                if target.type.name == 'Material':
                    row = materials[target.assets_file.name.lower(), pid]
                    if hashlib.sha256(target.get_raw_data()).hexdigest() != row['payload_sha256']:
                        raise ValueError('Selected original material differs from native material source')
                    return {'fileID': 2100000, 'guid': guid(ROOT / row['output']['path']), 'type': 2}
                raise ValueError('Unexpected original dependency: ' + field)
            return {k: remap(v, owner, k) for k, v in value.items()}
        if isinstance(value, list): return [remap(v, owner, field) for v in value]
        return value
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
    renderer = None
    binder_id = 114001
    for pid, obj in objects.items():
        kind = obj.type.name
        raw = schemas.read(obj) if kind == 'MonoBehaviour' else obj.read_typetree()
        body = remap(raw, obj)
        for name in ('m_CorrespondingSourceObject', 'm_PrefabInstance', 'm_PrefabAsset'):
            body[name] = {'fileID': 0}
        if kind == 'GameObject': body['serializedVersion'] = 6
        if kind == 'Transform': body['serializedVersion'] = 2
        if kind == 'Animation': body['serializedVersion'] = 3
        if kind == 'MonoBehaviour': body['m_EditorClassIdentifier'] = ''
        if kind == 'SpriteRenderer': renderer = obj
        if pid == root.path_id: body['m_Component'].append({'component': {'fileID': binder_id}})
        text += '--- !u!' + str(obj.type.value) + ' &' + str(pid) + '\n' + dump({kind: body})
    text += '--- !u!114 &' + str(binder_id) + '\n' + dump({'MonoBehaviour': {
        'm_ObjectHideFlags': 0, 'm_CorrespondingSourceObject': {'fileID': 0}, 'm_PrefabInstance': {'fileID': 0},
        'm_PrefabAsset': {'fileID': 0}, 'm_GameObject': {'fileID': root.path_id}, 'm_Enabled': 1,
        'm_EditorHideFlags': 0, 'm_Script': {'fileID': 11500000, 'guid': guid(binder), 'type': 3},
        'm_Name': '', 'm_EditorClassIdentifier': '', 'bindings': [{'target': {'fileID': renderer.path_id},
            'sourceKey': renderer.assets_file.name.lower() + ':' + str(renderer.path_id)}]}})
    path = DEST / 'StageFastKillTip.prefab'
    path.write_text(text, encoding='utf8', newline='\n'); meta(path, 'PrefabImporter')
    output += [path, Path(str(path) + '.meta')]
    fast_path = path
    combined = apk.asset('defaultpackage_assets_prefabs_map_common_entityui.bundle', '/StageCombinate.prefab')
    fast_objects = objects
    objects = {}
    visit(combined)
    combined_objects = list(objects.values())
    combined_script = RUNTIME.parent.parent.parent / 'Official/EntityRadarCombined/MapControllerEntityRadarTipCombinate.cs'
    cost_path = ROOT / 'unity/Assets/Official/World/Map/14/NativeRadarTips/Prefabs/StageCostTip.prefab'
    combined_text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
    def combined_remap(value, field=''):
        if isinstance(value, dict):
            if set(value) == {'m_FileID', 'm_PathID'}:
                pid = value['m_PathID']
                if not pid: return {'fileID': 0}
                if field == 'm_Script': return {'fileID': 11500000, 'guid': guid(combined_script), 'type': 3}
                if pid == 5603993812614540572:
                    return {'fileID': 114884547391421755, 'guid': guid(cost_path), 'type': 3}
                if pid == 4522477724878928372:
                    return {'fileID': pid, 'guid': guid(fast_path), 'type': 3}
                if not value['m_FileID'] and pid in objects: return {'fileID': pid}
                raise ValueError('Unexpected combined dependency')
            return {k: combined_remap(v, k) for k, v in value.items()}
        if isinstance(value, list): return [combined_remap(v, field) for v in value]
        return value
    for pid, obj in objects.items():
        kind = obj.type.name
        body = combined_remap(schemas.read(obj) if kind == 'MonoBehaviour' else obj.read_typetree())
        for name in ('m_CorrespondingSourceObject', 'm_PrefabInstance', 'm_PrefabAsset'):
            body[name] = {'fileID': 0}
        if kind == 'GameObject': body['serializedVersion'] = 6
        if kind == 'Transform': body['serializedVersion'] = 2
        if kind == 'MonoBehaviour': body['m_EditorClassIdentifier'] = ''
        combined_text += '--- !u!' + str(obj.type.value) + ' &' + str(pid) + '\n' + dump({kind: body})
    path = DEST / 'StageCombinate.prefab'
    path.write_text(combined_text, encoding='utf8', newline='\n'); meta(path, 'PrefabImporter')
    output += [path, Path(str(path) + '.meta')]
    # Retain each monster display's exact effect-slot ancestor transforms.
    slots = []
    slot_inputs = []
    for asset_path, pointer in apk.container('defaultpackage_assets_prefabs_map_common_entitydisplaysworldmonster.bundle'):
        if not asset_path.endswith('_view.prefab'): continue
        display = pointer.deref()
        if display.type.name != 'GameObject': continue
        for component in display.read().m_Component:
            driver = component.component.deref()
            if driver.type.name != 'MonoBehaviour': continue
            data = schemas.read(driver)
            for ref in data.get('references', {}).get('RefIds', []):
                if ref['type']['class'] != 'ECMapControllerEntityRadarSupportView': continue
                radar = ref['data']
                if radar['mRadarTipPrefab']['m_PathID'] != -8295975784239242397: continue
                from UnityPy.classes import PPtr
                chain = []
                transform = PPtr(**radar['mRadarEffectSlot'], assetsfile=driver.assets_file).deref() if radar['mRadarEffectSlot']['m_PathID'] else None
                while transform is not None:
                    raw = transform.read_typetree()
                    chain.insert(0, dict(Position=raw['m_LocalPosition'], Rotation=raw['m_LocalRotation'], Scale=raw['m_LocalScale']))
                    slot_inputs.append(transform)
                    if raw['m_Father']['m_PathID'] == 0: break
                    transform = PPtr(**raw['m_Father'], assetsfile=transform.assets_file).deref()
                slots.append(dict(ModelName=Path(asset_path).stem.removesuffix('_view'), Transforms=chain))
                slot_inputs.append(driver)
    catalog_script = RUNTIME / 'OfficialMainlineFastKillTipCatalog.cs'
    meta(catalog_script, 'MonoImporter')
    catalog = ROOT / 'unity/Assets/Game/Resources/World/Mainline/fast-kill-tip.asset'
    catalog.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' + dump({'MonoBehaviour': {
        'm_ObjectHideFlags': 0, 'm_Script': {'fileID': 11500000, 'guid': guid(catalog_script), 'type': 3},
        'm_Name': 'Official mainline fast-kill tip', 'Manifest': apk.version,
        'Prefab': {'fileID': combined.path_id, 'guid': guid(path), 'type': 3},
        'FastKillPrefab': {'fileID': root.path_id, 'guid': guid(fast_path), 'type': 3}, 'Slots': slots}}), encoding='utf8', newline='\n')
    meta(catalog, 'NativeFormatImporter'); output += [catalog, Path(str(catalog) + '.meta')]
    for folder in (DEST, DEST.parent, DEST.parent.parent):
        meta(folder, folder=True); output.append(Path(str(folder) + '.meta'))
    sprite_proof = ROOT / 'assets/reference/official-map/map14-native-entity-sprites-source-manifest.json'
    original_graph = json.loads((ROOT / 'unity/Assets/Official/World/Map/14/Source/native-entity-object-graph.json').read_bytes())
    sprite = sprite_dependencies[0]
    expected, = [r for r in original_graph['objects'] if r['cab'].lower() == sprite.assets_file.name.lower() and int(r['path_id']) == sprite.path_id]
    if hashlib.sha256(sprite.get_raw_data()).hexdigest() != expected['payload_sha256']:
        raise ValueError('Selected original sprite differs from packed Windows sprite source')
    inputs = [dict(apk.provenance(o), payload_sha256=hashlib.sha256(o.get_raw_data()).hexdigest())
              for o in list(fast_objects.values()) + combined_objects + list(clips.values()) + sprite_dependencies + slot_inputs]
    write_json(RECEIPT, dict(format=1, manifest=apk.version, inputs=inputs,
        dependencies=[source_record(p) for p in (material_receipt, sprite_proof, script, combined_script, cost_path, binder, template_path, catalog_script)],
        tools=[source_record(Path(__file__))], outputs=[source_record(p) for p in output],
        operation='Preserve the original fast-kill and combined-tip GameObjects/components, transforms, renderer, animation clip curves and hide event. Restore each monster display radar effect-slot ancestor chain. Bind migrated official scripts, native material, existing original cost-tip prefab and byte-identical packed original sprite. Add explicit sprite loading metadata.'))
    print('Original fast-kill prefab and two native legacy clips restored')


if __name__ == '__main__': build()
