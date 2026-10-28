"""Restore Map13 prop Mecanim assets from original compiled native objects.

Only temporary outer object names change during conversion to preserve CAB/PID
identity through AssetRipper. Original names and the original dummy binding are
restored before import; no geometry is baked into these clock clips.
"""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import struct
import uuid
import zlib

from official_apk import Apk, ROOT
import UnityPy
from UnityPy.helpers.Tpk import get_typetree_node
from official_native_yaml_verification import documents
from prepare_unity_map13_water_entity_export import convert

CACHE = ROOT / '.tools/map13-prop-animators-native'
SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source'
DEST = ROOT / 'unity/Assets/Official/World/Map/13/NativePropAnimators'
RECEIPT = ROOT / 'assets/reference/official-map/map13-native-prop-animators-source-manifest.json'
GUID = re.compile(r'(?m)^guid: ([a-f0-9]{32})$')
SOURCE_RECEIPTS = [ROOT / 'assets/reference/official-map' / name for name in (
    'map13-entity-skeletons-source-manifest.json', 'map13-native-entity-object-graph-source-manifest.json')]


def sha(raw): return hashlib.sha256(raw).hexdigest()


def write(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != raw:
        temp = CACHE / 'write.tmp'
        temp.parent.mkdir(parents=True, exist_ok=True)
        temp.write_bytes(raw)
        temp.replace(path)


def encode(data):
    return (json.dumps(data, ensure_ascii=False, indent=2, allow_nan=False,
                       default=lambda b: dict(bytes_hex=bytes(b).hex())) + '\n').encode()


def identity(cab, pid): return sha((cab.lower() + ':' + str(pid)).encode())[:24]


def stage():
    apk = Apk.current()
    for receipt, path in zip(SOURCE_RECEIPTS, (SOURCE / 'native-entity-skeletons.json', SOURCE / 'native-entity-object-graph.json')):
        proof = json.loads(receipt.read_bytes())
        record = next(r for r in proof['outputs'] if r['path'] == path.relative_to(ROOT).as_posix())
        if sha(path.read_bytes()) != record['sha256']:
            raise ValueError('Map13 original source receipt changed: ' + record['path'])
    skeletons = json.loads((SOURCE / 'native-entity-skeletons.json').read_bytes())
    graph = json.loads((SOURCE / 'native-entity-object-graph.json').read_bytes())
    if apk.version != graph['manifest'] or apk.version != skeletons['manifest']:
        raise ValueError('Selected original source version differs from Map13')
    objects = {(r['cab'].lower(), r['path_id']): r for r in graph['objects']}
    models, controllers = [], {}
    for model in skeletons['skeletons']:
        if not model['name'].startswith('map_'):
            continue
        refs = [r for r in model['references'] if r['field'] == '.controller']
        if len(refs) != 1:
            raise ValueError('Prop controller identity must be explicit')
        ref = refs[0]
        key = ref['cab'].lower(), ref['path_id']
        controllers[key] = objects[key]
        models.append(dict(name=model['name'], cab=model['cab'], path_id=model['path_id'],
                           controller_key=identity(*key)))
    records, inputs = [], []
    for key, source in controllers.items():
        selected = [obj for obj in apk.objects(source['input'])
                    if obj.assets_file.name.lower() == key[0] and
                    obj.type.name in ('AnimationClip', 'AnimatorController')]
        file = selected[0].assets_file
        expected, types, indices = {}, [], {}
        for obj in selected:
            raw = obj.get_raw_data()
            fields = obj.read_typetree(check_read=True)
            objkey = identity(key[0], obj.path_id)
            recorded = objects.get((key[0], str(obj.path_id)))
            if recorded and sha(raw) != recorded['payload_sha256']:
                raise ValueError('Original controller differs from source object graph')
            # A compiled NamedObject starts with one aligned UTF-8 string.
            name = fields['m_Name']
            length, = struct.unpack_from('<I', raw)
            if raw[4:4 + length].decode() != name:
                raise ValueError('Unexpected native NamedObject layout')
            tag = 'Map13_' + objkey + '__' + name
            prefix = struct.pack('<I', len(tag.encode())) + tag.encode()
            tagged = prefix + bytes((-len(prefix)) % 4) + raw[(4 + length + 3) & ~3:]
            obj.set_raw_data(tagged)
            expected[str(obj.path_id)] = sha(tagged)
            records.append(dict(key=objkey, cab=file.name, path_id=str(obj.path_id),
                kind=obj.type.name, source=apk.provenance(obj), payload_sha256=sha(raw),
                converter_payload_sha256=sha(tagged), temporary_name=tag, fields=fields))
            if obj.type_id not in indices:
                indices[obj.type_id] = len(types)
                types.append(obj.serialized_type)
        for obj in selected:
            obj.type_id = indices[obj.type_id]
        file.objects = {obj.path_id: obj for obj in selected}
        file.types, file.ref_types, file._enable_type_tree = types, [], True
        for typ in types:
            if typ.node is None:
                typ.node = get_typetree_node(typ.class_id, file.version)
            if typ.type_dependencies is None:
                typ.type_dependencies = ()
            for index, node in enumerate(typ.node.traverse()):
                node.m_Index = index
                for field in ('m_TypeFlags', 'm_RefTypeHash', 'm_MetaFlag'):
                    setattr(node, field, getattr(node, field) or 0)
                node.m_Version = node.m_Version or 1
        raw = file.save()
        loaded = UnityPy.load(raw)
        if {str(o.path_id): sha(o.get_raw_data()) for o in loaded.objects} != expected:
            raise ValueError('Native input serialization changed object payload')
        for obj in loaded.objects:
            record = next(r for r in records if r['cab'] == file.name and r['path_id'] == str(obj.path_id))
            actual = obj.read_typetree(check_read=True)
            if actual.pop('m_Name') != record['temporary_name']:
                raise ValueError('Temporary native identity was not serialized')
            expected_fields = dict(record['fields']); expected_fields.pop('m_Name')
            if actual != expected_fields:
                raise ValueError('Temporary identity changed native object fields')
        write(CACHE / 'inputs' / file.name, raw)
        inputs.append(dict(cab=file.name, sha256=sha(raw), objects=expected))
    result = dict(manifest=apk.version,
                  models=models, objects=records, inputs=inputs)
    write(CACHE / 'originals.json', encode(result))
    print('Original native inputs:', len(controllers), 'controllers,',
          sum(r['kind'] == 'AnimationClip' for r in records), 'clips', flush=True)


def restore():
    source = json.loads((CACHE / 'originals.json').read_bytes())
    exported = CACHE / 'export/ExportedProject/Assets'
    assets, remap = {}, {}
    for record in source['objects']:
        extension = '.controller' if record['kind'] == 'AnimatorController' else '.anim'
        matches = list(exported.rglob(record['temporary_name'] + extension))
        if len(matches) != 1:
            raise ValueError('Native export identity lost: ' + record['temporary_name'])
        path = matches[0]
        oldguid = GUID.search(Path(str(path) + '.meta').read_text(encoding='utf8'))[1]
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:map13-native-prop-animator:' + record['key']).hex
        remap[oldguid] = guid
        assets[record['key']] = path, record, guid
    outputs = []
    for path, record, guid in assets.values():
        text = path.read_text(encoding='utf8')
        text, count = re.subn(r'(?m)^  m_Name: ' + re.escape(record['temporary_name']) + '$',
                              '  m_Name: ' + record['fields']['m_Name'], text)
        if count != 1:
            raise ValueError('Temporary native name was not unique')
        if record['kind'] == 'AnimationClip':
            # CRC32 recovers the real dummy property; random converter fallback
            # names would cause Unity to compile a different attribute hash.
            if zlib.crc32(b'dummy') != 1341452863:
                raise ValueError('Original dummy binding hash changed')
            text = re.sub(r'(?m)^(\s*attribute:) missed_0x4FF4F23F_[^\n]+$', r'\1 dummy', text)
        for old, new in remap.items():
            text = text.replace('guid: ' + old, 'guid: ' + new)
        refs = re.findall(r'guid: ([a-f0-9]{32})', text)
        if any(ref not in remap.values() for ref in refs):
            raise ValueError('Unbound native animator reference')
        folder = 'Controllers' if record['kind'] == 'AnimatorController' else 'Clips'
        target = DEST / folder / (record['key'] + path.suffix)
        write(target, text.encode())
        write(Path(str(target) + '.meta'), ('fileFormatVersion: 2\nguid: ' + guid +
              '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' +
              ('9100000' if record['kind'] == 'AnimatorController' else '7400000') + '\n').encode())
        docs = documents(text)
        root = next(v for kind, v in docs.values() if kind == record['kind'])
        if root['m_Name'] != record['fields']['m_Name']:
            raise ValueError('Original native name changed')
        outputs.append(dict(key=record['key'], path=target.relative_to(ROOT).as_posix(),
                            sha256=sha(target.read_bytes()),
                            meta_sha256=sha(Path(str(target) + '.meta').read_bytes()), guid=guid))
    # Keep the complete compiled fields for verification and explicit bindings.
    catalog = DEST / 'original-native-animators.json'
    write(catalog, encode(source))
    for folder in (DEST, DEST / 'Clips', DEST / 'Controllers'):
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:map13-native-prop-animator-folder:' +
                          folder.relative_to(ROOT).as_posix()).hex
        write(Path(str(folder) + '.meta'), ('fileFormatVersion: 2\nguid: ' + guid +
              '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode())
    catalog_guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:map13-native-prop-animator-catalog').hex
    write(Path(str(catalog) + '.meta'), ('fileFormatVersion: 2\nguid: ' + catalog_guid +
          '\nTextScriptImporter:\n  externalObjects: {}\n').encode())
    cases = []
    for record in source['objects']:
        f = record['fields']
        case = dict(key=record['key'], kind=record['kind'], name=f['m_Name'],
                    asset_path=next(r['path'][6:].lower() for r in outputs if r['key'] == record['key']),
                    states=[], parameters=[], transitions=[])
        if record['kind'] == 'AnimationClip':
            case.update(duration=f['m_MuscleClip']['m_StopTime'] - f['m_MuscleClip']['m_StartTime'],
                        loop=f['m_MuscleClip']['m_LoopTime'])
        else:
            tos = dict(f['m_TOS']); c = f['m_Controller']
            sm = c['m_StateMachineArray'][0]['data']
            for p in c['m_Values']['data']['m_ValueArray']:
                case['parameters'].append(dict(name=tos[p['m_ID']], type=p['m_Type']))
            states = [s['data'] for s in sm['m_StateConstantArray']]
            for state in states:
                trees = state['m_BlendTreeConstantArray']
                clip = f['m_AnimationClips'][trees[0]['data']['m_NodeArray'][0]['data']['m_ClipID']] if trees else None
                case['states'].append(dict(name=tos[state['m_NameID']], hash=state['m_FullPathID'],
                    clip_key=identity(record['cab'], clip['m_PathID']) if clip else '',
                    speed=state['m_Speed'], loop=state['m_Loop']))
            for index, transitions in [(-1, sm['m_AnyStateTransitionConstantArray'])] + [
                    (i, state['m_TransitionConstantArray']) for i, state in enumerate(states)]:
                for t in transitions:
                    v = t['data']
                    case['transitions'].append(dict(source=index, destination=v['m_DestinationState'],
                        duration=v['m_TransitionDuration'], fixed_duration=v['m_HasFixedDuration'],
                        exit_time=v['m_ExitTime'], has_exit_time=v['m_HasExitTime'],
                        conditions=[dict(name=tos[k['data']['m_EventID']], mode=k['data']['m_ConditionMode'],
                                         threshold=k['data']['m_EventThreshold']) for k in v['m_ConditionConstantArray']]))
        cases.append(case)
    case_path = DEST / 'execution-cases.json'
    write(case_path, encode(dict(entries=cases, models=source['models'])))
    write(Path(str(case_path) + '.meta'), ('fileFormatVersion: 2\nguid: ' +
          uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:map13-native-prop-animator-cases').hex +
          '\nTextScriptImporter:\n  externalObjects: {}\n').encode())
    receipt = dict(manifest=source['manifest'], inputs=source['inputs'], outputs=outputs,
                   source=dict(path=catalog.relative_to(ROOT).as_posix(), sha256=sha(catalog.read_bytes())),
                   execution_cases=dict(path=case_path.relative_to(ROOT).as_posix(), sha256=sha(case_path.read_bytes())),
                   tools=[dict(path=Path(__file__).relative_to(ROOT).as_posix(),
                               sha256=sha(Path(__file__).read_bytes()))],
                   conversion='Original compiled native controllers/clips; temporary CAB/PID names restored; '
                              'dummy property recovered by exact CRC32; no geometry baking.',
                   limitations='Prefab binding, skeleton mixing bridge and full-map acceptance remain pending.')
    dependencies = [SOURCE / 'native-entity-skeletons.json', SOURCE / 'native-entity-object-graph.json',
                    *SOURCE_RECEIPTS, ROOT / 'official-source.json']
    receipt['dependencies'] = [dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes())) for p in dependencies]
    receipt['converter'] = dict(path='.tools/assetripper-1.3.14/AssetRipper.GUI.Free.exe',
        sha256=sha((ROOT / '.tools/assetripper-1.3.14/AssetRipper.GUI.Free.exe').read_bytes()), version='1.3.14')
    write(RECEIPT, encode(receipt))
    print('Restored original native animator assets:', len(outputs), flush=True)


def pin_windows_bundle(roundtrip):
    """Keep original native polynomial coefficients after a real Windows build.

    Unity's normal authoring-curve recompilation changes a few least significant
    bits in one clip. All original platform-neutral AnimationClip bytes are
    restored here. Controllers only change their explicit local clip PPtrs.
    The native Windows file header, type metadata and container are Unity-built.
    """
    source = json.loads((CACHE / 'originals.json').read_bytes())
    originals = {r['key']: r for r in source['objects']}
    raw = Path(roundtrip).read_bytes()
    env = UnityPy.load(raw)
    bundle = next(iter(env.files.values()))
    native = next(iter(bundle.files.values()))
    if int(native.target_platform) != 19:
        raise ValueError('Actual Unity StandaloneWindows64 build is required')
    container = next(o for o in env.objects if o.type.name == 'AssetBundle').read_typetree(check_read=True)
    refs = {Path(path).stem: value['asset']['m_PathID'] for path, value in container['m_Container']}
    if set(refs) != set(originals):
        raise ValueError('Windows container does not contain every original native animator asset')
    loaded_sources, patched = {}, []
    object_map = {obj.path_id: obj for obj in env.objects}
    for key, record in originals.items():
        cab = record['cab']
        if cab not in loaded_sources:
            loaded_sources[cab] = UnityPy.load(str(CACHE / 'inputs' / cab))
        obj = next(o for o in loaded_sources[cab].objects if str(o.path_id) == record['path_id'])
        serialized = obj.save_typetree(record['fields'])
        if sha(serialized) != record['payload_sha256']:
            raise ValueError('Original native binary serialization is not exact')
        data = copy.deepcopy(record['fields'])
        if record['kind'] == 'AnimatorController':
            if data['m_StateMachineBehaviours']:
                raise ValueError('Original controller behaviour requires explicit binding')
            for ref in data['m_AnimationClips']:
                if ref['m_FileID'] != 0:
                    raise ValueError('External native clock clip requires explicit mapping')
                ref['m_PathID'] = refs[identity(cab, ref['m_PathID'])]
            serialized = obj.save_typetree(data)
        elif data['m_Events'] or data['m_PPtrCurves'] or data['m_ClipBindingConstant']['pptrCurveMapping']:
            raise ValueError('Original clip contains additional native object pointers')
        target = object_map[refs[key]]
        if target.type.name != record['kind']:
            raise ValueError('Windows native type identity changed')
        target.set_raw_data(serialized)
        patched.append(dict(key=key, source_payload_sha256=record['payload_sha256'],
                            windows_path_id=str(target.path_id), windows_payload_sha256=sha(serialized)))
    result = bundle.save()
    reloaded = UnityPy.load(result)
    verify_objects = {str(o.path_id): o for o in reloaded.objects}
    for record in patched:
        obj = verify_objects[record['windows_path_id']]
        if sha(obj.get_raw_data()) != record['windows_payload_sha256']:
            raise ValueError('Windows native bundle did not retain pinned animation bytes')
        # Check complete consumption using the Windows file's own type metadata.
        obj.read_typetree(check_read=True)
    target = DEST / 'map13-prop-animators.windows.bundle.bytes'
    write(target, result)
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:map13-native-prop-animator-windows-bundle').hex
    write(Path(str(target) + '.meta'), ('fileFormatVersion: 2\nguid: ' + guid +
          '\nTextScriptImporter:\n  externalObjects: {}\n').encode())
    receipt = json.loads(RECEIPT.read_bytes())
    receipt['native_windows_bundle'] = dict(path=target.relative_to(ROOT).as_posix(), sha256=sha(result),
        meta_sha256=sha(Path(str(target) + '.meta').read_bytes()), unity_windows_build_sha256=sha(raw),
        platform=19, objects=patched,
        method='Unity StandaloneWindows64 build; original native clip bytes; original controllers with only local clip pointer relocation.')
    receipt['tools'] = [dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha(Path(__file__).read_bytes()))]
    write(RECEIPT, encode(receipt))
    print('Pinned original Windows animation payloads:', len(patched), 'assets,', len(result), 'bytes', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage', action='store_true')
    parser.add_argument('--convert', action='store_true')
    parser.add_argument('--restore', action='store_true')
    parser.add_argument('--pin-windows-bundle', type=Path)
    args = parser.parse_args()
    if args.stage: stage()
    if args.convert: convert(CACHE)
    if args.restore: restore()
    if args.pin_windows_bundle: pin_windows_bundle(args.pin_windows_bundle)
