"""Restore the three original tree performance prefabs and their real scripts.

The private native export supplies engine serialization. Every source object is
paired by ordered hierarchy/component identity; original application fields are
restored from verified managed type trees, never from converter script stubs.
"""
from collections import Counter
from pathlib import Path
import argparse
import json
import re
import struct
import yaml

from official_apk import ROOT
from official_native_yaml_verification import documents
from prepare_unity_map13_prop_parts import dump, meta, ref
from prepare_unity_map13_prop_hierarchies import key, sha, write, encode
from prepare_unity_story_attachment_runtime import record

SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source/native-tree-playables.json'
DEST = ROOT / 'unity/Assets/Game/Resources/Map13/Native/TreePlayables'
PROOF = ROOT / 'assets/reference/official-map/map13-native-tree-playable-prefabs-source-manifest.json'
SCRIPT_FOLDERS = {
    'MyPlayableSequence': 'StoryPlayables', 'ApplyBySource': 'StoryPlayables',
    'ApplyEnableOnPlay': 'StoryPlayables', 'MyPlayableAnimator': 'StoryAnimation',
    'MyPlayableActiveBone': 'StoryAttachment', 'MyPlayableAttachToBone': 'StoryAttachment',
    'MyPlayableFollowBone': 'StoryFollowBone',
    'MyPlayableMaterialPropertyBlock': 'World/Map13TreePlayables',
    'ApplyAnimatorTriggerByAwardItemCount': 'World/Map13TreePlayables',
    'ApplyByBone': 'World/Map13TreePlayables',
}


def build(conversion):
    conversion = conversion.resolve()
    if not conversion.is_relative_to((ROOT / 'output/validation').resolve()):
        raise ValueError('Native conversion must belong to a validation workspace')
    exported = conversion / 'export/ExportedProject/Assets'
    source_proof = PROOF.with_name('map13-native-tree-playables-source-manifest.json')
    for output in json.loads(source_proof.read_bytes())['outputs']:
        if sha((ROOT / output['path']).read_bytes()) != output['sha256']:
            raise ValueError('Original tree performance inventory changed')
    source = json.loads(SOURCE.read_bytes())
    objects = {o['key']: o for o in source['objects']}
    materials_path = SOURCE.with_name('native-entity-material-bindings.json')
    materials = {key(r['cab'], r['path_id']): ROOT / r['output']['path']
                 for r in json.loads(materials_path.read_bytes())['entries']}
    exported_scripts = {}
    for path in exported.rglob('*.cs.meta'):
        guid = re.search(r'(?m)^guid: (\w+)', path.read_text())
        if guid:
            exported_scripts[guid[1]] = path.name[:-8]
    conversion_manifest = json.loads((conversion / 'source-manifest.json').read_bytes())
    inputs = [SOURCE, source_proof, materials_path]
    scripts = {}
    for name, folder in SCRIPT_FOLDERS.items():
        path = ROOT / 'unity/Assets/Game/Runtime/Official' / folder / (name + '.cs')
        scripts[name] = meta(path, 'MonoImporter')
        inputs.extend([path, Path(str(path) + '.meta')])
    binding = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap13TreePlayables'
    for path in binding.iterdir():
        if path.suffix in ['.cs', '.asmdef']:
            meta(path, 'MonoImporter' if path.suffix == '.cs' else 'AssemblyDefinitionImporter')
    meta(binding, folder=True)
    sequence_adapter = binding / 'NativeMap13TreePlayableSequence.cs'
    scripts['MyPlayableSequence'] = meta(sequence_adapter, 'MonoImporter')
    inputs.extend(p for p in binding.iterdir() if p.is_file())
    inputs.append(Path(str(binding) + '.meta'))
    outputs, prefabs, counts = [], [], Counter()
    for prefab in source['prefabs']:
        path = exported / 'GameObject' / (prefab['name'] + '.prefab')
        text = path.read_text(encoding='utf-8-sig')
        docs = documents(text)
        paired, native_ids = {}, []

        def edge(owner, field):
            matches = [r for r in owner['references'] if r['field'] == field]
            if len(matches) != 1:
                raise ValueError('Original reference not unique: ' + owner['key'] + field)
            return key(matches[0]['cab'], matches[0]['path_id'])

        def short_type(identity):
            return objects[identity]['type'].rsplit('.', 1)[-1]

        def pair(identity, file_id):
            file_id = int(file_id)
            obj = objects[identity]
            native = docs[file_id][1]
            if obj['type'] != 'GameObject' or native['m_Name'] != obj['fields']['m_Name']:
                raise ValueError('Original ordered performance hierarchy differs')
            paired[identity] = file_id
            original = [edge(obj, '.m_Component[' + str(i) + '].component')
                        for i in range(len(obj['fields']['m_Component']))]
            actual = [r['component']['fileID'] for r in native['m_Component']]
            if len(original) != len(actual):
                raise ValueError('Original component count differs: ' + identity)
            for source_id, target_id in zip(original, actual):
                target_id = int(target_id)
                kind, fields = docs[target_id]
                actual_type = exported_scripts[fields['m_Script']['guid']] if kind == 'MonoBehaviour' else kind
                if actual_type != short_type(source_id):
                    raise ValueError('Original component order/type differs: ' + source_id)
                paired[source_id] = target_id
                if kind != 'MonoBehaviour':
                    native_ids.append(source_id)
            source_tf, = [i for i in original if short_type(i) == 'Transform']
            tf = objects[source_tf]
            target_tf = docs[paired[source_tf]][1]
            if len(tf['fields']['m_Children']) != len(target_tf['m_Children']):
                raise ValueError('Original child count differs: ' + identity)
            for i, target in enumerate(target_tf['m_Children']):
                child = objects[edge(tf, '.m_Children[' + str(i) + ']')]
                pair(edge(child, '.m_GameObject'), docs[target['fileID']][1]['m_GameObject']['fileID'])

        roots = [i for i, (kind, fields) in docs.items()
                 if kind == 'Transform' and fields['m_Father']['fileID'] == 0]
        if len(roots) != 1:
            raise ValueError('Original native performance root not unique')
        pair(prefab['root'], docs[roots[0]][1]['m_GameObject']['fileID'])
        if set(paired) != set(prefab['objects']) or len(paired) != len(docs):
            raise ValueError('Not every original performance object was paired')
        external, signed_zeros = {}, {}

        def compare(expected, actual, owner, field='', tokens=(), file_id=None, repair=True):
            if isinstance(expected, dict):
                if set(expected) == {'m_FileID', 'm_PathID'}:
                    if not expected['m_PathID']:
                        if actual != {'fileID': 0}:
                            raise ValueError('Original null reference differs: ' + field)
                    else:
                        identity = edge(owner, field)
                        if identity in paired:
                            if actual != {'fileID': paired[identity]}:
                                raise ValueError('Original internal reference differs: ' + field)
                        else:
                            if identity not in materials or not actual.get('guid'):
                                raise ValueError('Unmigrated external performance reference: ' + identity + field)
                            target = materials[identity]
                            replacement = ref(2100000, meta(target), 2)
                            if actual['guid'] in external and external[actual['guid']] != replacement:
                                raise ValueError('Ambiguous original exported reference')
                            external[actual['guid']] = replacement
                            inputs.extend([target, Path(str(target) + '.meta')])
                    counts['references'] += 1
                    return
                if not isinstance(actual, dict) or set(expected) - set(actual):
                    raise ValueError('Original field names differ: ' + owner['key'] + field + ' ' + str(set(expected) - set(actual)))
                for name, value in expected.items():
                    compare(value, actual[name], owner, field + '.' + name, tokens + (name,), file_id, repair)
            elif isinstance(expected, list):
                if field.endswith(('.m_VertexStreams', '.m_TrailVertexStreams')) and not isinstance(actual, list):
                    scalar = str(getattr(actual, 'scalar', actual))
                    if len(scalar) != len(expected) * 2:
                        raise ValueError('Original packed vertex stream extent differs')
                    actual = list(bytes.fromhex(scalar))
                    counts['packed_vertex_streams'] += 1
                if actual is None and not expected:
                    actual = []
                if not isinstance(actual, list) or len(expected) != len(actual):
                    raise ValueError('Original array differs: ' + field)
                for i, (left, right) in enumerate(zip(expected, actual)):
                    compare(left, right, owner, field + '[' + str(i) + ']', tokens + (i,), file_id, repair)
            elif isinstance(expected, float):
                if struct.pack('<f', expected) != struct.pack('<f', actual):
                    if repair and expected == actual == 0 and struct.pack('<f', expected) == bytes.fromhex('00000080'):
                        signed_zeros.setdefault(file_id, []).append(tokens)
                    else:
                        raise ValueError('Original Float32 bits differ: ' + owner['key'] + field + repr((expected, actual)))
            elif expected != actual:
                raise ValueError('Original value differs: ' + owner['key'] + field + repr((expected, actual)))
            counts['values'] += 1

        omitted = {'m_LightmapIndex', 'm_LightmapIndexDynamic', 'm_LightmapTilingOffset', 'm_LightmapTilingOffsetDynamic'}
        for identity in native_ids:
            owner = objects[identity]
            file_id = paired[identity]
            actual = docs[file_id][1]
            if owner['type'] == 'ParticleSystemRenderer' and set(owner['fields']) - set(actual) == omitted:
                values = {name: owner['fields'][name] for name in sorted(omitted)}
                if any(values[n] != 65535 for n in ['m_LightmapIndex', 'm_LightmapIndexDynamic']) or any(
                        values[n] != dict(x=1., y=1., z=0., w=0.) for n in ['m_LightmapTilingOffset', 'm_LightmapTilingOffsetDynamic']):
                    raise ValueError('Inspect original nondefault lightmap fields')
                prefix = dump(values)
                prefix = ''.join('  ' + line + '\n' for line in prefix.splitlines())
                text, changed = re.subn(r'(?m)(^--- !u!199 &' + str(file_id) + r'\nParticleSystemRenderer:\n)', lambda m: m[1] + prefix, text)
                if changed != 1:
                    raise ValueError('Cannot restore original renderer fields')
                actual = dict(actual, **values)
                counts['restored_lightmap_fields'] += len(values)
            compare(owner['fields'], actual, owner, file_id=file_id)
        for identity in prefab['game_objects']:
            owner = objects[identity]
            actual = docs[paired[identity]][1]
            if owner['fields']['m_Tag'] != 0 or actual['m_TagString'] != 'Untagged':
                raise ValueError('Original performance tag needs explicit migration')
            compare({k: v for k, v in owner['fields'].items() if k != 'm_Tag'}, actual,
                    owner, file_id=paired[identity])
        for file_id, paths in signed_zeros.items():
            match = re.search(r'(?ms)(^--- !u!\d+ &' + str(file_id) + r'\n)(.*?)(?=^--- !u!|\Z)', text)
            body = match[2]
            root = yaml.compose(body).value[0][1]
            edits = []
            for tokens in paths:
                node = root
                for token in tokens:
                    node = node.value[token] if isinstance(token, int) else next(v for k, v in node.value if k.value == token)
                if node.value not in ('0', '-0', '0.0'):
                    raise ValueError('Inspect native negative-zero spelling')
                edits.append((node.start_mark.index, node.end_mark.index))
            for start, end in sorted(edits, reverse=True):
                body = body[:start] + '-0.0' + body[end:]
            text = text[:match.start(2)] + body + text[match.end(2):]
            counts['restored_signed_zeros'] += len(paths)
        docs = documents(text)
        for identity in native_ids:
            compare(objects[identity]['fields'], docs[paired[identity]][1], objects[identity], file_id=paired[identity], repair=False)

        def pointer(value, owner, field=''):
            if isinstance(value, dict):
                if set(value) == {'m_FileID', 'm_PathID'}:
                    if not value['m_PathID']:
                        return ref()
                    identity = edge(owner, field)
                    if identity not in paired:
                        raise ValueError('Unbound original managed reference: ' + identity + field)
                    return ref(paired[identity])
                converted = {k: pointer(v, owner, field + '.' + k) for k, v in value.items()}
                # These engine YAML schema versions do not occur as fields in
                # a managed binary type tree. Use the selected Unity 2022
                # Gradient/AnimationCurve/Keyframe serialization schema.
                if set(value) == {'m_Curve', 'm_PreInfinity', 'm_PostInfinity', 'm_RotationOrder'}:
                    converted = dict(serializedVersion=2, **converted)
                    converted['m_Curve'] = [dict(serializedVersion=3, **v) for v in converted['m_Curve']]
                elif 'm_NumColorKeys' in value and 'm_NumAlphaKeys' in value and 'key7' in value:
                    converted = dict(serializedVersion=2, **converted)
                return converted
            if isinstance(value, list):
                return [pointer(v, owner, field + '[' + str(i) + ']') for i, v in enumerate(value)]
            return value

        for identity, file_id in paired.items():
            owner = objects[identity]
            kind = owner['type'].rsplit('.', 1)[-1]
            if owner['type'] in ['GameObject', 'Transform', 'ParticleSystem', 'ParticleSystemRenderer']:
                continue
            fields = pointer({k: v for k, v in owner['fields'].items() if k != 'm_Script'}, owner)
            fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=ref(), m_PrefabInstance=ref(),
                          m_PrefabAsset=ref(), m_GameObject=fields.pop('m_GameObject'),
                          m_Enabled=fields.pop('m_Enabled'), m_EditorHideFlags=0,
                          m_Script=ref(11500000, scripts[kind]), m_Name=fields.pop('m_Name'),
                          m_EditorClassIdentifier='', **fields)
            block = '--- !u!114 &' + str(file_id) + '\n' + dump(dict(MonoBehaviour=fields))
            text, changed = re.subn(r'(?ms)^--- !u!114 &' + str(file_id) + r'\n.*?(?=^--- !u!|\Z)', lambda m: block, text)
            if changed != 1:
                raise ValueError('Cannot restore original application component')
            counts[kind] += 1
        for guid, replacement in external.items():
            target = '{fileID: ' + str(replacement['fileID']) + ', guid: ' + replacement['guid'] + ', type: 2}'
            text, changed = re.subn(r'\{fileID: -?\d+, guid: ' + guid + r', type: \d+\}', lambda m: target, text)
            if not changed:
                raise ValueError('Cannot bind original Windows material')
        # Unity runs the original OnValidate while importing components. Emit
        # the sequence after its referenced playables, preserving the original
        # GameObject component order and all reference identities.
        headers = re.split(r'(?m)(^--- !u!\d+ &-?\d+\n)', text)
        sequence_ids = {paired[i] for i in paired if short_type(i) == 'MyPlayableSequence'}
        blocks = [headers[i] + headers[i + 1] for i in range(1, len(headers), 2)]
        text = headers[0] + ''.join(b for b in blocks if int(re.search(r'&(-?\d+)', b)[1]) not in sequence_ids)
        text += ''.join(b for b in blocks if int(re.search(r'&(-?\d+)', b)[1]) in sequence_ids)
        destination = DEST / (prefab['name'] + '.prefab')
        write(destination, text.encode('utf-8'))
        meta(destination, 'PrefabImporter')
        outputs.extend([destination, Path(str(destination) + '.meta')])
        prefabs.append(dict(name=prefab['name'], root=prefab['root'], path=destination.relative_to(ROOT).as_posix(),
                            native_export_sha256=sha(path.read_bytes()),
                            objects=len(paired), bindings=[dict(source=k, fileID=v) for k, v in paired.items()]))
        counts['objects'] += len(paired)
        counts['ParticleSystem'] += sum(short_type(i) == 'ParticleSystem' for i in paired)
        print('Restored original tree performance:', prefab['name'], len(paired), flush=True)
    if counts['objects'] != 205 or counts['ParticleSystem'] != 27:
        raise ValueError('Original tree performance coverage changed')
    meta(DEST, folder=True)
    outputs.append(Path(str(DEST) + '.meta'))
    write(PROOF, encode(dict(manifest=source['manifest'], mapClassId=13, prefabs=prefabs, counts=dict(counts),
        method='Pair every original object by ordered native hierarchy and component type. Verify all native Transform and particle fields, Float32 bits, array ordering and PPtrs; restore only verified omitted default lightmap fields and negative-zero spellings. Restore every original managed field from verified official type trees and bind migrated original application scripts and existing verified Windows materials. Sequence components inherit the complete original implementation; their Editor-only adapter defers the exact original OnValidate diagnostic until prefab cross-object import resolves. Runtime order and all sequence methods remain inherited unchanged. No converter script implementation is used.',
        native_conversion=dict(manifest=conversion_manifest['manifest'], roots=conversion_manifest['roots'],
            inputs=[{k: v for k, v in row.items() if k != 'converter_input_path'} for row in conversion_manifest['inputs']]),
        inputs=[record(p) for p in sorted(set(inputs))], tools=[record(p) for p in [Path(__file__).resolve(),
            ROOT / '.tools/assetripper-1.3.14/AssetRipper.GUI.Free.exe', ROOT / '.tools/assetripper-1.3.14/capstone.dll']],
        outputs=[record(p) for p in sorted(set(outputs))])))
    print(dict(counts))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--conversion', type=Path, required=True)
    build(parser.parse_args().conversion)
