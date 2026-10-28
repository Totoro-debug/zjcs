"""Migrate original client entity indexing and bind the unchanged selected index."""
import json
import re
import io
import urllib.request
import zipfile
from pathlib import Path
from official_apk import Apk, ROOT
from prepare_unity_map13_sources import encode, sha, write, meta
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_unity_map14_entity_loading import diagnostics
CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
RECEIPT = ROOT / 'assets/reference/official-map/map14-entity-index-source-manifest.json'
def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))
def prepare():
    source = CODE / 'client-source/Common/MyEntityIndexHandler.cs'
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    if manifest['manifest'] != '871_156598':
        raise ValueError('Original entity index source version differs')
    assemblies = [CODE / 'managed/decoded' / name for name in ('Assembly-CSharp.dll', 'Console.dll', 'Launch.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Original entity index assembly changed')
    launch = CODE / 'launch-source/Common/ConfigConst.cs'
    constant = re.search(r'public static string EC_CONFIG_PATH = "([^"]+)";', launch.read_text(encoding='utf-8-sig'))[1]
    original_path = constant + 'index.g.txt'
    text = source.read_text(encoding='utf-8-sig')
    text = re.sub(r'(?m)^using (?:Framework(?:\.[\w.]+)?|GameFramework);\n', '', text)
    text = text.replace('namespace Common;', 'using System;\nusing Zjcs.Official.EntityLoading;\nnamespace Zjcs.Official.EntityIndex\n{') + '\n}\n'
    text = text.replace('SceneScopeBackgroundServiceBase, IMyEntityIndexHandler, IMapScopeBackgroundService, IFightScopeBackgroundService', 'IMyEntityIndexHandler, IDisposable')
    text = text.replace('ConfigConst.EC_CONFIG_PATH + "index.g.txt"', json.dumps(original_path))
    text = text.replace('[Inject]\n\tprivate IResourceLoader ResourceLoader { get; set; }', 'public IEntityIndexContext Context { get; set; }')
    text = text.replace('protected override void Initialize()', 'public void Initialize()').replace('protected override void Dispose()', 'public void Dispose()')
    text = text.replace('\t\tbase.Initialize();\n', '').replace('\t\tbase.Dispose();\n', '')
    text = text.replace('JsonUtils.FromJson<Dictionary<string, int[]>>(context)', 'Context.Decode(context)')
    text = text.replace('string location = CsvUtils.WrapCsvPath(sIndexPath);\n\t\tusing AssetOperationHandle<TextAsset> assetOperationHandle = ResourceLoader.LoadAsset<TextAsset>(location);\n\t\treturn assetOperationHandle.Result.text;', 'return Context.LoadText(sIndexPath);')
    text, count = diagnostics(text)
    if count != 1 or 'GLogger' in text or 'ResourceLoader.' in text or 'base.' in text:
        raise ValueError('Original entity index adaptation shape differs')
    runtime = ROOT / 'unity/Assets/Game/Runtime/Official/EntityIndex'
    write(runtime / source.name, text.encode())
    native = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14EntityLoading'
    editor = ROOT / 'unity/Assets/Game/Editor/Map14/EntityIndex'
    editor.mkdir(parents=True,exist_ok=True)
    folders = [(runtime, 'Zjcs.Official.EntityIndex', ['Zjcs.Official.EntityLoading'], []),
               (native, 'Zjcs.Offline.World.NativeMap14EntityLoading', ['Zjcs.Official.EntityIndex', 'Zjcs.Official.EntityLoading', 'Zjcs.Offline.World.NativeMap14SharedEntities'], []),
               (editor, 'Zjcs.Editor.Map14.EntityIndex', ['Zjcs.Official.EntityIndex', 'Zjcs.Official.EntityLoading', 'Zjcs.Offline.World.NativeMap14EntityLoading', 'Zjcs.Offline.World.NativeMap14SharedEntities', 'Zjcs.Editor.Map14.ClientEntities'], ['Editor'])]
    outputs = []
    for folder, name, refs, platforms in folders:
        write(folder / (name + '.asmdef'), encode(dict(name=name, autoReferenced=False, references=refs, includePlatforms=platforms)))
        for path in sorted(folder.iterdir()):
            if path.is_file() and path.suffix != '.meta':
                runtime_meta(path); outputs += [path, Path(str(path) + '.meta')]
        runtime_meta(folder); outputs.append(Path(str(folder) + '.meta'))
    apk = Apk.current(); obj = apk.asset('config_ec', 'index.g.txt')
    raw = obj.read().m_Script.encode('utf-8', errors='surrogateescape')
    index = json.loads(raw); identifiers = [n for values in index.values() for n in values]
    if len(identifiers) != len(set(identifiers)):
        raise ValueError('Original selected entity index has duplicates')
    assets = ROOT / 'unity/Assets/Official/World/Map/14/Resources/Map14/EntityLoading'
    package_path = ROOT / '.tools/nuget-cache/map14-shared-ec/litedb.5.0.20.nupkg'
    package_url = 'https://api.nuget.org/v3-flatcontainer/litedb/5.0.20/litedb.5.0.20.nupkg'
    if not package_path.exists():
        write(package_path, urllib.request.urlopen(package_url, timeout=45).read())
    package = package_path.read_bytes()
    if sha(package) != '5fa8d0e820a5acb7e408201c88be6de1191ac61888c677d476062eaf9025b566':
        raise ValueError('Pinned public JSON platform dependency package changed')
    archive = zipfile.ZipFile(io.BytesIO(package))
    dependency = assets / 'LiteDB.bytes'; write(dependency, archive.read('lib/netstandard2.0/LiteDB.dll'))
    license_path = assets / 'LiteDB-license.txt'; write(license_path, archive.read('LICENSE'))
    payload = assets / 'index.g.txt'; write(payload, raw)
    binding = assets / 'index-binding.json'
    write(binding, encode(dict(OriginalPath=original_path, Resource='Map14/EntityLoading/index.g', Sha256=sha(raw),
                              Directories=len(index), Identifiers=len(identifiers),
                              DependencyResource='Map14/EntityLoading/LiteDB', DependencySha256=sha(dependency.read_bytes()),
                              DependencyModuleId='b132ccd6-5c02-42e0-a5e3-839c6502d6c3')))
    for path in (payload, binding, dependency, license_path):
        meta(path); outputs += [path, Path(str(path) + '.meta')]
    runtime_meta(assets); outputs.append(Path(str(assets) + '.meta'))
    inputs = [source, launch, CODE / 'client-source/IMyEntityIndexHandler.cs', CODE / 'common-source/Common/CsvUtils.cs', CODE / 'common-source/Common/JsonUtils.cs']
    write(RECEIPT, encode(dict(manifest=apk.version, mapClassId=14, assemblies=[record(p) for p in assemblies],
        inputs=[record(p) for p in inputs], tools=[record(Path(__file__).resolve())],
        payload=dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()), text_sha256=sha(raw),
                     directories=len(index), identifiers=len(identifiers)), outputs=[record(p) for p in outputs],
        platformDependency=dict(name='LiteDB', version='5.0.20', url=package_url, package_sha256=sha(package),
            framework='netstandard2.0', dll_sha256=sha(dependency.read_bytes()),
            moduleId='b132ccd6-5c02-42e0-a5e3-839c6502d6c3', license_entry='LICENSE',
            purpose='Original JsonUtils static initialization requires the original referenced LiteDB ABI. This pinned public MIT package is a platform dependency, not an extracted original game assembly or a new save implementation.'),
        operation='Copy the complete original MyEntityIndexHandler with exact directory inversion, duplicate handling, static path cache and original path formatting. Replace scene-scope lifetime hooks with explicit Initialize/Dispose and bind local text loading through its original path; unchanged original Console JsonUtils.FromJson decodes the selected unchanged APK-first index. CsvUtils.WrapCsvPath is identity in this source. No fallback directory mapping or invented entity address is supplied.',
        validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14EntityIndexAcceptance.Run', graphicalPlayMode=True,
                        checks=['All selected index directories, identifiers and exact native paths', 'Original static path cache and disposal/reinitialization', 'Native shared state, client container/loading and four-statue regression']),
        limitations=['Native prefab catalog and dependency injection, complete entity components, formal map host and Windows Player remain required.'])))
    print('Migrated original entity index:', len(index), 'directories,', len(identifiers), 'identifiers')
if __name__ == '__main__':
    prepare()
