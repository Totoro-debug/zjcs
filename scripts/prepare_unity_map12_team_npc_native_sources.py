"""Retain and verify the original 91 NPC UI prefabs as native conversion source.

The JSON carries the actual restored YAML, not a reconstructed UI. Native Unity
installation and runtime script/resource bindings are deliberately separate.
"""
from __future__ import annotations

import argparse
import base64
from collections import Counter
import hashlib
import io
import json
from pathlib import Path
import re
from PIL import Image

from official_apk import Apk, ROOT
from UnityPy.export import Texture2DConverter
import texture2ddecoder
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map12_sources import encode, sha, source_path
from prepare_unity_map12_team_npc_sources import OUTPUT as ORIGINAL
from prepare_unity_map12_team_npc_native_export import ConversionPath
from official_native_yaml_verification import documents, reference_shapes, element, unpack_array, float_bits
from UnityPy.classes.PPtr import PPtr
from UnityPy.helpers.Tpk import get_typetree_node

OUTPUT = ROOT / 'unity/Assets/Official/World/Map/12/Source/native-team-npc-prefabs.json'
RECEIPT = ROOT / 'assets/reference/official-map/map12-team-npc-native-source-manifest.json'
GUID = re.compile(r'(?m)^guid: ([a-f0-9]{32})$')


def shape(rows):
    stack, root = [], None
    for row in rows:
        value = dict(type=row['type'], name=row['name'], children={})
        while len(stack) > row['level']:
            stack.pop()
        if len(stack) != row['level']:
            raise ValueError('Original team schema skips a level')
        if stack:
            stack[-1]['children'][value['name']] = value
        else:
            root = value
        stack.append(value)
    return root


def build(stage, write=False):
    from validation_workspace import validation_directory
    stage = ConversionPath(stage.resolve())
    workspace = validation_directory()
    if workspace == ROOT / 'output/validation' or not stage.is_relative_to(workspace):
        raise ValueError('Use a verified original team export in the owning validation workspace')
    tools = ('scripts/prepare_unity_map12_team_npc_native_sources.py', 'scripts/prepare_unity_map12_team_npc_native_export.py',
             'scripts/prepare_unity_map13_water_entity_export.py', 'scripts/official_native_yaml_verification.py')
    guarded = [dict(path=p, sha256=sha((ROOT / p).read_bytes())) for p in tools]
    decoder_paths = [Path(Texture2DConverter.__file__), Path(Image.__file__), Path(texture2ddecoder.__file__)]
    decoder_paths += sorted(Path(texture2ddecoder.__file__).parent.glob('*.pyd'))
    decoder_paths += sorted(Path(Image.__file__).parent.glob('_imaging*.pyd'))
    decoding_inputs = [dict(path=str(p.resolve()), sha256=sha(p.read_bytes())) for p in sorted(set(decoder_paths))]
    export_proof_path = stage / 'native-export-proof.json'
    export_proof = json.loads(export_proof_path.read_bytes())
    for key in ('entry', 'generator', 'verification_library', 'input_receipt', 'tool'):
        record = export_proof[key]
        if sha(ConversionPath(ROOT / record['path']).read_bytes()) != record['sha256']:
            raise ValueError('Original team native export proof changed: ' + key)
    restored_hashes = {}
    for record in export_proof['outputs']:
        path = ConversionPath(ROOT / record['path'])
        if sha(path.read_bytes()) != record['sha256']:
            raise ValueError('Original restored team native output changed')
        restored_hashes[path] = record['sha256']
    input_receipt = json.loads((stage / 'inputs/source-manifest.json').read_bytes())
    original_raw = ORIGINAL.read_bytes()
    if sha(original_raw) != input_receipt['source']['sha256']:
        raise ValueError('Original team hierarchy source changed')
    original = json.loads(original_raw)
    apk = Apk.current()
    if original['manifest'] != apk.version or apk.version != '871_156598':
        raise ValueError('Original team native source version differs')
    roots = sorted({'defaultpackage_assets_prefabs_' + row['figureModelUI'].rsplit('/', 1)[0].lower().replace('/', '_') + '.bundle' for row in original['models']})
    _, missing = collect(apk, roots)
    if missing:
        raise ValueError('Resolve original team native pointer sources: ' + str(missing))
    objects = {file.name + ':' + str(obj.path_id): obj for bundle in apk.loaded.values()
               for file in serialized_files(bundle) for obj in file.objects.values()}
    source_records = {row['identity']: row for row in original['objects']}
    schemas = {key: shape(rows) for key, rows in original['schemas'].items()}
    registry_shapes = reference_shapes(input_receipt['managed_reference_unions'])
    exported, restored = stage / 'export/ExportedProject/Assets', stage / 'restored'
    index, cache = {}, {}
    for meta in exported.rglob('*.meta'):
        match = GUID.search(meta.read_text(encoding='utf8'))
        if match:
            if match[1] in index:
                raise ValueError('Original team native GUID is ambiguous')
            path = meta.with_suffix('')
            replacement = restored / path.relative_to(exported)
            index[match[1]] = replacement if replacement.is_file() else path

    def docs(path):
        if path not in cache:
            cache[path] = documents(path.read_text(encoding='utf8'))
        return cache[path]

    bindings, panels, counts, external, texture_overrides, texture_checks = {}, [], Counter(), {}, {}, {}

    def bind(original_id, path, actual_id):
        expected = source_records[original_id]
        kind, actual = docs(path)[actual_id]
        if kind != 'GameObject' or actual['m_Name'] != expected['fields']['m_Name']:
            raise ValueError('Original team hierarchy node differs: ' + original_id)
        bindings[original_id] = path, actual_id
        components = expected['fields']['m_Component']
        if len(components) != len(actual['m_Component']):
            raise ValueError('Original team component count differs')
        owner = objects[original_id]
        transform = native_transform = None
        for left, right in zip(components, actual['m_Component']):
            obj = PPtr(**left['component'], assetsfile=owner.assets_file).deref()
            identity = obj.assets_file.name + ':' + str(obj.path_id)
            target_id = right['component']['fileID']
            if docs(path)[target_id][0] != obj.type.name:
                raise ValueError('Original team component order differs')
            bindings[identity] = path, target_id
            if obj.type.name in ('Transform', 'RectTransform'):
                transform, native_transform = obj, docs(path)[target_id][1]
        children = transform.read().m_Children
        if len(children) != len(native_transform['m_Children']):
            raise ValueError('Original team child count differs')
        for left, right in zip(children, native_transform['m_Children']):
            go = left.read().m_GameObject.deref()
            bind(go.assets_file.name + ':' + str(go.path_id), path, docs(path)[right['fileID']][1]['m_GameObject']['fileID'])

    for panel in original['models']:
        name = Path(panel['path']).stem
        relative = panel['path'].removeprefix('Assets/')
        candidate = restored / relative
        path = candidate if candidate.is_file() else exported / relative
        if not path.is_file():
            raise ValueError('Original native team root absent: ' + name)
        if path not in restored_hashes:
            selected_components = [key for row in panel['nodes'] for key in row['components']]
            if any('references' in source_records[key]['fields'] for key in selected_components):
                raise ValueError('Original team managed-reference root was not restored: ' + name)
        roots = [i for i, (kind, fields) in docs(path).items() if kind in ('Transform', 'RectTransform') and fields['m_Father']['fileID'] == 0]
        root_transform, = roots
        root_id = docs(path)[root_transform][1]['m_GameObject']['fileID']
        bind(panel['root'], path, root_id)
        original_meta = (exported / relative).with_name(name + '.prefab.meta')
        native_guid = GUID.search(original_meta.read_text(encoding='utf8'))[1]
        panels.append(dict(name=name, npcClassId=panel['npcClassId'], figureModelUI=panel['figureModelUI'], originalRoot=panel['root'], rootFileId=str(root_id), nativeGuid=native_guid,
                           yaml=path.read_bytes().decode('utf8'), sha256=sha(path.read_bytes())))
    if set(bindings) != set(source_records):
        raise ValueError('Original team native hierarchy coverage differs')

    def compare(expected, actual, owner, path, field_shape, label):
        if isinstance(expected, dict) and set(expected) == {'m_FileID', 'm_PathID'}:
            if not expected['m_PathID']:
                if actual != dict(fileID=0):
                    raise ValueError('Original team null pointer differs: ' + label)
            else:
                target = PPtr(**expected, assetsfile=owner.assets_file).deref()
                identity = target.assets_file.name + ':' + str(target.path_id)
                target_path = index[actual['guid']] if actual.get('guid') else path
                if identity in bindings:
                    if (target_path, actual['fileID']) != bindings[identity]:
                        raise ValueError('Original team hierarchy pointer differs: ' + label)
                elif target.type.name == 'MonoScript':
                    script = target.read()
                    relative = target_path.relative_to(exported).as_posix()
                    if relative != 'Scripts/' + script.m_AssemblyName.removesuffix('.dll') + '/' + ((script.m_Namespace.replace('.', '/') + '/') if script.m_Namespace else '') + script.m_ClassName + '.cs':
                        raise ValueError('Original team script assembly/type differs: ' + label + ' ' + relative)
                else:
                    pixels = None
                    if target.type.name == 'Texture2D' and target_path.suffix.lower() == '.png':
                        key = identity, target_path
                        if key not in texture_checks:
                            expected_image = target.read().image.convert('RGBA')
                            with target_path.open('rb') as stream:
                                actual_image = Image.open(stream).convert('RGBA')
                            if expected_image.size != actual_image.size:
                                raise ValueError('Original team texture dimensions differ: ' + label)
                            original_rgba = expected_image.tobytes()
                            pixels = dict(width=expected_image.width, height=expected_image.height, rgba_sha256=sha(original_rgba))
                            if original_rgba != actual_image.tobytes():
                                # Keep the checked YAML/identity intact. Supply an
                                # exact original decoded PNG for final installation
                                # instead of accepting converter ASTC rounding.
                                stream = io.BytesIO()
                                expected_image.save(stream, format='PNG')
                                raw_png = stream.getvalue()
                                if Image.open(io.BytesIO(raw_png)).convert('RGBA').tobytes() != original_rgba:
                                    raise ValueError('Original team PNG pixel round trip differs')
                                texture_overrides[identity] = dict(original=identity,
                                    convertedPath=target_path.relative_to(exported).as_posix(), nativeGuid=actual['guid'],
                                    originalConverterPngSha256=sha(target_path.read_bytes()),
                                    originalConverterRgbaSha256=sha(actual_image.tobytes()),
                                    restoredPngSha256=sha(raw_png), decodedPixels=pixels,
                                    pngBase64=base64.b64encode(raw_png).decode('ascii'),
                                    operation='Decode the same original Texture2D with the established UnityPy path; retain exact RGBA bytes in a lossless PNG overlay. Preserve native GUID, dimensions, layout and serialized texture references.')
                                counts['texture_decoding_overrides'] += 1
                            texture_checks[key] = pixels
                        pixels = texture_checks[key]
                    elif target.type.name == 'Font' and target_path.suffix.lower() in ('.ttf', '.otf'):
                        if bytes(target.read().m_FontData) != target_path.read_bytes():
                            raise ValueError('Original team font bytes differ: ' + label)
                    else:
                        if target_path.suffix.lower() not in ('.prefab', '.asset', '.mat', '.controller', '.anim', '.overridecontroller'):
                            raise ValueError('Inspect original team external native resource: ' + label + ' ' + target.type.name + ' ' + str(target_path))
                        kind, fields = docs(target_path)[actual['fileID']]
                        native_name = fields.get('m_Name')
                        native_name = '' if native_name is None else getattr(native_name, 'scalar', native_name)
                        if kind != target.type.name or native_name != target.peek_name():
                            raise ValueError('Original team external reference identity differs: ' + label + ' expected=' +
                                repr((target.type.name, target.peek_name())) + ' actual=' + repr((kind, fields.get('m_Name'))) + ' path=' + str(target_path))
                    external[identity] = dict(source=apk.provenance(target), payload_sha256=sha(target.get_raw_data()),
                        convertedPath=target_path.relative_to(restored if target_path.is_relative_to(restored) else exported).as_posix(),
                        convertedFileId=str(actual['fileID']), convertedSha256=sha(target_path.read_bytes()))
                    if pixels:
                        external[identity]['decodedPixels'] = pixels
            counts['pointers'] += 1
        elif isinstance(expected, dict):
            if not isinstance(actual, dict) or set(expected) != set(actual):
                raise ValueError('Original team native field structure differs: ' + label)
            for key, value in expected.items():
                compare(value, actual[key], owner, path, field_shape['children'][key], label + '.' + key)
        elif isinstance(expected, list):
            if not isinstance(actual, list):
                actual = unpack_array(actual, field_shape, len(expected))
                counts['packed_arrays'] += 1
            if len(expected) != len(actual):
                raise ValueError('Original team array length differs: ' + label)
            for at, (left, right) in enumerate(zip(expected, actual)):
                compare(left, right, owner, path, element(field_shape, left), label + '[' + str(at) + ']')
        elif isinstance(expected, float):
            if float_bits(expected, field_shape) != float_bits(float(getattr(actual, 'scalar', actual)), field_shape):
                raise ValueError('Original team float bits differ: ' + label)
            counts['values'] += 1
        elif isinstance(expected, str) and field_shape['type'] == 'string':
            # Unity uses the serialized field type: blank/numeric-looking YAML
            # tokens are strings here. Retain and compare their lexical text.
            text = '' if actual is None else getattr(actual, 'scalar', actual)
            if text != expected:
                raise ValueError('Original team native string differs: ' + label)
            counts['string_scalars'] += 1
        elif expected != actual:
            raise ValueError('Original team native scalar differs: ' + label + ' ' + repr(expected) + ' ' + repr(actual))
        else:
            counts['values'] += 1

    for identity, record in source_records.items():
        owner = objects[identity]
        if sha(owner.get_raw_data()) != record['payload_sha256']:
            raise ValueError('Original team object bytes changed')
        path, file_id = bindings[identity]
        actual = docs(path)[file_id][1]
        if record['type'] == 'MonoBehaviour':
            key = record['assembly'] + ':' + record['scriptType']
            current_shape = registry_shapes.get((owner.assets_file.name, owner.serialized_type.script_type_index), schemas[key])
        else:
            tree = owner.serialized_type.node or get_typetree_node(owner.type.value, owner.version)
            current_shape = shape([dict(level=n.m_Level, type=n.m_Type, name=n.m_Name) for n in tree.traverse()])
        for name, expected in record['fields'].items():
            if name == 'm_Tag':
                if expected != 0 or actual['m_TagString'] != 'Untagged':
                    raise ValueError('Original team runtime tag differs')
                counts['runtime_tag_to_editor_name'] += 1
                continue
            if name not in actual:
                raise ValueError('Original team field absent from native YAML: ' + identity + '.' + name)
            compare(expected, actual[name], owner, path, current_shape['children'][name], identity + '.' + name)
        counts[record['type']] += 1
    payload = encode(dict(manifest=apk.version, mapClassId=12, panels=panels,
        bindings=[dict(original=key, panel=path.stem, fileId=str(file_id)) for key, (path, file_id) in sorted(bindings.items())],
        externalReferences=[dict(identity=key, **value) for key, value in sorted(external.items())],
        textureOverrides=[value for _, value in sorted(texture_overrides.items())]))
    meta_path = OUTPUT.with_name(OUTPUT.name + '.meta')
    guid = hashlib.md5(('zjcs-official:' + source_path(OUTPUT).removeprefix('unity/')).encode()).hexdigest()
    metadata = ('fileFormatVersion: 2\nguid: ' + guid + '\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    receipt = encode(dict(format=1, manifest=apk.version, mapClassId=12,
        original=dict(path=source_path(ORIGINAL), sha256=sha(original_raw)),
        nativePreparation=dict(path='assets/reference/official-map/map12-team-npc-native-input-source-manifest.json', sha256=sha((stage / 'inputs/source-manifest.json').read_bytes())),
        nativeExport=export_proof, tools=guarded, counts=dict(counts),
        textureDecodingInputs=decoding_inputs,
        textureDecodingOverrides=[{k: v for k, v in value.items() if k != 'pngBase64'} for _, value in sorted(texture_overrides.items())],
        outputs=[dict(path=source_path(OUTPUT), sha256=sha(payload), bytes=len(payload)),
                 dict(path=source_path(meta_path), sha256=sha(metadata), bytes=len(metadata))],
        operation='Retain exact restored native YAML for 91 original NPC prefabs. Compare every original hierarchy node, component/child order, field, scalar, float bit, local object pointer and script assembly/type against the decoded APK source.',
        limitations=['Native source only: Unity prefab installation, exact final image/material/shader/font bindings and runtime scripts/services remain pending.',
                     'External resource source IDs and converted hashes are retained; final Windows bindings must verify the same original objects/pixels, not names alone.',
                     'No replacement UI, Spine runtime, actual input/render/save verification or Windows publication is provided by this source step.']))
    if any(sha((ROOT / row['path']).read_bytes()) != row['sha256'] for row in guarded):
        raise ValueError('Team native source verification code changed during execution')
    if any(sha(Path(row['path']).read_bytes()) != row['sha256'] for row in decoding_inputs):
        raise ValueError('Team texture decoding dependency changed during verification')
    for path, content in ((OUTPUT, payload), (meta_path, metadata), (RECEIPT, receipt)):
        if write:
            path.write_bytes(content)
        elif not path.is_file() or path.read_bytes() != content:
            raise ValueError('Original team native source drift: ' + str(path))
    print('Original 91 team NPC native prefab sources verified:', dict(counts), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage-dir', type=Path, required=True)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    build(args.stage_dir, args.write)
