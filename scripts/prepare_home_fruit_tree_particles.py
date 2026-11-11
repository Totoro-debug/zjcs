"""Restore selected official fruit-tree particles and complete Windows resources."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path

import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from prepare_current_partner_native import documents
from prepare_fruit_tree_unity_assets import retain_particle_hierarchy
from prepare_partner_card_native import PartnerCardBinding, Dumper, sha
from validation_workspace import validation_directory

NATIVE = 'Prefabs/Home/Dynamic/Furniture/FruitTree/FruitTree.prefab'
BUNDLE = 'defaultpackage_assets_prefabs_home_dynamic_furniture_fruittree.bundle'
DEST = ROOT / 'unity/Assets/Official/Home/CurrentFruitTree'
SHADERS = ROOT / 'unity/Assets/Game/Runtime/Presentation/Home/Shaders'
RECEIPT = ROOT / 'assets/reference/home-fruit-tree-particles-source-manifest.json'


def stable(value):
    return hashlib.md5(('zjcs:home-fruit-current:' + value).encode()).hexdigest()


class FruitParticles(PartnerCardBinding):
    def register_scripts(self):
        self.script_guids = set()
        self.original_refs = {}

    def load(self, source_guid):
        obj = self.objects[source_guid]
        if obj.type.name == 'Shader':
            from prepare_current_partner_native import identity
            row = self.shaders[identity(obj)]
            assert not row['stripped_passes'], 'Restore every official particle shader pass'
            path = self.cache / 'all-assets/windows-shaders' / row['path']
            self.inputs[path.relative_to(ROOT).as_posix()] = row['sha256']
        return super().load(source_guid)

    def build(self):
        recovery = self.recovery
        recovery.recover('FruitTree', self.work / 'fruit-native', bundle=BUNDLE, native_relative=NATIVE)
        native, _, panel, original_record = recovery.outputs[0]
        raw, counts = retain_particle_hierarchy(native)
        selected = documents(raw.decode('utf8'))
        for key, file_id in panel['objectMap'].items():
            if file_id not in selected:
                continue
            owner = self.original(key)
            self.learn(recovery.fields(owner), next(iter(selected[file_id].values())), owner)
        # Keep native particle curves, modules, packed vertex streams and text
        # serialization headers byte-for-byte. Only external GUIDs are rebound.
        pointers = {}
        for guid in set(re.findall(r'guid: ([a-f0-9]{32})', raw.decode())):
            if guid.startswith('0000000000000000'):
                continue
            pointers[guid] = self.load(guid)
        pending = 0
        while pending < len(self.queue):
            _, _, payload = self.queue[pending]
            if isinstance(payload, dict):
                converted = self.replace(payload)
                payload.clear()
                payload.update(converted)
            pending += 1
        root_guid = stable('Particles.prefab')
        root = re.sub(r'(fileID: -?\d+, guid: )([a-f0-9]{32})(, type: )\d+',
            lambda m: m[0] if m[2] not in pointers else
                'fileID: ' + str(pointers[m[2]]['fileID']) + ', guid: ' + pointers[m[2]]['guid'] +
                ', type: ' + str(pointers[m[2]]['type']), raw.decode())
        outputs = [(Path('Resources/Home/FruitTree/Particles.prefab'), root_guid, root.encode())]
        names = {old: stable(old) for _, old, _ in self.queue}
        native_meshes = {row['output']: ROOT / row['native_path'] for row in self.proof.values() if row['type'] == 'Mesh'}
        for target, old, payload in self.queue:
            if target.as_posix() in native_meshes:
                # Packed mesh buffers and empty byte/stream fields must keep
                # Unity's native text representation, not YAML null strings.
                payload = native_meshes[target.as_posix()].read_bytes()
                assert not re.search(rb'guid: [a-f0-9]{32}', payload), 'Bind additional native mesh external references'
            if isinstance(payload, dict):
                text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id, content in payload.items():
                    kind, = content
                    text += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                    text += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
                payload = text.encode()
            if target.suffix == '.shader':
                text = payload.decode()
                text, count = re.subn(r'^Shader "[^"]+"',
                    'Shader "Zjcs/Official/Home/FruitTree/' + target.stem + '"', text, count=1)
                assert count == 1 and 'DummyShaderTextExporter' not in text
                payload = text.encode()
            outputs.append((target, names[old], payload))
        staged = self.work / 'fruit-particles-assets'
        records = {}
        for relative, guid, payload in outputs:
            destination = SHADERS / ('FruitTree_' + relative.name) if relative.suffix == '.shader' else DEST / relative
            if relative.suffix not in ('.ttf', '.otf'):
                text = payload.decode('utf8')
                text = re.sub(r'guid: ([a-f0-9]{32})', lambda m: 'guid: ' + names.get(m[1], m[1]), text)
                payload = text.encode()
            path = staged / destination.relative_to(ROOT)
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(payload)
            importer = 'ShaderImporter' if path.suffix == '.shader' else 'PrefabImporter' if path.suffix == '.prefab' else 'NativeFormatImporter'
            meta = ('fileFormatVersion: 2\nguid: ' + guid + '\n' + importer + ':\n  externalObjects: {}\n').encode()
            Path(str(path) + '.meta').write_bytes(meta)
            records[destination.relative_to(ROOT).as_posix()] = sha(payload)
            records[destination.relative_to(ROOT).as_posix() + '.meta'] = sha(meta)
            parent = destination.parent
            while parent != ROOT / 'unity/Assets':
                sidecar = Path(str(parent) + '.meta')
                owned = sidecar.exists() and re.search(rb'(?m)^guid: ([a-f0-9]{32})', sidecar.read_bytes())[1].decode() == stable(parent.relative_to(ROOT).as_posix())
                if (not sidecar.exists() or owned) and sidecar.relative_to(ROOT).as_posix() not in records:
                    data = ('fileFormatVersion: 2\nguid: ' + stable(parent.relative_to(ROOT).as_posix()) +
                        '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
                    temp = staged / sidecar.relative_to(ROOT)
                    temp.parent.mkdir(parents=True, exist_ok=True)
                    temp.write_bytes(data)
                    records[sidecar.relative_to(ROOT).as_posix()] = sha(data)
                parent = parent.parent
        for path in (native, Path(str(native) + '.meta'), Path(__file__),
                ROOT / 'scripts/prepare_fruit_tree_unity_assets.py', ROOT / 'scripts/prepare_partner_card_native.py',
                ROOT / 'scripts/prepare_home_fruit_tree_particles_validation.py',
                ROOT / 'scripts/fixtures/home_furniture_host_validation.cs'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt = dict(manifest=recovery.apk.version, original=original_record,
            counts=counts, inputs=self.inputs, objects=self.proof, outputs=records,
            resource='Home/FruitTree/Particles',
            operation='Retain current official particle hierarchy and exact native modules; omit non-particle scene/scripts. Resolve material/mesh PPtrs against original CAB identities and restore verified complete Windows shader programs and textures.',
            limits=['Non-particle fruit-tree scene, skeleton and client state binding are separate.'])
        (self.work / 'home-fruit-tree-particles-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Prepared current home fruit particles:', counts, len(records), 'files', flush=True)


def install(work):
    receipt = json.loads((work / 'home-fruit-tree-particles-source-manifest.json').read_bytes())
    for relative, digest in receipt['inputs'].items():
        assert sha((ROOT / relative).read_bytes()) == digest, relative
    for relative, digest in receipt['outputs'].items():
        source = work / 'fruit-particles-assets' / relative
        assert sha(source.read_bytes()) == digest, relative
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    for relative in receipt['outputs']:
        path = ROOT / relative
        payload = (work / 'fruit-particles-assets' / relative).read_bytes()
        if path.exists() and path.read_bytes() != payload:
            assert relative in previous and sha(path.read_bytes()) == previous[relative], relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(payload)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    if args.install:
        install(work)
    else:
        FruitParticles(work).build()
