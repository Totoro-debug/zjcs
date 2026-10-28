"""Bind untouched original shared EC IL, original metadata ABI and local prefabs.

Only application metadata and generated logging forwarding IL are inspected.
No animation SDK implementation is read, copied or translated by this tool.
"""
import base64
import hashlib
import io
import json
from pathlib import Path
import struct
import subprocess
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

from official_apk import ROOT
from official_metadata import Metadata
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_sound_runtime import runtime_meta

RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities'
ASSETS = ROOT / 'unity/Assets/Official/World/Map/14/SharedRuntime'
RESOURCES = ROOT / 'unity/Assets/Game/Resources/Map14/Native'
CONFIG = ROOT / 'unity/Assets/Game/Runtime/Official/SharedConfig'
RECEIPT = ROOT / 'assets/reference/official-map/map14-shared-ec-runtime-source-manifest.json'
CACHE = ROOT / '.tools/nuget-cache/map14-shared-ec'
PACKAGES = [
    ('Microsoft.Extensions.Logging.Abstractions', '8.0.0', 'netstandard2.0',
     'UKCt2W0w2QWA+44ColzqCqFfTSJ0Qnm1rP4YzIVot0QCqgYtXbE8xYh6CL/STgfLyIsvwQ7o7sLDft87zaf4pw=='),
    ('ZLogger', '2.5.10', 'netstandard2.1',
     '/1DqUYTMuXic6DqCSS70FsMPgxpqGsXwxhG4UxPwOBIzjHCMUcvYgn20+YLi+s/Y9RgsL+Pw1+fUtD7pj5bGgg=='),
]


def meta(path):
    identity = path.relative_to(ROOT / 'unity').as_posix()
    guid = hashlib.md5(('zjcs-official:' + identity).encode()).hexdigest()
    text = 'fileFormatVersion: 2\nguid: ' + guid + '\n'
    if path.is_dir():
        text += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
    elif path.suffix == '.asset':
        text += 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n'
    else:
        text += 'TextScriptImporter:\n  externalObjects: {}\n'
    text += '  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    target = Path(str(path) + '.meta')
    if target.exists() and target.read_text(encoding='utf-8-sig').replace('\r\n','\n') != text:
        raise ValueError('Unrelated shared EC asset metadata: ' + str(target))
    write(target, text.encode())
    return guid


def packages():
    CACHE.mkdir(parents=True, exist_ok=True)
    sources = []
    for name, version, framework, expected in PACKAGES:
        slug = name.lower()
        url = f'https://api.nuget.org/v3-flatcontainer/{slug}/{version}/{slug}.{version}.nupkg'
        cache = CACHE / f'{slug}.{version}.nupkg'
        if not cache.exists():
            write(cache, urllib.request.urlopen(url, timeout=45).read())
        package = cache.read_bytes()
        if base64.b64encode(hashlib.sha512(package).digest()).decode() != expected:
            raise ValueError('Pinned original logging dependency hash differs: ' + name)
        archive = zipfile.ZipFile(io.BytesIO(package))
        dll = archive.read(f'lib/{framework}/{name}.dll')
        write(CACHE / (name + '.dll'), dll)
        licenses = [n for n in archive.namelist() if 'license' in n.lower() and not n.endswith('/')]
        # Keep the publisher package and its license in the raw tool cache;
        # bundle the complete publisher license with the local payload too.
        for index, entry in enumerate(licenses):
            write(ASSETS / f'{name}-license-{index}.txt', archive.read(entry))
        license_source = None
        if not licenses and name == 'ZLogger':
            specification = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))))
            repository = next(n for n in specification.iter() if n.tag.endswith('}repository'))
            if repository.attrib != dict(type='git',url='https://github.com/Cysharp/ZLogger',commit='ac885aa1e6588d28aaf6b523682a591e97ed9ba5'):
                raise ValueError('Pinned ZLogger publisher repository differs')
            url_license = 'https://raw.githubusercontent.com/Cysharp/ZLogger/ac885aa1e6588d28aaf6b523682a591e97ed9ba5/LICENSE'
            cached_license = CACHE/'ZLogger-LICENSE.txt'
            if not cached_license.exists(): write(cached_license,urllib.request.urlopen(url_license,timeout=45).read())
            content = cached_license.read_bytes()
            if b'MIT License' not in content or b'Cysharp' not in content:
                raise ValueError('Publisher ZLogger license is not the expected MIT license')
            write(ASSETS/'ZLogger-license.txt',content)
            license_source = dict(url=url_license,sha256=sha(content))
        sources.append(dict(name=name, version=version, url=url,
                            package_sha512=expected, dll_sha256=sha(dll),
                            framework=framework, license_entries=licenses,license_source=license_source))
    return sources


def field_graph(value):
    rows = []
    def visit(item, parent, name):
        index = len(rows)
        kind = 5 if item is None else 3 if isinstance(item,bool) else 0 if isinstance(item,dict) else 1 if isinstance(item,list) else 2 if isinstance(item,str) else 4
        encoded = '' if kind in (0,1,5) else str(item).lower() if kind == 3 else str(item)
        rows.append(dict(Parent=parent, Name=name, Kind=kind, Value=encoded))
        if kind == 0:
            for key, child in item.items(): visit(child,index,key)
        elif kind == 1:
            for offset, child in enumerate(item): visit(child,index,str(offset))
    visit(value,-1,'')
    return rows


def config_contract(metadata):
    """Recover the complete empty marker interface; table classes stay in IL."""
    row = next(t for t in metadata.types if t['namespace']=='GConfig' and t['name']=='IConfig')
    definition = struct.unpack_from('<16i8H2I',metadata.data,metadata.header[40]+88*row['index'])
    if row['flags'] != 0xa1 or definition[16] != 0 or definition[22] != 0 or row['fields']:
        raise ValueError('Original GConfig.IConfig marker contract changed')
    source = '// Complete original marker interface from APK metadata.\nnamespace GConfig\n{\n    public interface IConfig { }\n}\n'
    write(CONFIG/'IConfig.cs',source.encode())
    asmdef = CONFIG/'Config.asmdef'
    write(asmdef,encode(dict(name='Config',rootNamespace='GConfig',references=[],autoReferenced=False,noEngineReferences=True)))
    for path in [CONFIG,asmdef,CONFIG/'IConfig.cs']: runtime_meta(path)
    return dict(type='GConfig.IConfig',type_index=row['index'],method_count=0,field_count=0,interface_count=0)


def build():
    manifest_path = CODE.parent / 'code-manifest.json'
    manifest = json.loads(manifest_path.read_bytes())
    originals = [CODE / 'managed/decoded' / name for name in ('Console.dll','Luban.dll','Framework.FirstPass.Runtime.dll')]
    for path in originals:
        if sha(path.read_bytes()) != manifest['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Original shared assembly changed: ' + path.name)
    metadata_path = CODE / 'global-metadata.dat'
    if sha(metadata_path.read_bytes()) != manifest['metadata_sha256']:
        raise ValueError('Original APK metadata changed')
    metadata = Metadata(metadata_path.read_bytes())
    interface = next(t for t in metadata.types if t['namespace']=='GFrameCore' and t['name']=='IGSerializable')
    definition = struct.unpack_from('<16i8H2I',metadata.data,metadata.header[40]+88*interface['index'])
    if definition[16] != 1: raise ValueError('Original serializable contract method count differs')
    method = struct.unpack_from('<6iI4H',metadata.data,metadata.header[12]+36*definition[9])
    if metadata.string(method[0]) != 'GetOpCode' or method[-1] != 0 or metadata.type_for(method[2])['name'] != 'UInt16':
        raise ValueError('Original serializable contract ABI differs')
    config_abi = config_contract(metadata)
    package_sources = packages()
    output = CACHE / 'contracts'
    subprocess.run(['pwsh','-NoProfile','-File',str(ROOT/'scripts/prepare_unity_shared_ec_contracts.ps1'),
                    '-OriginalDirectory',str(CODE/'managed/decoded'),'-PackageDirectory',str(CACHE),
                    '-Destination',str(output),'-IdentitySeed',sha(metadata_path.read_bytes())+sha(originals[0].read_bytes())],check=True)
    contract = json.loads((output/'contracts.json').read_text(encoding='utf-8-sig'))
    bindings = []
    for module in contract['modules']:
        folder = CODE/'managed/decoded' if module['origin']=='original' else CACHE if module['origin']=='package' else output
        target = ASSETS / (module['name']+'.bytes')
        write(target,(folder/module['file']).read_bytes())
        guid = meta(target)
        bindings.append(dict(Name=module['name'],guid=guid,Sha256=sha(target.read_bytes()),ModuleId=module['moduleId']))
    source = ROOT/'unity/Assets/Official/World/Map/14/Source/templates.json'
    source_receipt = ROOT/'assets/reference/official-map/map14-source-manifest.json'
    evidence = json.loads(source_receipt.read_bytes())
    expected = next(r['sha256'] for r in evidence['outputs'] if r['path']==source.relative_to(ROOT).as_posix())
    if sha(source.read_bytes()) != expected: raise ValueError('Original Map14 templates changed')
    templates = json.loads(source.read_bytes())
    entities = []
    for class_id in (6570,6571,6572,6573):
        components = []
        for name, info in templates[str(class_id)].items():
            info = dict(info)
            if name=='GridTransformComponent':
                center=info['RealCenter']; info['RealCenter']=dict(x=center[0],y=center[1])
            components.append(dict(Type=name,Fields=field_graph(info)))
        entities.append(dict(ClassId=class_id,Components=components))
    prefab_asset = ASSETS/'hero-shared-prefabs.json'
    write(prefab_asset,encode(dict(Entities=entities)))
    prefab_guid = meta(prefab_asset)
    asmdef = RUNTIME/'Zjcs.Offline.World.NativeMap14SharedEntities.asmdef'
    write(asmdef,encode(dict(name='Zjcs.Offline.World.NativeMap14SharedEntities',rootNamespace='Zjcs.Offline.World.NativeMap14SharedEntities',references=['Config'],autoReferenced=False)))
    for path in [RUNTIME,asmdef,*RUNTIME.glob('*.cs')]: runtime_meta(path)
    script_guid = hashlib.md5(('zjcs-official:Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedAssemblyCatalog.cs').encode()).hexdigest()
    catalog = RESOURCES/'shared-assembly-catalog.asset'
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: '+script_guid+', type: 3}\n  m_Name: shared-assembly-catalog\n  m_EditorClassIdentifier: \n  Assemblies:\n'
    for b in bindings:
        text += f"  - Name: {b['Name']}\n    Data: {{fileID: 4900000, guid: {b['guid']}, type: 3}}\n    Sha256: {b['Sha256']}\n    ModuleId: {b['ModuleId']}\n"
    text += f'  Prefabs: {{fileID: 4900000, guid: {prefab_guid}, type: 3}}\n  PrefabsSha256: {sha(prefab_asset.read_bytes())}\n'
    write(catalog,text.encode()); meta(catalog); meta(ASSETS)
    for path in ASSETS.glob('*.txt'): meta(path)
    sources = [CODE/'common-source/Common'/name for name in ('EcEntity.cs','EcComponent.cs','EcDataCenterParser.cs','GridTransformComponent.cs','GridTransformComponentInfo.cs','WorldStageComponent.cs','SubStageStateInfo.cs')]
    formatter = CODE/'common-source/MessagePack.Formatters.Common/SubStageStateInfoFormatter.cs'
    outputs = [p for directory in (RUNTIME,ASSETS,CONFIG) for p in directory.rglob('*') if p.is_file()]
    outputs += [Path(str(RUNTIME)+'.meta'),Path(str(ASSETS)+'.meta'),catalog,Path(str(catalog)+'.meta')]
    outputs += [Path(str(CONFIG)+'.meta')]
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=14,assemblies=[record(p) for p in originals],
        inputs=[record(p) for p in [manifest_path,metadata_path,source,source_receipt,formatter,*sources]],
        contract=dict(type='GFrameCore.IGSerializable',type_index=interface['index'],method='System.UInt16 GetOpCode()',method_index=definition[9]),
        packages=package_sources,logging_calls=contract['loggingCalls'],config_contract=config_abi,
        tools=[record(Path(__file__).resolve()),record(ROOT/'scripts/prepare_unity_shared_ec_contracts.ps1')],outputs=[record(p) for p in outputs],
        validation=record(ROOT/'unity/Assets/Game/Editor/Map14/SharedEntities/OfficialMap14SharedEcAcceptance.cs'),
        operation='Ship unchanged original Console/Luban/Framework.FirstPass.Runtime IL as explicitly bound local TextAssets. Recover the complete GConfig.IConfig marker and exact IGSerializable ABI from APK metadata; forward seven application logging signatures to pinned public ZLogger APIs. Original ContainerInfo and all other table/shared class methods remain in Console IL. Restore the four original 11-component client prefabs in original order and initialize original info using source field data. SubStageStateInfo.Select follows its original generated client formatter, which consumes without assignment. No server entity state, reward or save rules are supplied.',
        limitations=['Additional AOT dependencies, full serializers, formal view/input/state/save integration and Windows Player remain required. The supplied GFrameCore contract covers only IGSerializable, not its full original assembly. Logging catch paths and all public APIs of Console are not validated.'])))
    print('Local original shared EC payload: 7 assemblies, 4 source client prefabs / 44 components.')


if __name__=='__main__': build()
