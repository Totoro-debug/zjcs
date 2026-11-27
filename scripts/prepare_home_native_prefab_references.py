"""Add independently loadable local routes to already verified native home prefabs."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_card_native import guid


RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeNativeAssets'
DEST = ROOT / 'unity/Assets/Game/Resources/Home/NativePrefabs'
RECEIPT = ROOT / 'assets/reference/home-native-prefab-references-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs, inputs, sources, routes = {}, {}, {}, []

    def write(path, raw):
        key = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(key):
            raise ValueError('Preserve another local prefab route edit: ' + key)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs[key] = sha(raw)

    def metadata(path, folder=False):
        token = hashlib.md5(('zjcs:home-native-prefab-reference:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind = 'DefaultImporter' if folder else 'MonoImporter' if path.suffix == '.cs' else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'NativeFormatImporter'
        text = 'fileFormatVersion: 2\nguid: ' + token + '\n' + ('folderAsset: yes\n' if folder else '')
        write(Path(str(path) + '.meta'), (text + kind + ':\n  externalObjects: {}\n').encode())

    metadata(RUNTIME, True); metadata(DEST, True)
    for name in ('HomeNativePrefabReference.cs', 'HomeNativePrefabLoader.cs',
        'HomeNativePrefabFactory.cs', 'HomeNativeWeaponRackServices.cs'):
        path = RUNTIME / name
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes()); metadata(path)
        outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    definition = RUNTIME / 'Zjcs.Offline.HomeNativeAssets.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Offline.HomeNativeAssets', references=[
        'Zjcs.Official.HomeEntityCreation', 'Zjcs.Official.HomeFurniture',
        'Zjcs.Official.ProfessionUI']), indent=2) + '\n').encode()); metadata(definition)
    script_guid = guid(Path(str(RUNTIME / 'HomeNativePrefabReference.cs') + '.meta'))
    for name in ('home-house-native', 'home-storage-native', 'home-bed-native', 'home-diningtable-native', 'home-cart-native', 'home-desk-native', 'home-weapon-parts'):
        path = ROOT / 'assets/reference' / (name + '-source-manifest.json')
        source = json.loads(path.read_bytes()); inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        sources[name] = dict(manifest=source['manifest'], receipt=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))
        prefabs = {}
        for relative, expected in source['outputs'].items():
            if not relative.endswith('.prefab'):
                continue
            prefab = ROOT / relative; meta = Path(str(prefab) + '.meta')
            if sha(prefab.read_bytes()) != expected or sha(meta.read_bytes()) != source['outputs'][relative + '.meta']:
                raise ValueError('Previously migrated native prefab differs: ' + relative)
            key = guid(meta)
            if key in prefabs:
                raise ValueError('Previously migrated prefab GUID is ambiguous: ' + key)
            prefabs[key] = relative
            inputs[relative] = expected; inputs[relative + '.meta'] = source['outputs'][relative + '.meta']
        if name == 'home-weapon-parts':
            selected = []
            for row in source['selections']:
                original = row['asset']['path']
                if not original.startswith('Assets/Prefabs/Appearance/PlayerPart/') or not original.endswith('_Home.prefab'):
                    raise ValueError('Verify the original home weapon address before routing it')
                selected.append((original.removeprefix('Assets/Prefabs/').removesuffix('.prefab'), row['pointer']))
        elif name in ('home-storage-native', 'home-bed-native', 'home-diningtable-native', 'home-cart-native', 'home-desk-native') and 'prefabs' in source:
            selected = [(row['location'], row['pointer']) for row in source['prefabs']]
        else:
            location = 'Home/Dynamic/Theme/House/House' if name == 'home-house-native' else 'Home/Dynamic/Furniture/StorageBox/StorageBox'
            selected = [(location, source['prefab'])]
        if len(selected) != len(prefabs):
            raise ValueError('Route every recovered prefab exactly once: ' + name)
        for location, pointer in selected:
            if pointer['guid'] not in prefabs:
                raise ValueError('Original prefab pointer is outside its verified module')
            if not re.fullmatch(r'[A-Za-z0-9_/]+', location) or any(row['location'] == location for row in routes):
                raise ValueError('Unique original prefab address required: ' + location)
            prefab_path = prefabs[pointer['guid']]
            if not re.search(r'(?m)^--- !u!1 &' + str(pointer['fileID']) + r'\r?$', (ROOT / prefab_path).read_text('utf-8-sig')):
                raise ValueError('Original prefab root identity differs: ' + location)
            token = sha(location.encode()); target = DEST / (token + '.asset')
            raw = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
                '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
                '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
                '  m_Script: {fileID: 11500000, guid: ' + script_guid + ', type: 3}\n'
                '  m_Name: ' + token + '\n  m_EditorClassIdentifier:\n  mOriginalLocation: ' + location + '\n'
                '  mPrefab: {fileID: ' + str(pointer['fileID']) + ', guid: ' + pointer['guid'] + ', type: 3}\n').encode()
            write(target, raw); metadata(target)
            routes.append(dict(location=location, resource='Home/NativePrefabs/' + token,
                reference=target.relative_to(ROOT).as_posix(), prefab=prefab_path, pointer=pointer, source=name))
    for path in (Path(__file__), ROOT / 'scripts/prepare_partner_card_native.py',
        ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionAppearancePartPaths.cs',
        ROOT / 'unity/Assets/Game/Runtime/Official/HomeEntityCreation/HomeEntityPrefabPaths.cs'):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    validations = [ROOT / path for path in ('scripts/fixtures/home_native_prefab_resources_validation.cs', 'scripts/test_home_native_prefab_resources.ps1',
        'scripts/fixtures/home_native_factory_probe.cs', 'scripts/fixtures/home_native_rack_owner_validation.cs', 'scripts/test_home_native_rack_owner.ps1')]
    proof = dict(inputs=inputs, sources=sources, routes=routes, outputs=outputs,
        validation_inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in validations if p.is_file()},
        adaptation='Each unchanged original prefab address resolves to one independent local Unity resource reference. Native prefab GUIDs, hierarchy and payload remain owned by their existing verified modules. Local factories install dependencies before activation while original creators/controllers own returned models. The native rack resource owner supplies those factories and local leases without replacing official skin selection or lifetime rules. No global asset unload or user-data mutation is performed.',
        scope='Local runtime resource routes, shared prefab leases, inactive dependency installation and native weapon-rack resource/instantiation ownership. Full building/main-scene gameplay and Player verification remain separate.')
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared local original prefab routes:', len(routes), '; owned outputs:', len(outputs))


if __name__ == '__main__':
    prepare()
