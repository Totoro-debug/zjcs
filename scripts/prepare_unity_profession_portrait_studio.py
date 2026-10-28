"""Recover the original portrait studio closure without installing incomplete actors."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from prepare_unity_bundle_inputs import collect, read_source
from prepare_unity_map13_water_entity_inputs import hydrate
from prepare_unity_map13_water_entity_export import convert, original_identity
from prepare_unity_profession_native_inputs import CACHE

STUDIO = 'defaultpackage_assets_prefabs_appearance_portrait.bundle'
CONFIG = 'defaultpackage_assets_scriptableconfig_modules_playerportrait.bundle'
DEST = CACHE / 'portrait-studio'
PROOF = ROOT / 'assets/reference/profession-ui/portrait-studio-source-manifest.json'


def verify(apk, schemas, receipt):
    from collections import Counter
    import re
    from UnityPy.classes import PPtr
    from prepare_unity_bundle_inputs import serialized_files
    from official_native_yaml_verification import documents, element, unpack_array, float_bits
    from verify_unity_map14_story_playable_hierarchy import shape
    exported = DEST / 'export/ExportedProject/Assets'
    guid_pattern = re.compile(r'^guid: ([a-f0-9]{32})$', re.M)
    index = {guid_pattern.search(p.read_text(encoding='utf-8'))[1]: p.with_suffix('')
             for p in exported.rglob('*.meta')}
    parsed, identities = {}, {}
    counts = Counter()

    def source_identity(obj):
        if obj.type.name not in ('GameObject', 'Transform', 'RectTransform', 'MonoBehaviour', 'MonoScript'):
            data = obj.read(check_read=False)
            parent = getattr(data, 'm_GameObject', None)
            if parent and parent.m_PathID:
                return obj.type.name, original_identity(parent.deref())[1]
        return original_identity(obj)

    def docs(path):
        if path not in parsed:
            parsed[path] = documents(path.read_text(encoding='utf-8'))
        return parsed[path]

    def identity(path, file_id):
        kind, fields = docs(path)[file_id]
        if kind == 'GameObject':
            transforms = [v['component']['fileID'] for v in fields['m_Component']
                          if docs(path)[v['component']['fileID']][0] in ('Transform', 'RectTransform')]
            parent = docs(path)[transforms[0]][1]['m_Father']['fileID']
            return kind, (identity(path, docs(path)[parent][1]['m_GameObject']['fileID'])[1] + '/' if parent else '') + fields['m_Name']
        if kind == 'MonoBehaviour':
            kind = index[fields['m_Script']['guid']].stem
        if kind in ('Transform', 'RectTransform') or fields.get('m_GameObject', {}).get('fileID'):
            return kind, identity(path, fields['m_GameObject']['fileID'])[1]
        return kind, fields.get('m_Name') or ''

    for path in exported.rglob('*'):
        if path.suffix not in ('.asset', '.prefab'): continue
        for file_id in docs(path):
            key = identity(path, file_id)
            if key in identities: raise ValueError('Ambiguous original studio export identity: ' + str(key))
            identities[key] = path, file_id

    def compare(left, right, tree, owner, path, label):
        if isinstance(left, dict) and set(left) == {'m_FileID', 'm_PathID'}:
            if not left['m_PathID']:
                if right != {'fileID': 0}: raise ValueError('Original null pointer changed: ' + label)
            else:
                target = PPtr(**left, assetsfile=owner.assets_file).deref()
                export_path = index[right['guid']] if right.get('guid') else path
                if target.type.name == 'MonoScript':
                    script = target.read()
                    if export_path.stem != script.m_ClassName: raise ValueError('Original script binding changed: ' + label)
                elif source_identity(target) != identity(export_path, right['fileID']):
                    raise ValueError('Original studio pointer target changed: ' + label)
            counts['pointers'] += 1
            return
        if isinstance(left, dict):
            for key, value in left.items():
                if key == 'm_Tag' and owner.type.name == 'GameObject':
                    if value != 0 or right.get('m_TagString') != 'Untagged':
                        raise ValueError('Inspect original non-default GameObject tag')
                    counts['nativeUntaggedTags'] += 1
                    continue
                lightmaps = dict(m_LightmapIndex=65535, m_LightmapIndexDynamic=65535,
                    m_LightmapTilingOffset=dict(x=1., y=1., z=0., w=0.),
                    m_LightmapTilingOffsetDynamic=dict(x=1., y=1., z=0., w=0.))
                if key not in right and owner.type.name == 'MeshRenderer' and key in lightmaps:
                    if value != lightmaps[key]: raise ValueError('Original non-default runtime lightmap data requires restoration')
                    counts['runtimeLightmapDefaults'] += 1
                    continue
                if key not in right and owner.type.name == 'MeshRenderer' and key == 'm_EnlightenVertexStream':
                    if value != dict(m_FileID=0, m_PathID=0): raise ValueError('Original Enlighten vertex stream requires restoration')
                    counts['nullRuntimeEnlightenStreams'] += 1
                    continue
                if key not in right: raise ValueError('Original field missing: ' + label + '.' + key)
                compare(value, right[key], tree['children'][key], owner, path, label + '.' + key)
        elif isinstance(left, list):
            if not isinstance(right, list): right = unpack_array(right, tree, len(left))
            if len(left) != len(right): raise ValueError('Original array size changed: ' + label)
            for a, b in zip(left, right): compare(a, b, element(tree), owner, path, label)
        elif isinstance(left, float):
            if float_bits(left, tree) != float_bits(float(getattr(right, 'scalar', right)), tree): raise ValueError('Original float bits changed: ' + label)
        elif isinstance(left, str):
            if left != ('' if right is None else str(right)): raise ValueError('Original string changed: ' + label)
        elif left != right: raise ValueError('Original value changed: ' + label)
        counts['values'] += 1

    selected = {'GameObject', 'Transform', 'Camera', 'MeshFilter', 'MeshRenderer', 'MonoBehaviour'}
    bindings = []
    for name in (STUDIO, CONFIG):
        for file in serialized_files(apk.loaded[name]):
            for obj in file.objects.values():
                if obj.type.name not in selected: continue
                key = source_identity(obj)
                path, file_id = identities[key]
                if obj.type.name == 'MonoBehaviour': fields, tree = schemas.read(obj)
                else: fields, tree = obj.read_typetree(check_read=True), obj.serialized_type.node
                compare(fields, docs(path)[file_id][1], shape(tree), obj, path, str(key))
                counts[obj.type.name] += 1
                bindings.append(dict(source=apk.provenance(obj), path=path.relative_to(ROOT).as_posix(), file_id=file_id))
    if counts['MonoBehaviour'] != 11 or counts['GameObject'] != 5:
        raise ValueError('Inspect original studio component coverage: ' + str(counts))
    return dict(format=1, manifest=apk.version, input_receipt=record(receipt),
        entry=record(Path(__file__)), counts=dict(counts), bindings=bindings,
        outputs=[record(p) for p in sorted(parsed)],
        operation='Strictly decode and roundtrip all 15 components in the eight-bundle closure. Compare the studio and manager/settings root bundles: 11 MonoBehaviours, five GameObjects/transforms, Camera, MeshFilter and MeshRenderer, including values, float bits, hierarchy and referenced component identities against native converter output. Explicitly verify the default runtime lightmap/null Enlighten fields and native Untagged representation. Preserve unbound output only in the source conversion cache.',
        limitations=['No actor component stubs or incomplete studio prefab are installed. Windows import, original appearance classes, live native animation, production graph and full profession page remain required.'])


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else str(path),
                sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build(project, run_converter):
    project = project.resolve()
    if project != (ROOT / 'unity').resolve():
        from validation_workspace import require_temporary_unity_project
        require_temporary_unity_project(project)
    apk = Apk.current()
    if apk.version != '871_156598' or apk.index[STUDIO] != '9466f2e5b048b0119c9d63e6cdf26406':
        raise ValueError('Original studio version changed')
    config = apk.asset(CONFIG, '/CharacterPortraitManagerConfig.asset')
    code = ROOT / '.tools/source-packages/current/871_156598/official-code'
    code_manifest = code.parent / 'code-manifest.json'
    code_inputs = json.loads(code_manifest.read_bytes())
    if code_inputs['manifest'] != apk.version:
        raise ValueError('Original studio and managed-code versions differ')
    for name, entry in code_inputs['assemblies'].items():
        if record(code / 'managed/decoded' / name)['sha256'] != entry['decoded_sha256']:
            raise ValueError('Original studio managed assembly changed: ' + name)
    schemas = NativeSchemas(code, unity_project=project)
    schema_proof = install(schemas, code)
    # AppearanceAnimation declares no serialized fields. Recover its inherited
    # AppearanceRenderer/AppearanceSpine layout, already checked by install().
    source = code / 'client-source/AppearanceSystem.Runtime/AppearanceAnimation.cs'
    text = source.read_text(encoding='utf-8')
    if '[SerializeField]' in text or 'class AppearanceAnimation : AppearanceRenderer,' not in text:
        raise ValueError('Inspect original AppearanceAnimation serialization')
    tree = schemas.default.tree

    def studio_tree(assembly, name):
        if name == 'AppearanceSystem.Runtime.AppearanceAnimation':
            result = tree(assembly, 'AppearanceSystem.Runtime.AppearanceMecanim')
            schemas.default.cache[assembly + ':' + name] = schemas.default.cache[
                assembly + ':AppearanceSystem.Runtime.AppearanceMecanim']
            schemas.default.records[assembly + ':' + name] = schemas.default.cache[assembly + ':' + name]
            return result
        return tree(assembly, name)

    schemas.default.tree = studio_tree
    config_fields, _ = schemas.read(config)
    from UnityPy.classes import PPtr
    studio = PPtr(**config_fields['CharacterPortraitStudioPrefab'], assetsfile=config.assets_file).deref()
    if apk.provenance(studio)['bundle'] != STUDIO:
        raise ValueError('Original manager no longer references the expected studio')
    _, missing = collect(apk, [CONFIG, STUDIO])
    if missing:
        raise ValueError('Missing original studio dependencies: ' + str(missing))
    folder = DEST / 'inputs'
    folder.mkdir(parents=True, exist_ok=True)
    inputs, components, changes, unions = [], [], [], []
    for name in sorted(apk.loaded):
        raw, provenance = read_source(apk, name)
        converted, rows, altered, references, _ = hydrate(apk.loaded[name], schemas, include_source_identity=True)
        components.extend(rows)
        changes.extend(altered)
        unions.extend(references)
        output = folder / Path(name).name
        output.write_bytes(converted)
        inputs.append(dict(provenance, output=output.name,
            converter_input_sha256=hashlib.sha256(converted).hexdigest(), converter_input_bytes=len(converted)))
    (folder / 'unity default resources').write_bytes(apk.archive.read('assets/bin/Data/unity default resources'))
    tools = [Path(__file__), source, code_manifest, *schemas.reference_inputs]
    tools += [ROOT / 'scripts' / name for name in ('map13_native_schemas.py',
        'map13_entity_native_schemas.py', 'official_managed_reference_conversion.py',
        'prepare_unity_map13_water_entity_inputs.py', 'prepare_unity_map13_water_entity_export.py',
        'official_native_yaml_verification.py', 'verify_unity_map14_story_playable_hierarchy.py',
        'official_unity_schema.py', 'prepare_unity_bundle_inputs.py', 'official_apk.py')]
    receipt = dict(format=1, manifest=apk.version, root=STUDIO, roots=[CONFIG, STUDIO],
        inputs=inputs, components=components, converted_objects=changes,
        managed_reference_unions=unions, schema_proof=schema_proof,
        config=apk.provenance(config), studio=apk.provenance(studio),
        tools=[record(p) for p in sorted(set(tools))], outputs=[],
        limitations=['Conversion cache only. Original runtime appearance types, native animation backend, explicit studio binding and real character validation remain required.'])
    receipt_path = folder / 'source-manifest.json'
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    if run_converter:
        convert(DEST)
    if (DEST / 'export/ExportedProject/Assets').is_dir():
        proof = verify(apk, schemas, receipt_path)
        PROOF.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Recovered original studio:', len(inputs), 'bundles;', len(components), 'strictly decoded components.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity-project', type=Path, default=ROOT / 'unity')
    parser.add_argument('--convert', action='store_true')
    args = parser.parse_args()
    build(args.unity_project, args.convert)
