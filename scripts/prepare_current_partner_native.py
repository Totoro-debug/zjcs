"""Recover current official partner prefab fields in converter-native hierarchies.

This stage retains original script identities. Runtime script/resource binding is a
separate required stage; these files are never installed as playable prefabs.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import struct

from Crypto.Hash import MD4
import yaml
from UnityPy.classes.PPtr import PPtr

from official_apk import Apk, ROOT
from validation_workspace import validation_directory

BUNDLE = 'defaultpackage_assets_prefabs_ui_npc.bundle'
NAMES = ('NpcPanel', 'NpcPanelNpcItem', 'NpcDetailPanel', 'NpcFriendshipDialog',
         'NpcFriendshipPreviewPropItem', 'NpcFriendshipPropItemGroup')


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def identity(obj):
    return obj.assets_file.name.lower() + ':' + str(obj.path_id)


def pointer(raw, owner):
    return PPtr(**raw, assetsfile=owner.assets_file).deref()


def script_id(script):
    return struct.unpack('<i', MD4.new(('s\0\0\0' + script.m_Namespace + script.m_ClassName).encode()).digest()[:4])[0]


def documents(text):
    return {int(file_id): yaml.safe_load(body) for file_id, body in re.findall(
        r'^--- !u!\d+ &(-?\d+)\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S)}


class PartnerNativeRecovery:
    def __init__(self):
        self.apk = Apk.current()
        self.cache = ROOT / '.tools/source-packages/current' / self.apk.version / 'native-conversion'
        self.native = self.cache / 'native-export-all/ExportedProject/Assets'
        self.field_cache = self.cache / 'component-fields-all'
        manifest_path = self.field_cache / 'source-manifest.json'
        manifest = json.loads(manifest_path.read_bytes())
        if manifest['manifest'] != self.apk.version or not manifest['completed']:
            raise ValueError('Current field recovery cache is not complete')
        self.cached = {row['cab'].lower() + ':' + row['path_id']: row for row in manifest['objects']}
        self.objects = self.apk.objects(BUNDLE)
        self.typed = {}
        self.schemas = None
        self.outputs = []
        self.proof = dict(manifest=self.apk.version, bundle=BUNDLE,
            stage='Recovered original hierarchy/fields only; runtime script and resource binding pending',
            generator_sha256=digest(Path(__file__).read_bytes()),
            component_cache=dict(path=manifest_path.relative_to(ROOT).as_posix(), sha256=digest(manifest_path.read_bytes())),
            prefabs=[])

    def fields(self, obj):
        key = identity(obj)
        if key not in self.typed:
            if key in self.cached:
                row = self.cached[key]
                raw = (self.field_cache / row['fields']).read_bytes()
                if digest(raw) != row['sha256'] or digest(obj.get_raw_data()) != row['raw_sha256']:
                    raise ValueError('Recovered official fields changed: ' + key)
                self.typed[key] = json.loads(raw)
            else:
                try:
                    self.typed[key] = obj.read_typetree()
                except ValueError:
                    # Export-supported components can still have stripped bundle
                    # type trees. Decode their exact active assembly descriptors.
                    if self.schemas is None:
                        from current_component_schemas import CurrentSchemas
                        self.schemas = CurrentSchemas()
                    self.typed[key] = self.schemas.read(obj)
        return self.typed[key]

    def recover(self, name, destination):
        original, = [obj for obj in self.objects if obj.type.name == 'GameObject' and obj.read().m_Name == name]
        path = self.native / 'Prefabs/UI/Npc' / (name + '.prefab')
        text = path.read_text('utf8')
        docs = documents(text)
        native_roots = []
        for file_id, doc in docs.items():
            go = doc.get('GameObject')
            if go is None or go['m_Name'] != name:
                continue
            transform = docs[go['m_Component'][0]['component']['fileID']]
            if next(iter(transform.values()))['m_Father']['fileID'] == 0:
                native_roots.append(file_id)
        native_root, = native_roots
        mapping, pairs = {}, []

        def bind(obj, native_id):
            key = identity(obj)
            if key in mapping and mapping[key] != native_id:
                raise ValueError('One source object maps to two native objects: ' + key)
            mapping[key] = native_id
            pairs.append((obj, native_id))

        def visit(go_obj, native_id, hierarchy):
            go = go_obj.read()
            native_go = docs[native_id]['GameObject']
            if go.m_Name != native_go['m_Name']:
                raise ValueError('Original/native hierarchy differs at ' + hierarchy)
            bind(go_obj, native_id)
            source_components = [pair.component.deref() for pair in go.m_Component]
            native_components = [pair['component']['fileID'] for pair in native_go['m_Component']]
            if len(source_components) != len(native_components):
                raise ValueError('Original/native component count differs at ' + hierarchy)
            for component, target in zip(source_components, native_components):
                kind = next(iter(docs[target]))
                if kind != component.type.name:
                    raise ValueError('Original/native component order differs at ' + hierarchy)
                if kind == 'MonoBehaviour':
                    script = component.read(check_read=False).m_Script.read()
                    if docs[target][kind]['m_Script']['fileID'] != script_id(script):
                        raise ValueError('Original/native script order differs at ' + hierarchy)
                bind(component, target)
            source_transform = source_components[0].read()
            native_transform = next(iter(docs[native_components[0]].values()))
            if len(source_transform.m_Children) != len(native_transform['m_Children']):
                raise ValueError('Original/native child count differs at ' + hierarchy)
            for child, target in zip(source_transform.m_Children, native_transform['m_Children']):
                child_go = child.read().m_GameObject.deref()
                target_go = next(iter(docs[target['fileID']].values()))['m_GameObject']['fileID']
                visit(child_go, target_go, hierarchy + '/' + child_go.read().m_Name)

        visit(original, native_root, name)
        external, unresolved = {}, {}

        def learn(source, native, owner):
            if isinstance(source, dict) and 'm_PathID' in source and 'm_FileID' in source:
                if source['m_PathID'] and isinstance(native, dict) and 'guid' in native:
                    key = identity(pointer(source, owner))
                    if key in external and external[key] != native:
                        raise ValueError('Native external pointer is inconsistent: ' + key)
                    external[key] = native
            elif isinstance(source, dict) and isinstance(native, dict):
                for key in source.keys() & native.keys():
                    if key != 'm_Script':
                        learn(source[key], native[key], owner)
            elif isinstance(source, list) and isinstance(native, list) and len(source) == len(native):
                for left, right in zip(source, native):
                    learn(left, right, owner)

        for obj, target in pairs:
            if obj.type.name == 'MonoBehaviour':
                learn(self.fields(obj), docs[target]['MonoBehaviour'], obj)

        def convert(value, owner):
            if isinstance(value, dict) and 'm_PathID' in value and 'm_FileID' in value:
                if not value['m_PathID']:
                    return dict(fileID=0)
                obj = pointer(value, owner)
                key = identity(obj)
                if key in mapping:
                    return dict(fileID=mapping[key])
                if key in external:
                    return external[key]
                unresolved[key] = dict(type=obj.type.name, payload_sha256=digest(obj.get_raw_data()),
                    name=getattr(obj.read(check_read=False), 'm_Name', ''))
                # Keep the original identity in the intermediate source document.
                return dict(originalCAB=obj.assets_file.name, originalPathID=obj.path_id)
            if isinstance(value, dict):
                return {key: convert(item, owner) for key, item in value.items()}
            if isinstance(value, list):
                return [convert(item, owner) for item in value]
            return value

        recovered, scripts = [], []
        for obj, target in pairs:
            if obj.type.name != 'MonoBehaviour':
                continue
            script = obj.read(check_read=False).m_Script.read()
            scripts.append(dict(nativeFileID=target, original=identity(obj),
                namespace=script.m_Namespace, type=script.m_ClassName,
                assembly=script.m_AssemblyName, scriptFileID=script_id(script)))
            if identity(obj) not in self.cached:
                continue
            fields = self.fields(obj)
            native_fields = docs[target]['MonoBehaviour']
            for key, value in fields.items():
                if key not in ('m_GameObject', 'm_Enabled', 'm_Script', 'm_Name'):
                    native_fields[key] = convert(value, obj)
            recovered.append(dict(original=identity(obj), nativeFileID=target,
                scriptType=self.cached[identity(obj)]['type'], payload_sha256=digest(obj.get_raw_data())))
        destination.mkdir(parents=True, exist_ok=True)
        output = destination / (name + '.json')
        data = dict(documents=docs, root=native_root, external=external,
            unresolved=unresolved, scripts=scripts, bindings=recovered, objectMap=mapping)
        record = dict(name=name, originalRoot=identity(original),
            originalSource=self.apk.provenance(original),
            nativeSource=path.relative_to(ROOT).as_posix(), native_sha256=digest(path.read_bytes()),
            mapped_objects=len(mapping), recovered_components=len(recovered), unresolved=len(unresolved),
            pendingBindings=unresolved,
            output=output.name)
        self.outputs.append((path, output, data, record))
        self.proof['prefabs'].append(record)
        print(name, 'objects', len(mapping), 'recovered', len(recovered), 'external pending', len(unresolved), flush=True)

    def finalize(self):
        # A reference to another recovered prefab must retain that prefab's own
        # GUID/root fileID. It must not become a reference to a similarly named
        # object inside the current hierarchy.
        roots = {}
        for source, output, data, record in self.outputs:
            raw = Path(str(source) + '.meta').read_bytes()
            guid, = re.findall(r'^guid: ([0-9a-f]{32})$', raw.decode('utf8'), re.M)
            roots[record['originalRoot']] = dict(fileID=data['root'], guid=guid, type=3)
            record['nativeMeta_sha256'] = digest(raw)
        for source, output, data, record in self.outputs:
            resolved = {}

            def bind(value):
                if isinstance(value, dict):
                    if set(value) == {'originalCAB', 'originalPathID'}:
                        key = value['originalCAB'].lower() + ':' + str(value['originalPathID'])
                        if key in roots:
                            resolved[key] = roots[key]
                            return roots[key]
                    return {key: bind(item) for key, item in value.items()}
                if isinstance(value, list):
                    return [bind(item) for item in value]
                return value

            data['documents'] = bind(data['documents'])
            for key, native in resolved.items():
                data['unresolved'].pop(key)
                data['external'][key] = native
            record['unresolved'] = len(data['unresolved'])
            output.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', 'utf8')
            record['output_sha256'] = digest(output.read_bytes())
            print(record['name'], 'final external pending', record['unresolved'], flush=True)


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--names', nargs='+', choices=NAMES, default=list(NAMES))
    parser.add_argument('--record', action='store_true', help='Update the repository source receipt; do not install intermediate prefabs')
    args = parser.parse_args()
    work = validation_directory()
    if work == ROOT / 'output/validation' or Path(os.environ.get('ZJCS_REPOSITORY_ROOT', '')).resolve() != ROOT:
        raise ValueError('Set the owned validation workspace and repository environment first')
    recovery = PartnerNativeRecovery()
    for name in args.names:
        recovery.recover(name, work / 'partner-native')
    recovery.finalize()
    record = json.dumps(recovery.proof, ensure_ascii=False, indent=2) + '\n'
    (work / 'partner-native/source-manifest.json').write_text(record, 'utf8')
    if args.record:
        (ROOT / 'assets/reference/partner-native-recovery-source-manifest.json').write_text(record, 'utf8')


if __name__ == '__main__':
    main()
