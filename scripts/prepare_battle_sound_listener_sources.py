"""Bind original battle listener prefab nodes and matching SDK listener code."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import uuid

from official_apk import Apk, ROOT

SDK = ROOT / '.tools/wwise/UnityIntegration-2022.1.18.8567.3199'
SDK_SOURCE = ROOT / 'assets/reference/battle-animation/wwise-windows-integration-source-manifest.json'
EXPORT = ROOT / '.tools/source-packages/current/884_156692/native-conversion/native-export-all'
EXPORT_SOURCE = EXPORT.with_name(EXPORT.name + '-source-manifest.json')
SCENES = ROOT / 'unity/Assets/Official/Combat/Scenes/Resources/BattleScenes/catalog.json'
ART = ROOT / 'unity/Assets/Official/Combat/NativeSound/Resources/Combat/Wwise'
CODE = ROOT / 'unity/Assets/Game/Runtime/Official/Wwise'
RECEIPT = ROOT / 'assets/reference/battle-animation/sound-listener-source-manifest.json'
MONO = ('AkAudioListener', 'AkEnvironment', 'AkEnvironmentPortal', 'AkGameObj',
        'AkGameObjEnvironmentData', 'AkGameObjListenerList', 'AkGameObjPositionData',
        'AkGameObjPositionOffsetData', 'AkGameObjPosOffsetData', 'AkTriggerBase', 'AkTriggerHandler')


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw), sha256=digest(raw))


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:wwise-listeners:' + path.relative_to(ROOT / 'unity/Assets').as_posix()).hex


def documents(path):
    return {int(i): (int(t), b) for t, i, b in re.findall(
        r'^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)', path.read_text(encoding='utf-8-sig'), re.M | re.S)}


def main():
    previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    old_hashes = {record['path']: record['sha256'] for record in previous.get('outputs', [])}
    outputs, inputs = {}, {}

    def write(path, raw):
        if isinstance(raw, str): raw = raw.encode('utf-8')
        key = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw:
            if digest(path.read_bytes()) != old_hashes.get(key):
                raise ValueError('Preserve unrecognized existing output: ' + key)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw: path.write_bytes(raw)
        outputs[key] = identity(path)

    def meta(path, folder=False):
        raw = 'fileFormatVersion: 2\nguid: ' + guid(path) + '\n'
        if folder: raw += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
        write(Path(str(path) + '.meta'), raw)

    proof = json.loads(SDK_SOURCE.read_bytes())
    assert proof['sdk_version'] == '2022.1.18.8567' and proof['integration_build'] == 3199
    sdk_hashes = {record['path']: record['sha256'] for record in proof['files']}

    def original_sdk(path):
        value = identity(path)
        if value['sha256'] != sdk_hashes[value['path']]:
            raise ValueError('Changed installed official listener SDK input: ' + value['path'])
        inputs[value['path']] = value
        return path.read_bytes()

    for path in sorted((SDK / 'Wwise/API/Runtime/WwiseTypes').rglob('*.cs')):
        target = CODE / 'API/Runtime/WwiseTypes' / path.relative_to(SDK / 'Wwise/API/Runtime/WwiseTypes')
        write(target, original_sdk(path)); meta(target)
    for name in MONO:
        target = CODE / 'MonoBehaviour/Runtime' / (name + '.cs')
        write(target, original_sdk(SDK / 'Wwise/MonoBehaviour/Runtime' / (name + '.cs'))); meta(target)
    for relative, references in [
        ('API/Runtime/WwiseTypes/AK.Wwise.Unity.API.WwiseTypes.asmdef', ['AK.Wwise.Unity.API']),
        ('MonoBehaviour/Runtime/AK.Wwise.Unity.MonoBehaviour.asmdef', ['AK.Wwise.Unity.API', 'AK.Wwise.Unity.API.WwiseTypes'])
    ]:
        original = json.loads(original_sdk(SDK / 'Wwise' / relative))
        original['references'] = references
        target = CODE / relative
        write(target, json.dumps(original, indent=2) + '\n'); meta(target)

    # The existing original map port already contains this exact client body.
    follow = ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeEnvironment/FollowSceneActiveSelfController.cs'
    follow_source = ROOT / '.tools/source-packages/official-code/client-source/FollowSceneActiveSelfController.cs'
    body = follow_source.read_text(encoding='utf-8-sig').strip()
    if body not in follow.read_text(encoding='utf-8'):
        raise ValueError('Inspect changed existing original scene activation implementation')
    follow_meta = Path(str(follow) + '.meta')
    follow_guid = re.search(r'^guid: (\w+)', follow_meta.read_text(encoding='utf-8'), re.M)[1]
    existing = [identity(p) for p in (follow, follow_meta, follow_source)]

    # OfficialBundleReader performs APK inspection before the pinned cloud cache.
    with Apk.current() as apk:
        bundle = 'defaultpackage_assets_prefabs_ecentity_fight_fightstage_1000.bundle'
        objects = apk.objects(bundle)
        listeners = []
        for obj in objects:
            if obj.type.name == 'MonoBehaviour':
                value = obj.read(check_read=False)
                if value.m_Script.read().m_ClassName == 'AkAudioListener': listeners.append(obj)
        if len(listeners) != 1: raise ValueError('Original representative battle listener is not unique')
        source = dict(apk.provenance(listeners[0]), object_sha256=digest(listeners[0].get_raw_data()))

    scenes = json.loads(SCENES.read_bytes())['scenes']
    names = {scene['sceneName'] for scene in scenes}
    paths = {name: EXPORT / f'ExportedProject/Assets/Prefabs/EcEntity/Fight/FightStage/{name.removeprefix("game_")}/{name}.prefab' for name in names}
    wanted = {p.relative_to(EXPORT).as_posix() for p in paths.values()}
    exported = json.loads(EXPORT_SOURCE.read_bytes())
    if exported['manifest'] != '884_156692': raise ValueError('Inspect changed selected native prefab export')
    expected = {r['path']: r['sha256'] for r in exported['outputs'] if r['path'] in wanted}
    if expected.keys() != wanted: raise ValueError('Native prefab export receipt does not cover every battle scene')
    records, representative = [], None
    bindings = {
        -130519023: guid(CODE / 'MonoBehaviour/Runtime/AkGameObj.cs'),
        1872259402: guid(CODE / 'MonoBehaviour/Runtime/AkAudioListener.cs'),
        -16082665: follow_guid,
    }
    for name, path in sorted(paths.items()):
        if digest(path.read_bytes()) != expected[path.relative_to(EXPORT).as_posix()]:
            raise ValueError('Changed original battle scene prefab: ' + name)
        blocks = documents(path)
        listeners = [i for i, (t, b) in blocks.items() if t == 114 and '  isDefaultListener: 1\n' in b]
        if len(listeners) != 1: raise ValueError('Original scene has no unique default listener: ' + name)
        listener = listeners[0]
        go = int(re.search(r'm_GameObject: \{fileID: (-?\d+)\}', blocks[listener][1])[1])
        components = [int(i) for i in re.findall(r'- component: \{fileID: (-?\d+)\}', blocks[go][1])]
        if len(components) != 4: raise ValueError('Inspect changed listener component list: ' + name)
        transform = next(i for i in components if blocks[i][0] == 4)
        pose = blocks[transform][1]
        if not all(value in pose for value in ['m_LocalPosition: {x: 0, y: 0, z: 20}', 'm_LocalRotation: {x: -0, y: -0, z: -0, w: 1}', 'm_LocalScale: {x: 1, y: 1, z: 1}']):
            raise ValueError('Inspect changed original listener pose: ' + name)
        parent = int(re.search(r'm_Father: \{fileID: (-?\d+)\}', pose)[1])
        parent_go = int(re.search(r'm_GameObject: \{fileID: (-?\d+)\}', blocks[parent][1])[1])
        if '  m_Name: Main Camera\n' not in blocks[parent_go][1]:
            raise ValueError('Inspect changed original listener parent: ' + name)
        ids = []
        for comp in components:
            if blocks[comp][0] != 114: continue
            script_id = int(re.search(r'm_Script: \{fileID: (-?\d+),', blocks[comp][1])[1])
            ids.append(script_id)
            if script_id not in bindings: raise ValueError('Unbound listener script: ' + name)
            if script_id == -130519023 and not all(value in blocks[comp][1] for value in [
                'initialListenerList: []', 'useDefaultListeners: 1', 'isEnvironmentAware: 0',
                'isStaticObject: 0', 'positionOffset: {x: 0, y: 0, z: 0}', 'm_posOffsetData: {fileID: 0}'
            ]): raise ValueError('Inspect changed original listener sound-object settings: ' + name)
        if set(ids) != bindings.keys(): raise ValueError('Incomplete listener script binding: ' + name)
        records.append(dict(source=identity(path), listener_game_object=go, transform=transform,
            components=components, game_ids=sorted(s['gameId'] for s in scenes if s['sceneName'] == name)))
        if name == 'game_1000': representative = (blocks, go, components, transform)
    blocks, go, components, transform = representative
    prefab = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
    for key in [go] + components:
        kind, value = blocks[key]
        if key == transform: value = re.sub(r'm_Father: \{fileID: -?\d+\}', 'm_Father: {fileID: 0}', value)
        if kind == 114:
            script_id = int(re.search(r'm_Script: \{fileID: (-?\d+),', value)[1])
            value = re.sub(r'm_Script: .*', 'm_Script: {fileID: 11500000, guid: ' + bindings[script_id] + ', type: 3}', value)
        prefab += f'--- !u!{kind} &{key}\n' + value
    target = ART / 'AudioListener.prefab'
    write(target, prefab); meta(target)
    target = ART / 'listener-bindings.json'
    write(target, json.dumps(dict(manifest='884_156692', prefab_resource='Combat/Wwise/AudioListener',
        parent='Main Camera', game_ids=sorted(s['gameId'] for s in scenes)), indent=2) + '\n'); meta(target)
    for key in list(outputs):
        if key.endswith('.meta'): continue
        for directory in (ROOT / key).parents:
            if directory in (CODE, ART): break
            folder_meta = Path(str(directory) + '.meta')
            if not folder_meta.exists(): meta(directory, True)
            elif 'guid: ' + guid(directory) in folder_meta.read_text(encoding='utf-8'):
                outputs[folder_meta.relative_to(ROOT).as_posix()] = identity(folder_meta)
    result = dict(manifest='884_156692', sdk_source=identity(SDK_SOURCE),
        export_source=identity(EXPORT_SOURCE), scenes_source=identity(SCENES),
        representative_original=source, sdk_inputs=list(inputs.values()), existing_bindings=existing,
        scenes=records, outputs=list(outputs.values()), tool=identity(Path(__file__)), adaptations=[
            'Copy original SDK listener, sound-object, environment and trigger dependencies and Wwise value/reference types without changing C# bytes.',
            'Assembly references retain only installed native API/value modules; unavailable optional Addressables references are omitted.',
            'Reuse the existing original FollowSceneActiveSelfController body; bind its existing script identity.',
            'Copy the original listener node and all three scripts; only script references and the standalone root parent are rebound. Mount under the real output camera to retain local z=20 and original camera following.',
            f'All {len(scenes)} supported game IDs resolve to {len(records)} selected original scene prefabs with the same verified listener pose, scripts and sound-object settings.'])
    RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(dict(outputs=len(outputs), sdk_files=len(inputs), scenes=len(records), game_ids=len(scenes))))


if __name__ == '__main__': main()
