"""Extract original music nodes, Wwise references and battle override tables."""
from pathlib import Path
import hashlib
import json
import re
import struct
import uuid

from Crypto.Hash import MD4
from official_apk import Apk, ROOT, UnityPy
from official_bundle_reader import OfficialBundleReader
from official_source import OfficialSource
from official_tables import Constructors
from prepare_battle_sound_listener_sources import documents

EXPORT = ROOT / '.tools/source-packages/current/884_156692/native-conversion/native-export-all'
ASSETS = EXPORT / 'ExportedProject/Assets'
DEST = ROOT / 'unity/Assets/Official/Combat/NativeSound/Resources/Combat/Wwise/Bgm'
CODE = ROOT / 'unity/Assets/Game/Runtime/Official/BattleBgm'
RECEIPT = ROOT / 'assets/reference/battle-animation/bgm-scene-source-manifest.json'
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())


def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:battle-bgm-assets:' + path.relative_to(ROOT).as_posix()).hex


def script_id(name):
    return struct.unpack('<i', MD4.new(('s\0\0\0' + name).encode()).digest()[:4])[0]


def main():
    previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    old = {row['path']:row['sha256'] for row in previous.get('outputs', [])}
    export_source = EXPORT.with_name(EXPORT.name + '-source-manifest.json')
    exports = {r['path']:r for r in json.loads(export_source.read_bytes())['outputs']}
    inputs, outputs = {}, {}

    def original(path):
        record = identity(path)
        assert record['sha256'] == exports[path.relative_to(EXPORT).as_posix()]['sha256'], path
        inputs[record['path']] = record
        return path.read_text(encoding='utf-8-sig')

    def write(path, text):
        raw = text.encode('utf-8')
        key = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw:
            assert hashlib.sha256(path.read_bytes()).hexdigest() == old.get(key), key
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs[key] = identity(path)

    def meta(path, folder=False):
        text = 'fileFormatVersion: 2\nguid: ' + guid(path) + '\n'
        if folder: text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
        write(Path(str(path) + '.meta'), text)

    def code_guid(path):
        inputs[path.relative_to(ROOT).as_posix()] = identity(path)
        meta_path = Path(str(path) + '.meta')
        inputs[meta_path.relative_to(ROOT).as_posix()] = identity(meta_path)
        return re.search(r'^guid: (\w+)', meta_path.read_text(encoding='utf-8'), re.M)[1]

    script_guids = {name:code_guid(CODE / (name + '.cs'))
                    for name in ('GameBgm', 'GameBgmConfig', 'BgmGameObject')}
    sdk = ROOT / 'unity/Assets/Game/Runtime/Official/Wwise'
    for name in ('AkGameObj', 'AkAudioListener', 'WwiseSwitchReference', 'WwiseSwitchGroupReference', 'WwiseEventReference'):
        path, = sdk.rglob(name + '.cs')
        script_guids[name] = code_guid(path)

    # Only original Wwise reference assets and audio configs can be reached here.
    original_guids = {}
    for folder in ('Wwise/ScriptableObjects', 'MonoBehaviour', 'ScriptableConfig/Modules/Audio/GameBgmConfig'):
        for path in sorted((ASSETS / folder).rglob('*.meta')):
            match = re.search(r'^guid: (\w+)', path.read_text(encoding='utf-8-sig'), re.M)
            if match: original_guids[match[1]] = Path(str(path)[:-5])
    references = {}
    reference_types = {script_id(name):name for name in ('WwiseSwitchReference', 'WwiseSwitchGroupReference', 'WwiseEventReference')}

    def reference(value):
        if value in references: return references[value]
        source = original_guids[value]
        text = original(source); original(Path(str(source) + '.meta'))
        identifier = int(re.search(r'm_Script: \{fileID: (-?\d+),', text)[1])
        kind = reference_types[identifier]
        target = DEST / 'References' / kind / source.name
        references[value] = target
        text = re.sub(r'  m_Script: .*', '  m_Script: {fileID: 11500000, guid: ' + script_guids[kind] + ', type: 3}', text)
        for group in re.findall(r'WwiseSwitchGroupReference: \{fileID: 11400000, guid: (\w+),', text):
            text = text.replace('guid: ' + group + ',', 'guid: ' + guid(reference(group)) + ',')
        write(target, text); meta(target)
        return target

    configs = []
    config_by_guid = {}
    for source in sorted((ASSETS / 'ScriptableConfig/Modules/Audio/GameBgmConfig').glob('*.asset')):
        text = original(source); meta_text = original(Path(str(source) + '.meta'))
        source_guid = re.search(r'^guid: (\w+)', meta_text, re.M)[1]
        target = DEST / 'Configs' / source.name
        text = re.sub(r'  m_Script: .*', '  m_Script: {fileID: 11500000, guid: ' + script_guids['GameBgmConfig'] + ', type: 3}', text)
        for value in re.findall(r'WwiseObjectReference: \{fileID: 11400000, guid: (\w+),', text):
            text = text.replace('guid: ' + value + ',', 'guid: ' + guid(reference(value)) + ',')
        write(target, text); meta(target)
        config_by_guid[source_guid] = target
        configs.append(dict(location='Assets/' + source.relative_to(ASSETS).with_suffix('.asset').as_posix(),
            resource='Combat/Wwise/Bgm/Configs/' + source.stem))

    # Inspect the actual APK first for the global node, including its dedicated
    # non-default listener. The extracted node excludes the existing initializer.
    with OfficialBundleReader(OfficialSource()) as reader:
        matches = [r['name'] for r in reader.manifest('Launch')['bundles'] if 'wwiseglobal' in r['name']]
        bundle, = matches
        raw = reader.read('Launch', bundle)
        global_origin = dict(raw.provenance)
        global_environment = UnityPy.load(Apk.decode(raw.data))
        global_objects = [dict(path_id=obj.path_id, type=obj.type.name,
            sha256=hashlib.sha256(obj.get_raw_data()).hexdigest()) for obj in global_environment.objects]
        global_node, = [obj.read() for obj in global_environment.objects
                        if obj.type.name == 'GameObject' and obj.read().m_Name == 'BgmGameObject']
        assert len(global_node.m_Component) == 4
    source = ASSETS / 'Launch/Prefabs/WwiseGlobal.prefab'
    original(source)
    docs = documents(source)
    go, body = next((i,b) for i,(t,b) in docs.items() if t == 1 and 'm_Name: BgmGameObject\n' in b)
    ids = [go] + [int(x) for x in re.findall(r'component: \{fileID: (-?\d+)\}', body)]
    assert len(ids) == 5
    script_map = {script_id(name):script_guids[name] for name in ('BgmGameObject', 'AkGameObj', 'AkAudioListener')}
    parts = []
    for identifier in ids:
        kind, text = docs[identifier]
        if kind == 4: text = re.sub(r'  m_Father: .*', '  m_Father: {fileID: 0}', text)
        if kind == 114:
            file_id = int(re.search(r'm_Script: \{fileID: (-?\d+),', text)[1])
            text = re.sub(r'  m_Script: .*', '  m_Script: {fileID: 11500000, guid: ' + script_map[file_id] + ', type: 3}', text)
        parts.append('--- !u!' + str(kind) + ' &' + str(identifier) + '\n' + text)
    target = DEST / 'BgmGameObject.prefab'
    write(target, HEADER + ''.join(parts)); meta(target)

    scene_source = ROOT / 'assets/reference/battle-animation/sound-listener-source-manifest.json'
    scenes = []
    for record in json.loads(scene_source.read_bytes())['scenes']:
        source = ROOT / record['source']['path']
        original(source)
        docs = documents(source)
        component, body = next((i,b) for i,(t,b) in docs.items() if t == 114
            and 'm_Script: {fileID: ' + str(script_id('GameBgm')) + ',' in b)
        go = int(re.search(r'm_GameObject: \{fileID: (-?\d+)\}', body)[1])
        components = [int(x) for x in re.findall(r'component: \{fileID: (-?\d+)\}', docs[go][1])]
        assert len(components) == 2 and component in components
        selected = [go] + components
        parts = []
        for identifier in selected:
            kind, text = docs[identifier]
            if kind == 4: text = re.sub(r'  m_Father: .*', '  m_Father: {fileID: 0}', text)
            if kind == 114:
                text = re.sub(r'  m_Script: .*', '  m_Script: {fileID: 11500000, guid: ' + script_guids['GameBgm'] + ', type: 3}', text)
                for value in re.findall(r'WwiseObjectReference: \{fileID: 11400000, guid: (\w+),', text):
                    text = text.replace('guid: ' + value + ',', 'guid: ' + guid(reference(value)) + ',')
                for value in re.findall(r'mBgmConfigOverride: \{fileID: 11400000, guid: (\w+),', text):
                    text = text.replace('guid: ' + value + ',', 'guid: ' + guid(config_by_guid[value]) + ',')
            parts.append('--- !u!' + str(kind) + ' &' + str(identifier) + '\n' + text)
        target = DEST / 'Scenes' / source.name
        write(target, HEADER + ''.join(parts)); meta(target)
        scenes.append(dict(game_ids=record['game_ids'], resource='Combat/Wwise/Bgm/Scenes/' + source.stem,
            source=identity(source), original_component=component))

    constructors = Constructors()
    tables, table_sources = {}, []
    with Apk.current() as apk:
        for table, schema in (('stage_override_bgm', 'TbStageOverrideBgmInfo'), ('fight_theme', 'TbFightThemeInfo')):
            path = 'Assets/Config/Luban/' + table + '.g.bytes'
            obj = apk.asset('config_luban', path)
            data = obj.read().m_Script.encode('utf-8', 'surrogateescape')
            rows = constructors.table(data, schema)
            key = 'StageId' if table == 'stage_override_bgm' else 'FightThemeId'
            tables[table] = [dict(id=row[key], group=row['RtpcName'], value=row['RtpcValue'], config=row['BgmConfigPath']) for row in rows]
            table_sources.append(dict(apk.provenance(obj), asset=path, bytes=len(data), sha256=hashlib.sha256(data).hexdigest()))
    catalog = dict(manifest='884_156692', provider_resource='Combat/Wwise/Bgm/BgmGameObject',
        default_config_resource='Combat/Wwise/Bgm/Configs/GameBgmConfig', configs=configs,
        scenes=[{k:v for k,v in row.items() if k in ('game_ids','resource')} for row in scenes], **tables)
    target = DEST / 'catalog.json'
    write(target, json.dumps(catalog, ensure_ascii=False, indent=2) + '\n'); meta(target)
    for item in list(outputs):
        path = ROOT / item
        for parent in path.parents:
            if parent == DEST.parent: break
            if parent == DEST or parent.is_relative_to(DEST):
                if not Path(str(parent) + '.meta').exists(): meta(parent, True)
                elif 'guid: ' + guid(parent) in Path(str(parent) + '.meta').read_text(encoding='utf-8'):
                    outputs[str(parent.relative_to(ROOT)).replace('\\','/') + '.meta'] = identity(Path(str(parent) + '.meta'))
    receipt = dict(manifest='884_156692', export_source=identity(export_source),
        listener_source=identity(scene_source), global_apk_source=global_origin, global_apk_objects=global_objects,
        inputs=list(inputs.values()), scenes=scenes, configuration_sources=table_sources,
        constructor_sources=[identity(constructors.source_root / (name + '.cs')) for name in sorted(constructors.sources)],
        outputs=list(outputs.values()), tool=identity(Path(__file__)), adaptations=[
            'Preserve original music node IDs, serialized values and reference assets; rebind script and asset GUIDs.',
            'Extract only the original global BGM child; the original initializer is already owned by the application sound module.',
            'Expose selected music fields from the exact original configuration constructors.'])
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps(dict(scenes=len(scenes), references=len(references), outputs=len(outputs),
        stage_overrides=len(tables['stage_override_bgm']), themes=len(tables['fight_theme']))))


if __name__ == '__main__':
    main()
