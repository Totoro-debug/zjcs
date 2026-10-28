"""Bind pinned shared config application code and the unchanged settings table."""
import argparse
import hashlib
import json
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION = '871_156598'
SETTINGS_HASH = '07b8e9400e89722fa35ee8d6ac9ff693f00b24cdfd8da2ad91589673989c347e'
RECEIPT = ROOT / 'assets/reference/official-map/map14-shared-config-source-manifest.json'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare(cache):
    native = json.loads(cache.read_bytes())
    code = ROOT / '.tools/source-packages/current' / VERSION / 'official-code'
    metadata = code / 'global-metadata.dat'
    image = ROOT / '.tools/source-packages/commerce-native/libil2cpp-restored.bin'
    if sha(metadata) != native['metadata_sha256'] or sha(image) != native['image_sha256'] or native['metadata_sha256'] != '58884b94b56ef35665f44d3f186fce8b8c854cf3bab0a74e4733a18b6a9175b4' or native['image_sha256'] != '8e49906e085ba3819c4b47fd3bd12dcdd594ff0fcc75d418a87ac35a4566305b':
        raise ValueError('Native source evidence differs from the pinned inputs')
    proof_path = code.parent / 'table-proof-source-manifest.json'
    proof = json.loads(proof_path.read_bytes())
    table = next(row for row in proof['tables'] if row['table'] == 'game_settings')
    settings = code.parent / 'official-data/game_settings.g.bytes'
    if proof['manifest'] != VERSION or table['sha256'] != SETTINGS_HASH or sha(settings) != SETTINGS_HASH:
        raise ValueError('Original settings table differs from its retained official source')
    output = ROOT / 'unity/Assets/Official/World/Map/14/SharedConfig/Resources/Map14/SharedConfig/game_settings.g.bytes'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(settings.read_bytes())
    owned = [ROOT / 'unity/Assets/Game/Runtime/Official/SharedConfig', ROOT / 'unity/Assets/Game/Runtime/Official/SharedLaunch']
    paths = [file for folder in owned for file in folder.iterdir() if file.suffix in ('.cs', '.asmdef')]
    paths += [ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedConfiguration.cs', output]
    paths += [ROOT / 'unity/Assets/Game/Editor/Map14/HeroStatues' / name for name in ('OfficialMap14SharedConfigAcceptance.cs', 'OfficialMap14HeroStatueAcceptance.cs', 'Zjcs.Editor.Map14.HeroStatues.asmdef')]
    paths += [ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/Zjcs.Offline.World.NativeMap14SharedEntities.asmdef']
    cs_template = (owned[0] / 'ConfigRequest.cs.meta').read_text(encoding='utf-8')
    folder_template = Path(str(owned[0]) + '.meta').read_text(encoding='utf-8')
    for file in paths:
        meta = Path(str(file) + '.meta')
        if not meta.exists():
            template = cs_template if file.suffix == '.cs' else 'fileFormatVersion: 2\nguid: PLACEHOLDER\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
            template = re.sub(r'guid: \S+', 'guid: ' + uuid.uuid4().hex, template)
            meta.write_text(template, encoding='utf-8')
        for parent in file.parents:
            if parent == ROOT / 'unity/Assets':
                break
            parent_meta = Path(str(parent) + '.meta')
            if not parent_meta.exists():
                parent_meta.write_text(re.sub(r'guid: \S+', 'guid: ' + uuid.uuid4().hex, folder_template), encoding='utf-8')
    manager_methods = set('get_RootDir get_Status get_FileLoader get_PathTransformFunc set_PathTransformFunc Dispose RegisterTypes RegisterType GetParser AddJob TransformPath InitializeCommon OnStatusChanged LoadJob ParseJob PostParseJob get_BackgroundThreadCount OnDestroy Initialize InvokeProgressListener Wait QueueUserWorkItem ResetThreadPool Update .ctor .cctor'.split())
    names = ['GConfig.ConfigAttribute', 'GConfig.ParsePhase', 'GConfig.IConfig', 'GConfig.IParser', 'GConfig.ParserBase', 'GConfig.ParserBase`1',
             'GConfig.ISupportParseSingle', 'GConfig.ISupportParseSingle`1', 'GConfig.ILoadSingle', 'GConfig.ILoadSingle`1', 'GConfig.IParseSingleFromMemory',
             'GConfig.ConfigRequestBase', 'GConfig.ConfigRequest', 'GConfig.ConfigRequest`1', 'GConfig.ParseStatus', 'GConfig.StatusChanged',
             'GConfig.IConfigParseJob', 'GConfig.ConfigParseJob', 'GConfig.ConfigParseJob`1', 'GConfig.ConfigParseJobFactory',
             'GConfig.LoadDelegate', 'GConfig.ParseDelegate', 'GConfig.ParseDelegate`1', 'GConfig.PostParseDelegate', 'GConfig.PostParseDelegate`1',
             'GConfig.ConfigBatchRequest', 'GConfig.ConfigManager', 'GConfig.ConfigManagerStatus', 'GConfig.ConfigManager+ConfigParseJob',
             'GConfig.ThreadPool', 'GConfig.ThreadPool+WorkItem', 'GConfig.IFileLoader', 'GConfig.FileLoader', 'GConfig.DefaultFileLoader',
             'GConfig.ResourceManager', 'GConfig.ResourceManager+LoadItem', 'GConfig.ResourceManager+LoadWorker', 'GConfig.Helper.JsonHelper',
             'GConfig.Parser.RawParser', 'GConfig.Parser.JsonParser`1', 'GConfig.Parser.CatalogConfigAttribute', 'GConfig.Parser.CatalogCache`1',
             'GConfig.Parser.CatalogParser`2', 'GConfig.Parser.CatalogChildParseJob`1']
    # Closures are original application methods belonging only to the selected types.
    names += [name for name in native['types'] if '+' in name and name.split('+')[0] in names and name not in names]
    evidence = {}
    for name in names:
        entry = dict(native['types'][name])
        entry['methods'] = [dict(method) for method in entry['methods'] if name != 'GConfig.ConfigManager' or method['name'] in manager_methods]
        for method in entry['methods']:
            method['bodies'] = [{k: v for k, v in body.items() if k != 'instructions'} for body in method['bodies']]
        evidence[name] = entry
    inputs = [metadata, image, proof_path, settings, code / 'managed/decoded/Console.dll', code / 'managed/decoded/Launch.dll', code / 'launch-source/Common/RegionCodeEnum.cs', ROOT / 'unity/Packages/manifest.json', ROOT / 'unity/Packages/packages-lock.json']
    folder_metas = [Path(str(owned[1]) + '.meta')]
    payload_root = ROOT / 'unity/Assets/Official/World/Map/14/SharedConfig'
    folder_metas += [Path(str(folder) + '.meta') for folder in [payload_root, *payload_root.rglob('*')] if folder.is_dir()]
    source_paths = sorted(set(paths + [Path(str(path) + '.meta') for path in paths] + folder_metas))
    receipt = dict(manifest=VERSION, mapClassId=14,
                   operation='Port pinned GConfig application native methods and the complete original RegionCodeEnum; load unchanged Console GameSettingsParser and exact Luban bytes through local Unity Resources.',
                   inputs=[dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)) for path in inputs],
                   settings_source=table, native_types=evidence,
                   tools=[dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)) for path in (Path(__file__).resolve(), ROOT / 'scripts/read_official_config_native.py')],
                   dependencies=[dict(package='com.unity.nuget.newtonsoft-json', version='3.2.1', assembly='Newtonsoft.Json', purpose='Retain original GConfig JsonHelper serializer and reader API; Unity registry package is bundled into the Player.')],
                   outputs=[dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)) for path in source_paths],
                   bindings=dict(original_path='Config/Luban/game_settings.g.bytes', resource='Map14/SharedConfig/game_settings.g', singleton='Parsers.Instance', adapter='NativeMap14SharedConfiguration'),
                   logging_adapter='Original error branches use Unity Debug; request transitions, payload publication and callback order are retained.',
                   limitations=['ConfigManager phase batching, bulk validation/postprocessing and reload APIs are not migrated in this module.', 'The local scope currently binds GameSettings only; full Map14 table catalogs, host, business windows and saves remain pending.'])
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Bound', len(evidence), 'original config types and', len(source_paths), 'source/payload outputs')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--native-cache', type=Path, required=True)
    args = parser.parse_args()
    prepare(args.native_cache)
