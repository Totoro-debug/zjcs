"""Restore both complete original tree entity prefabs around verified Models."""
from pathlib import Path
from collections import Counter
import argparse
import json
import re
import struct

from official_apk import ROOT
from official_native_yaml_verification import documents
from prepare_unity_map13_prop_parts import meta, ref, dump
from prepare_unity_map13_prop_hierarchies import key, sha, write, encode
from prepare_unity_story_attachment_runtime import record

SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source'
DEST = ROOT / 'unity/Assets/Game/Resources/Map13/Native/TreeEntities'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap13TreeEntities'
PROOF = ROOT / 'assets/reference/official-map/map13-native-tree-entities-source-manifest.json'
NATIVE = dict(GameObject=1, Transform=4, MeshRenderer=23, MeshFilter=33,
              SpriteRenderer=212, CanvasRenderer=222)
TYPE_MAP = {
    'ECWorldStageView': ('TreeWorldStageView', 'Zjcs.Offline.World.NativeMap13TreeEntities'),
    'ECWorldStageCfgNormalProvider': ('TreeWorldStageConfigProvider', 'Zjcs.Offline.World.NativeMap13TreeEntities'),
    'ECPlayableWorldStageHandler': ('TreeWorldStagePlayableHandler', 'Zjcs.Offline.World.NativeMap13TreeEntities'),
    'ECPlayableSupporterView': ('TreePlayableSupporterView', 'Zjcs.Offline.World.NativeMap13TreeEntities'),
    'StateGameObjectConfig': ('WorldStateGameObjectConfig', 'Zjcs.Official.WorldUI'),
    'ECClientVisibilityView': ('ECClientVisibilityView', 'Zjcs.Official.World.Map13EntityVisibilityViews'),
    'ECEntityInteractionShowView': ('ECEntityInteractionShowView', 'Zjcs.Official.World.Map13EntityVisibilityViews'),
    'ECGridTransformView': ('ECGridTransformView', 'Zjcs.Official.World.Map13EntityGridViews'),
    'ECMapAwardItemCountSwitcherView': ('ECMapAwardItemCountSwitcherView', 'Zjcs.Official.World.Map13EntityStateViews'),
    'ECAnimatorView': ('ECAnimatorView', 'Zjcs.Official.World.Map13EntityStateViews'),
    'ECAttachAgentView': ('ECAttachAgentView', 'Zjcs.Official.World.Map13EntityStateViews'),
    'EcMapEntityFadeOutDelayDestoryHandler': ('EcMapEntityFadeOutDelayDestoryHandler', 'Zjcs.Official.World.Map13EntityFadeHandlers'),
}


def plain(value):
    if isinstance(value, dict): return {str(k): plain(v) for k, v in value.items()}
    if isinstance(value, list): return [plain(v) for v in value]
    if isinstance(value, bool): return value
    if isinstance(value, int): return int(value)
    if isinstance(value, float): return float(value)
    return value


def build(conversion):
    conversion = conversion.resolve()
    if not conversion.is_relative_to((ROOT / 'output/validation').resolve()):
        raise ValueError('Conversion must belong to the owned validation workspace')
    hierarchy_path = SOURCE / 'native-prop-hierarchies.json'
    anchors_path = SOURCE / 'native-attachment-followers.json'
    models_path = PROOF.with_name('map13-native-tree-models-source-manifest.json')
    hierarchy = json.loads(hierarchy_path.read_bytes())
    anchors = json.loads(anchors_path.read_bytes())
    models = json.loads(models_path.read_bytes())
    for entry in models['outputs']:
        if sha((ROOT / entry['path']).read_bytes()) != entry['sha256']:
            raise ValueError('Verified original Model dependency changed: ' + entry['path'])
    records = {r['key']: r for r in hierarchy['objects']}
    by_go = {r['key']: r for r in anchors['nodes']}
    by_tf = {r['transformKey']: r for r in anchors['nodes']}
    materials_path = SOURCE / 'native-entity-material-bindings.json'
    materials = {key(r['cab'], r['path_id']): ROOT / r['output']['path']
                 for r in json.loads(materials_path.read_bytes())['entries']}
    sliced_path = SOURCE / 'native-entity-sliced-renderers.json'
    sliced = {r['key']: r for r in json.loads(sliced_path.read_bytes())['renderers']}
    performances_path = PROOF.with_name('map13-native-tree-playable-prefabs-source-manifest.json')
    performances = {r['root']: (ROOT / r['path'], next(b['fileID'] for b in r['bindings'] if b['source'] == r['root']))
                    for r in json.loads(performances_path.read_bytes())['prefabs']}
    scripts = {}
    for name, folder in [('ECViewDriver', 'Official/EntityViews'),
                         ('SlicedFilledRender', 'Official/World/Map13EntityRendering'),
                         ('NativeMap13EntitySpriteBinding', 'Offline/World/NativeMap13EntityAssets'),
                         ('NativeMap13SlicedSpriteBinding', 'Offline/World/NativeMap13EntityViews'),
                         ('NativeMap13TreeEntityBinding', 'Offline/World/NativeMap13TreeEntities')]:
        path = ROOT / 'unity/Assets/Game/Runtime' / folder / (name + '.cs')
        scripts[name] = meta(path, 'MonoImporter')
    meta(RUNTIME, folder=True)
    for path in RUNTIME.glob('*.asmdef'): meta(path, 'AssemblyDefinitionImporter')
    inputs = [hierarchy_path, anchors_path, models_path, materials_path, sliced_path, performances_path]
    outputs, entities, counts = [], [], Counter()

    def mono(go_id, script, fields=None):
        return dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=ref(), m_PrefabInstance=ref(),
                    m_PrefabAsset=ref(), m_GameObject=ref(go_id), m_Enabled=1, m_EditorHideFlags=0,
                    m_Script=ref(11500000, scripts[script]), m_Name='', m_EditorClassIdentifier='', **(fields or {}))

    for model in models['models']:
        root = by_tf[by_go[model['source']]['parentTransformKey']]
        model_ids = {identity: 100001 + i for i, identity in enumerate(model['original_keys'])}
        outside = []
        def visit(node):
            if node['key'] in model_ids: return
            go = records[node['key']]; outside.append(go)
            outside.extend(records[key(p['cab'], p['path_id'])] for p in go['references'] if p['field'].startswith('.m_Component['))
            for child in records[node['transformKey']]['references']:
                if child['field'].startswith('.m_Children['): visit(by_tf[key(child['cab'], child['path_id'])])
        visit(root)
        ids = dict(model_ids, **{r['key']: 400001 + i for i, r in enumerate(outside)})
        native_path = conversion / 'export/ExportedProject/Assets/Prefabs/Map/Common/EntityDisplaysWorldBonusTouch' / (root['name'] + '.prefab')
        native = documents(native_path.read_text(encoding='utf-8-sig'))
        exported = {}
        def edge(owner, field):
            selected = [r for r in owner['references'] if r['field'] == field]
            if len(selected) != 1: raise ValueError('Original entity reference not unique: ' + owner['key'] + field)
            return key(selected[0]['cab'], selected[0]['path_id'])
        def pair(node, tf_id):
            tf = native[tf_id][1]; go_id = tf['m_GameObject']['fileID']; go = native[go_id][1]
            if go['m_Name'] != node['name']: raise ValueError('Original tree entity child order/name differs')
            exported[node['key']] = int(go_id); exported[node['transformKey']] = int(tf_id)
            if node['key'] in model_ids: return
            original = records[node['key']]
            components = [edge(original, '.m_Component[' + str(i) + '].component') for i in range(len(original['fields']['m_Component']))]
            if len(components) != len(go['m_Component']): raise ValueError('Original tree entity component count differs')
            for source, target in zip(components, go['m_Component']):
                file_id = target['component']['fileID']; kind = native[file_id][0]
                if records[source]['type'] in NATIVE and kind != records[source]['type']:
                    raise ValueError('Original outer entity component order differs')
                exported[source] = int(file_id)
            children = [r for r in records[node['transformKey']]['references'] if r['field'].startswith('.m_Children[')]
            if len(children) != len(tf['m_Children']): raise ValueError('Original tree entity child count differs')
            for source, target in zip(children, tf['m_Children']): pair(by_tf[key(source['cab'], source['path_id'])], target['fileID'])
        native_root, = [i for i, (kind, fields) in native.items() if kind == 'Transform' and fields['m_Father']['fileID'] == 0]
        pair(root, native_root)

        def compare(expected, actual, owner, field=''):
            if isinstance(expected, dict):
                if set(expected) == {'m_FileID', 'm_PathID'}:
                    if not expected['m_PathID']:
                        if actual != {'fileID': 0}: raise ValueError('Original null entity reference differs: ' + field)
                    else:
                        identity = edge(owner, field)
                        if identity in exported:
                            if actual != {'fileID': exported[identity]}: raise ValueError('Original internal entity reference differs: ' + field)
                        elif not actual.get('guid') or not actual.get('fileID'):
                            raise ValueError('Original external entity reference absent: ' + field)
                    counts['references'] += 1; return
                if not isinstance(actual, dict) or set(expected) - set(actual):
                    raise ValueError('Original entity field omitted: ' + owner['key'] + ':' + owner['type'] + field + ' ' + str(set(expected) - set(actual)))
                for name, value in expected.items(): compare(value, actual[name], owner, field + '.' + name)
            elif isinstance(expected, list):
                if actual is None and not expected: actual = []
                if not isinstance(actual, list) or len(expected) != len(actual): raise ValueError('Original entity array differs: ' + field)
                for i, (a, b) in enumerate(zip(expected, actual)): compare(a, b, owner, field + '[' + str(i) + ']')
            elif isinstance(expected, float):
                if struct.pack('<f', expected) != struct.pack('<f', actual):
                    if expected == actual == 0 and struct.pack('<f', expected) == bytes.fromhex('00000080'):
                        # Every source field is written back below, preserving
                        # the original negative-zero bits lost by the export.
                        counts['restored_signed_zeros'] += 1
                    else: raise ValueError('Original entity Float32 differs: ' + field)
            elif expected != actual: raise ValueError('Original entity value differs: ' + field + repr((expected, actual)))
            counts['values'] += 1

        def pointer(value, owner, field=''):
            if isinstance(value, dict):
                if set(value) == {'m_FileID', 'm_PathID'}:
                    if not value['m_PathID']: return ref()
                    identity = edge(owner, field)
                    if identity in ids: return ref(ids[identity])
                    if identity in materials:
                        path = materials[identity]; inputs.extend([path, Path(str(path) + '.meta')]); return ref(2100000, meta(path), 2)
                    if identity in performances:
                        path, root_id = performances[identity]; inputs.extend([path, Path(str(path) + '.meta')]); return ref(root_id, meta(path, 'PrefabImporter'))
                    if owner['type'] in ['SpriteRenderer', 'SlicedFilledRender'] and field in ['.m_Sprite', '.mSprite']: return ref()
                    raise ValueError('Unbound original complete-tree reference: ' + owner['type'] + field + identity)
                return {k: pointer(v, owner, field + '.' + k) for k, v in value.items()}
            if isinstance(value, list): return [pointer(v, owner, field + '[' + str(i) + ']') for i, v in enumerate(value)]
            return value

        model_path = ROOT / model['path']; inputs.append(model_path)
        model_docs = documents(model_path.read_text(encoding='utf-8-sig'))
        model_transform_id = model_ids[by_go[model['source']]['transformKey']]
        model_docs[model_transform_id][1]['m_Father'] = ref(ids[root['transformKey']])
        for target in model['external_fade_targets']:
            owner = model_docs[target['Owner']['fileID']][1]
            owner['FadeInCompleteActiveGO'][target['Index']] = ref(ids[target['GameObjectKey']])
        rendered = []
        def emit(class_id, file_id, kind, fields):
            rendered.append('--- !u!' + str(class_id) + ' &' + str(file_id) + '\n' + dump({kind: plain(fields)}))
        # Model native class IDs come from its verified prefab headers.
        model_text = model_path.read_text(encoding='utf-8-sig')
        class_ids = {int(file_id): int(class_id) for class_id, file_id in re.findall(r'(?m)^--- !u!(\d+) &(-?\d+)', model_text)}
        for file_id, (kind, fields) in model_docs.items(): emit(class_ids[file_id], file_id, kind, fields)
        sprite_records = [r for r in outside if r['type'] == 'SpriteRenderer']
        slice_records = [r for r in outside if r['type'] == 'SlicedFilledRender']
        sprite_ids = {r['key']: 500001 + i for i, r in enumerate(sprite_records)}
        slice_ids = {r['key']: 500101 + i for i, r in enumerate(slice_records)}
        binding_id = 500201
        driver_id = next(ids[r['key']] for r in outside if r['type'] == 'ECViewDriver')
        for owner in outside:
            kind, fields = owner['type'], owner['fields']
            if kind in NATIVE:
                body = plain(native[exported[owner['key']]][1])
                expected = dict(fields)
                if kind == 'GameObject':
                    if expected.pop('m_Tag') != 0 or body['m_TagString'] != 'Untagged': raise ValueError('Explicit tree tag migration required')
                omitted = set(expected) - set(body)
                if kind == 'MeshRenderer' and 'm_EnlightenVertexStream' in omitted:
                    if expected['m_EnlightenVertexStream'] != dict(m_FileID=0, m_PathID=0):
                        raise ValueError('Original Enlighten vertex stream requires a native binding')
                    body['m_EnlightenVertexStream'] = ref()
                    omitted.remove('m_EnlightenVertexStream')
                if kind in ['MeshRenderer', 'SpriteRenderer'] and omitted:
                    if omitted != {'m_LightmapIndex', 'm_LightmapIndexDynamic', 'm_LightmapTilingOffset', 'm_LightmapTilingOffsetDynamic'}:
                        raise ValueError('Inspect omitted native tree renderer fields: ' + kind + ' ' + str(omitted))
                    for name in omitted:
                        if expected[name] != (65535 if 'Index' in name else dict(x=1., y=1., z=0., w=0.)):
                            raise ValueError('Original tree lightmap defaults differ')
                        body[name] = expected[name]
                compare(expected, body, owner)
                # Keep engine serialization metadata and restore every source
                # field with explicit target-platform references.
                bound = pointer(expected, owner)
                body.update(bound)
                if kind == 'GameObject':
                    for renderer_record in sprite_records:
                        if edge(renderer_record, '.m_GameObject') == owner['key']: body['m_Component'].append(dict(component=ref(sprite_ids[renderer_record['key']])))
                    for renderer_record in slice_records:
                        if edge(renderer_record, '.m_GameObject') == owner['key']: body['m_Component'].append(dict(component=ref(slice_ids[renderer_record['key']])))
                    if owner['key'] == root['key']: body['m_Component'].append(dict(component=ref(binding_id)))
                emit(NATIVE[kind], ids[owner['key']], kind, body)
            elif kind in ['ECViewDriver', 'SlicedFilledRender']:
                body = pointer({k: v for k, v in fields.items() if k not in ['m_GameObject', 'm_Enabled', 'm_Script', 'm_Name']}, owner)
                if kind == 'ECViewDriver':
                    for item in body['references']['RefIds']:
                        if item['rid'] == -2: continue
                        name = item['type']['class']; target, assembly = TYPE_MAP[name]
                        item['type'] = dict(klass=target, ns=assembly, asm=assembly)
                        item['type']['class'] = item['type'].pop('klass')
                emit(114, ids[owner['key']], 'MonoBehaviour', mono(ids[edge(owner, '.m_GameObject')], kind, body))
            else: raise ValueError('Unported original complete-tree component: ' + kind)
            counts[kind] += 1
        for owner in sprite_records:
            emit(114, sprite_ids[owner['key']], 'MonoBehaviour', mono(ids[edge(owner, '.m_GameObject')], 'NativeMap13EntitySpriteBinding',
                dict(SourceKey=owner['key'], Renderer=ref(ids[owner['key']]), Mask=ref())))
        for owner in slice_records:
            row = sliced[owner['key']]
            emit(114, slice_ids[owner['key']], 'MonoBehaviour', mono(ids[edge(owner, '.m_GameObject')], 'NativeMap13SlicedSpriteBinding',
                dict(SourceKey=owner['key'], PackageKey=row['package'], AssetName=row['assetName'], Renderer=ref(ids[owner['key']]))))
        emit(114, binding_id, 'MonoBehaviour', mono(ids[root['key']], 'NativeMap13TreeEntityBinding',
            dict(SourceKey=root['key'], Driver=ref(driver_id), Model=ref(300002),
                 Sprites=[ref(i) for i in sprite_ids.values()], SlicedSprites=[ref(i) for i in slice_ids.values()],
                 OriginalObjects=[dict(Key=k, Value=ref(v)) for k, v in ids.items()])))
        path = DEST / (root['name'] + '.prefab')
        write(path, ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join(rendered)).encode())
        meta(path, 'PrefabImporter'); outputs.extend([path, Path(str(path) + '.meta')])
        entities.append(dict(root=root['key'], path=path.relative_to(ROOT).as_posix(), model=model['source'],
            original_objects=len(ids), outer_objects=len(outside), native_export_sha256=sha(native_path.read_bytes()),
            managed_types=[dict(original=k, target=v[0], assembly=v[1]) for k, v in TYPE_MAP.items()]))
        print('Restored complete original tree prefab:', root['name'], len(ids), flush=True)
    meta(DEST, folder=True); outputs.append(Path(str(DEST) + '.meta'))
    outputs.extend(p for p in RUNTIME.iterdir() if p.is_file()); outputs.append(Path(str(RUNTIME) + '.meta'))
    for name in ['map14-entity-view-runtime-source-manifest.json', 'map14-entity-playable-views-source-manifest.json',
                 'map14-entity-world-stage-views-source-manifest.json', 'map13-entity-state-views-source-manifest.json',
                 'map13-entity-visibility-views-source-manifest.json', 'map13-entity-grid-views-source-manifest.json',
                 'map13-entity-fade-handlers-source-manifest.json']:
        path = PROOF.with_name(name); inputs.append(path)
        for entry in json.loads(path.read_bytes())['outputs']:
            p = ROOT / entry['path']
            if sha(p.read_bytes()) != entry['sha256']: raise ValueError('Reused application port changed: ' + entry['path'])
            inputs.append(p)
    write(PROOF, encode(dict(manifest=hierarchy['manifest'], mapClassId=13, entities=entities, counts=dict(counts),
        method='Restore complete original enclosing hierarchies, original UI/slider components and ordered managed-reference EC driver registries around verified native Models. Preserve original RIDs, every original field, null branch and pointer identity; bind original sprites, materials and original performance prefabs explicitly. Shared lifecycle classes retain their complete inherited implementation through concrete serializable identities. No substitute UI or gameplay state is generated.',
        coverage='Complete source prefab and explicit resource-binding restoration. Entity lifecycle/state services, original interaction, formal map input/save and Windows Player still require execution and integration.',
        inputs=[record(p) for p in sorted(set(inputs))], outputs=[record(p) for p in sorted(set(outputs))],
        tools=[record(Path(__file__).resolve())])))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--conversion', type=Path, required=True)
    build(parser.parse_args().conversion)
