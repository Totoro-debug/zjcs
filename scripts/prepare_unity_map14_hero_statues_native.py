"""Bind the four verified original hero-statue display prefabs for Windows.

Keep exported YAML text (including floating-point spellings); replace only
explicit script/type/resource references. Sprite payloads remain in the verified
native Sprite/Atlas library and are bound before activating an instance.
"""
import collections
import json
import re
import struct
from pathlib import Path

from official_apk import ROOT, Apk
from official_native_yaml_verification import documents
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map14_hero_statue_inputs import workspace
from prepare_unity_map14_sources import encode, sha, source_path, write

SOURCE = ROOT / 'unity/Assets/Official/World/Map/14/Source'
DEST = ROOT / 'unity/Assets/Official/World/Map/14/NativeHeroStatues'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14HeroStatues'
RECEIPT = ROOT / 'assets/reference/official-map/map14-native-hero-statues-source-manifest.json'
SCRIPT_PATHS = {
    'ECViewDriver': 'EntityViews/ECViewDriver.cs',
    'MapEntityTipDotNodeLinker': 'EntityTipDotViews/MapEntityTipDotNodeLinker.cs',
    'MaintainGlobalScale': 'WorldUI/WorldMaintainGlobalScale.cs',
    'MyPlayableSequence': 'StoryPlayables/MyPlayableSequence.cs',
    'MyPlayableAttachToBone': 'StoryAttachment/MyPlayableAttachToBone.cs',
    'ApplyEnableOnPlay': 'StoryPlayables/ApplyEnableOnPlay.cs',
}
TYPE_MODULES = {
    **dict.fromkeys(('ECAnimatorView', 'ECAttachAgentView', 'ECGridTransformView'), 'EntityTransformViews'),
    **dict.fromkeys(('ECWorldStageView', 'ECWorldStageCfgNormalProvider', 'ECPlayableWorldStageHandler'), 'EntityWorldStages'),
    'ECPlayableSupporterView': 'EntityViewPlayables',
    'ECWorldStageInteractionOperationWindow': 'EntityStageOperations',
    'ECTipdotLinkerView': 'EntityTipDotViews',
    'StateGameObjectConfig': 'WorldUI',
}
HEADER = re.compile(r'(?m)^--- !u!(\d+) &(-?\d+)\n')
GUID = re.compile(r'(?m)^guid: ([0-9a-f]{32})$')
PTR = re.compile(r'\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: (\d+)\}')


def build():
    cache = workspace()
    export = cache / 'export/ExportedProject/Assets'
    proof = json.loads((cache / 'export-source-manifest.json').read_bytes())
    restored = {Path(row['input']).relative_to(Path(source_path(export))): row for row in proof['outputs']}
    graph = json.loads((SOURCE / 'native-entity-object-graph.json').read_bytes())
    if proof['manifest'] != '871_156598' or graph['manifest'] != proof['manifest']:
        raise ValueError('Original statue versions differ')
    source_proof = json.loads((ROOT / 'assets/reference/official-map/map14-native-entity-object-graph-source-manifest.json').read_bytes())
    expected, = [row for row in source_proof['outputs'] if row['path'] == source_path(SOURCE / 'native-entity-object-graph.json')]
    if sha((SOURCE / 'native-entity-object-graph.json').read_bytes()) != expected['sha256']:
        raise ValueError('Verified original statue object graph changed')
    apk = Apk.current()
    indexed = {(row['cab'], row['path_id']): row for row in graph['objects']}
    component_types = {(r['cab'], r['path_id']): r['type'].split('.')[-1] for r in
                       json.loads((SOURCE / 'native-entity-components.json').read_bytes())['components']}
    metadata = {}
    # Index metadata only. In particular, never open generated SDK source files.
    for path in export.rglob('*.meta'):
        extended = Path('\\\\?\\' + str(path))
        match = GUID.search(extended.read_text(encoding='utf-8-sig'))
        if match:
            if match[1] in metadata:
                raise ValueError('Duplicate converter asset GUID: ' + match[1])
            metadata[match[1]] = Path(str(path)[:-5])
    material_proof = json.loads((ROOT / 'assets/reference/official-map/map14-native-entity-materials-native-source-manifest.json').read_bytes())
    materials = {(r['cab'], r['path_id']): ROOT / r['output']['path'] for r in material_proof['materials']}
    material_records = {r['output']['path']: r['output'] for r in material_proof['materials']}
    sprite_proof = json.loads((ROOT / 'assets/reference/official-map/map14-native-entity-sprites-source-manifest.json').read_bytes())
    sprite_outputs = {r['path']: r['sha256'] for r in sprite_proof['outputs']}
    for row in sprite_proof['default_materials']:
        cab, pid = row['identity'].split(':')
        materials[cab, pid] = ROOT / row['output']
    # CAB comparisons are case-insensitive; source graph retains original spelling.
    materials = {(cab.lower(), pid): p for (cab, pid), p in materials.items()}
    script_guids = {}
    for name, relative in SCRIPT_PATHS.items():
        target = ROOT / 'unity/Assets/Game/Runtime/Official' / relative
        if not target.is_file():
            # Some existing source modules use a nested application-code directory.
            candidates = list((ROOT / 'unity/Assets/Game/Runtime/Official').rglob(Path(relative).name))
            if len(candidates) != 1:
                raise ValueError('Resolve original script binding: ' + name)
            target, = candidates
        script_guids[name] = meta(target, 'MonoImporter')
    outputs, assets, visiting, sprite_count = [], [], set(), 0
    native_sources, scalar_checks, material_checks = {}, [], set()
    imported = {}

    def identity(ref):
        return ref['cab'], ref['path_id']

    def original_ref(row, field, required=True):
        matches = [identity(r) for r in row['references'] if r['field'] == field]
        if len(matches) > 1 or (required and not matches):
            raise ValueError('Ambiguous/missing original pointer: ' + field + '/' + str(row))
        return matches[0] if matches else None

    def check_native_scalars(original, native, field):
        if isinstance(original, dict):
            if original.keys() != native.keys():
                raise ValueError('Original native field shape differs: ' + field)
            for name, value in original.items():
                check_native_scalars(value, native[name], field + '.' + name)
        elif isinstance(original, float):
            if struct.pack('<f', original) != struct.pack('<f', float(str(native))):
                raise ValueError('Original native float32 bits differ: ' + field)
        elif original != native:
            raise ValueError('Original native scalar differs: ' + field)

    def verify_native_fields(source_object, kind, fields):
        wanted = {
            'Transform': ('m_LocalPosition', 'm_LocalRotation', 'm_LocalScale'),
            'SpriteRenderer': ('m_Enabled', 'm_SortingLayerID', 'm_SortingOrder', 'm_Color', 'm_FlipX', 'm_FlipY',
                               'm_DrawMode', 'm_Size', 'm_AdaptiveModeThreshold', 'm_SpriteTileMode', 'm_MaskInteraction', 'm_SpriteSortPoint'),
            'Animator': ('m_Enabled', 'm_CullingMode', 'm_UpdateMode', 'm_ApplyRootMotion', 'm_LinearVelocityBlending',
                         'm_StabilizeFeet', 'm_HasTransformHierarchy', 'm_AllowConstantClipSamplingOptimization',
                         'm_KeepAnimatorStateOnDisable', 'm_WriteDefaultValuesOnDisable'),
        }.get(kind)
        if wanted is None:
            return
        name = source_object['input']
        if name not in native_sources:
            native_sources[name] = {(o.assets_file.name, str(o.path_id)): o for o in apk.objects(name)}
        obj = native_sources[name][source_object['cab'], source_object['path_id']]
        if sha(obj.get_raw_data()) != source_object['payload_sha256']:
            raise ValueError('Original statue native payload changed')
        original = obj.read_typetree(check_read=True)
        for field in wanted:
            check_native_scalars(original[field], fields[field], kind + '.' + field)
        scalar_checks.append(dict(cab=source_object['cab'], path_id=source_object['path_id'], fields=list(wanted)))

    def verify_material(path):
        relative = source_path(path)
        if relative in material_checks:
            return
        if relative in material_records:
            record = material_records[relative]
            if sha(path.read_bytes()) != record['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != record['meta_sha256']:
                raise ValueError('Verified native material changed: ' + relative)
        elif sha(path.read_bytes()) != sprite_outputs[relative] or sha(Path(str(path) + '.meta').read_bytes()) != sprite_outputs[relative + '.meta']:
            raise ValueError('Verified native default material changed')
        material_checks.add(relative)

    def source_tree(root):
        result = {}
        def visit(goid, route):
            go = indexed[goid]
            components = [indexed[identity(r)] for r in go['references'] if re.fullmatch(r'\.m_Component\[\d+\]\.component', r['field'])]
            counts = collections.Counter()
            for component in components:
                key = (component['cab'], component['path_id'])
                kind = component_types.get(key, component['type'])
                label = (route, kind, counts[kind]); counts[kind] += 1
                if label in result:
                    raise ValueError('Ambiguous original component hierarchy')
                result[label] = component
                if kind == 'Transform':
                    for child in component['references']:
                        if re.fullmatch(r'\.m_Children\[\d+\]', child['field']):
                            child_go = original_ref(indexed[identity(child)], '.m_GameObject')
                            visit(child_go, route + '/' + indexed[child_go]['name'])
        visit(root, indexed[root]['name'])
        return result

    def native_tree(docs):
        result = {}
        transforms = {int(body['m_GameObject']['fileID']): fid for fid, (kind, body) in docs.items() if kind == 'Transform'}
        def route(goid):
            name = str(docs[goid][1]['m_Name'])
            parent = int(docs[transforms[goid]][1]['m_Father']['fileID'])
            return route(int(docs[parent][1]['m_GameObject']['fileID'])) + '/' + name if parent else name
        for fid, (kind, body) in docs.items():
            if kind == 'GameObject':
                counts = collections.Counter()
                for reference in body['m_Component']:
                    cid = int(reference['component']['fileID']); ck, cb = docs[cid]
                    if ck == 'MonoBehaviour':
                        script = metadata[cb['m_Script']['guid']]
                        ck = script.stem
                    label = (route(fid), ck, counts[ck]); counts[ck] += 1
                    if label in result:
                        raise ValueError('Ambiguous exported component hierarchy')
                    result[label] = cid
        return result

    def use_asset(path, original_root=None):
        nonlocal sprite_count
        relative = path.relative_to(export)
        if relative in imported:
            return imported[relative]
        if relative in visiting:
            raise ValueError('Resolve cyclic original external assets: ' + str(relative))
        visiting.add(relative)
        if path.suffix not in ('.prefab', '.controller', '.anim', '.asset'):
            raise ValueError('Explicit native asset binding required: ' + str(relative))
        source = ROOT / restored[relative]['path'] if relative in restored else path
        if relative in restored and sha(source.read_bytes()) != restored[relative]['sha256']:
            raise ValueError('Verified restored prefab changed')
        text = source.read_text(encoding='utf-8-sig')
        docs = documents(text)
        target = DEST / relative
        target_guid = meta(target, 'PrefabImporter' if path.suffix == '.prefab' else 'NativeFormatImporter')
        bindings = []
        native = native_tree(docs) if path.suffix == '.prefab' else {}
        if native:
            if original_root is None:
                roots = [body['m_Name'] for _, (kind, body) in docs.items() if kind == 'GameObject' and not
                         docs[next(fid for fid, (k, b) in docs.items() if k == 'Transform' and b['m_GameObject']['fileID'] == _)][1]['m_Father']['fileID']]
                name, = roots
                matches = [(r['cab'], r['path_id']) for r in graph['objects'] if r['type'] == 'GameObject' and r.get('name') == name]
                if len(matches) != 1:
                    raise ValueError('Resolve original external prefab identity: ' + str(name))
                original_root, = matches
            original = source_tree(original_root)
            if native.keys() != original.keys():
                raise ValueError('Original/exported component hierarchy differs: ' + str(relative) + '/' + str(native.keys() ^ original.keys()))
            by_fid = {native[label]: original[label] for label in native}
        else:
            by_fid = {}
        parts = HEADER.split(text)
        for index in range(1, len(parts), 3):
            fid = int(parts[index + 1]); body = parts[index + 2]
            kind, fields = docs[fid]
            source_object = by_fid.get(fid)
            if source_object is not None:
                verify_native_fields(source_object, kind, fields)
            if kind == 'MonoBehaviour':
                original_name = metadata[fields['m_Script']['guid']].stem
                if original_name not in script_guids:
                    raise ValueError('Migrate original component first: ' + original_name)
                body = re.sub(r'(?m)^(  m_Script: \{fileID: 11500000, guid: )[a-f0-9]{32}',
                              lambda m: m[1] + script_guids[original_name], body)
            if kind in ('SpriteRenderer', 'SpriteMask'):
                source_key = source_object['cab'].lower() + ':' + source_object['path_id']
                bindings.append(dict(renderer=fid, sourceKey=source_key, kind=kind))
                body, count = re.subn(r'(?m)^  m_Sprite: .*$', '  m_Sprite: {fileID: 0}', body)
                if count != 1:
                    raise ValueError('Native Sprite pointer field missing')
                sprite_count += 1
            if kind.endswith('Renderer'):
                def replace_material(match):
                    material_index = replace_material.counter; replace_material.counter += 1
                    source_id = original_ref(source_object, '.m_Materials[' + str(material_index) + ']', False)
                    if source_id is None:
                        if 'fileID: 0}' not in match[0]:
                            raise ValueError('Original material null pointer differs')
                        return match[0]
                    native_material = materials[(source_id[0].lower(), source_id[1])]
                    verify_material(native_material)
                    return '  - {fileID: 2100000, guid: ' + meta(native_material, 'NativeFormatImporter') + ', type: 2}'
                replace_material.counter = 0
                body = re.sub(r'(?m)^  - \{fileID: [^\n]+\}', replace_material,
                              body, count=len(fields['m_Materials'])) if fields.get('m_Materials') else body
            def replace_type(match):
                name = match[1]
                if name not in TYPE_MODULES:
                    raise ValueError('Migrate original managed reference first: ' + name)
                module = TYPE_MODULES[name]
                migrated_name = 'WorldStateGameObjectConfig' if name == 'StateGameObjectConfig' else name
                return 'class: ' + migrated_name + '\n        ns: Zjcs.Official.' + module + '\n        asm: Zjcs.Official.' + module
            body = re.sub(r'class: (\w+)\n        ns:[^\n]*\n        asm:[^\n]*', replace_type, body)
            def replace_pointer(match):
                file_id, guid, typ = match.groups()
                if guid in script_guids.values() or guid == '0000000000000000f000000000000000':
                    return match[0]
                # Already bound original native material.
                if guid in material_guids:
                    return match[0]
                external = metadata.get(guid)
                if external is None:
                    raise ValueError('Unknown original exported external pointer: ' + guid)
                destination, new_guid = use_asset(external)
                return '{fileID: ' + file_id + ', guid: ' + new_guid + ', type: ' + typ + '}'
            body = PTR.sub(replace_pointer, body)
            parts[index + 2] = body
        text = parts[0] + ''.join('--- !u!' + parts[i] + ' &' + parts[i + 1] + '\n' + parts[i + 2]
                                 for i in range(1, len(parts), 3))
        if bindings:
            root_go, = [fid for fid, (kind, body) in docs.items() if kind == 'GameObject' and
                        not docs[next(tid for tid, (k, b) in docs.items() if k == 'Transform' and int(b['m_GameObject']['fileID']) == fid)][1]['m_Father']['fileID']]
            component_id = 114000000000000001
            if component_id in docs:
                raise ValueError('Host binding fileID collision')
            pattern = r'(--- !u!1 &' + str(root_go) + r'\nGameObject:\n[\s\S]*?  m_Component:\n)((?:  - component: \{fileID: -?\d+\}\n)+)'
            text, count = re.subn(pattern, lambda m: m[1] + m[2] + '  - component: {fileID: ' + str(component_id) + '}\n', text)
            if count != 1:
                raise ValueError('Could not attach explicit native sprite binder')
            text += '--- !u!114 &' + str(component_id) + '\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: ' + str(root_go) + '}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ' + binder_guid + ', type: 3}\n  m_Name:\n  m_EditorClassIdentifier:\n  bindings:\n'
            for binding in bindings:
                text += '  - target: {fileID: ' + str(binding['renderer']) + '}\n    sourceKey: ' + binding['sourceKey'] + '\n'
        write(target, text.encode('utf-8')); outputs.extend((target, Path(str(target) + '.meta')))
        assets.append(dict(input=source_path(source), input_sha256=sha(source.read_bytes()), output=source_path(target),
                           output_sha256=sha(target.read_bytes()), original_root=original_root, sprite_bindings=bindings))
        imported[relative] = (target, target_guid); visiting.remove(relative)
        return imported[relative]

    material_guids = {meta(path, 'NativeFormatImporter') for path in materials.values()}
    binder_guid = meta(RUNTIME / 'NativeMap14HeroStatueSprites.cs', 'MonoImporter')
    prefabs = []
    for class_id in (6570, 6571, 6572, 6573):
        root, = [r for r in graph['roots'] if r['path'].endswith('/' + str(class_id) + '_14_view.prefab')]
        path, guid = use_asset(export / Path(root['path']).relative_to('Assets'), (root['cab'], root['path_id']))
        prefabs.append(dict(classId=class_id, path=source_path(path), guid=guid))
    meta(RUNTIME, folder=True); outputs.append(Path(str(RUNTIME) + '.meta'))
    for path in sorted({p.parent for p in outputs} | {DEST}, key=lambda p: len(p.parts)):
        while path != ROOT / 'unity/Assets' and path.is_relative_to(DEST):
            meta(path, folder=True); outputs.append(Path(str(path) + '.meta')); path = path.parent
    for path in RUNTIME.iterdir():
        if path.suffix in ('.cs', '.asmdef'):
            meta(path, 'MonoImporter' if path.suffix == '.cs' else 'AssemblyDefinitionImporter'); outputs.extend((path, Path(str(path) + '.meta')))
    write(RECEIPT, encode(dict(manifest=proof['manifest'], mapClassId=14,
        source_graph=dict(path=source_path(SOURCE / 'native-entity-object-graph.json'), sha256=sha((SOURCE / 'native-entity-object-graph.json').read_bytes())),
        converter_proof=dict(path=source_path(cache / 'export-source-manifest.json'), sha256=sha((cache / 'export-source-manifest.json').read_bytes())),
        tool=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        prefabs=prefabs, assets=assets, native_field_checks=scalar_checks, native_material_checks=sorted(material_checks),
        counts=dict(assets=len(assets), sprites=sprite_count, native_components_checked=len(scalar_checks)),
        outputs=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in sorted(set(outputs))],
        operation='Retain verified original native prefab hierarchy and YAML scalar text; explicitly bind migrated original scripts/managed-reference types, original native materials, controller/clip/mesh/effect assets. Bind original renderer CAB/PID Sprite payloads before activation. No original entity/gameplay replacement or completion is implied.')))
    print('Original hero-statue displays bound:', len(prefabs), 'displays;', len(assets), 'native assets;', sprite_count, 'sprite bindings')


if __name__ == '__main__':
    build()
