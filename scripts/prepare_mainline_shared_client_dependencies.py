"""Bind pinned public client dependencies required by unchanged original IL."""
import base64
import hashlib
import json
from pathlib import Path
import urllib.request
import zipfile

from official_apk import ROOT
from prepare_mainline_shared_injection import existing_binding
from prepare_unity_map14_shared_ec_runtime import ASSETS, CACHE, RESOURCES, bind_licenses, meta

LOCK = ROOT/'assets/reference/official-map/mainline-shared-client-packages.json'
RECEIPT = ROOT/'assets/reference/official-map/mainline-shared-client-dependencies-source-manifest.json'


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    catalog = RESOURCES/'shared-assembly-catalog.asset'
    original, _ = existing_binding(catalog, ASSETS/'Framework.Core.bytes')
    if RECEIPT.exists():
        for row in json.loads(RECEIPT.read_bytes())['outputs']:
            if record(ROOT/row['path'])['sha256'] != row['sha256']:
                raise ValueError('Preserve a changed client dependency: '+row['path'])
    additions = ''
    outputs, assemblies = [], []
    packages = json.loads(LOCK.read_bytes())['packages']
    CACHE.mkdir(parents=True,exist_ok=True)
    for row in packages:
        name = row['name']
        package = CACHE/(row['package'].lower()+'.'+row['version']+'.nupkg')
        if not package.exists():
            package.write_bytes(urllib.request.urlopen(row['url'],timeout=45).read())
        raw = package.read_bytes()
        if base64.b64encode(hashlib.sha512(raw).digest()).decode() != row['package_sha512']:
            raise ValueError('Pinned client package digest differs: '+name)
        with zipfile.ZipFile(package) as archive:
            payload = archive.read('lib/'+row['framework']+'/'+name+'.dll')
            if 'license_entry' in row:
                license = archive.read(row['license_entry'])
            else:
                license_cache = CACHE/(name+'-license.txt')
                if not license_cache.exists():
                    license_cache.write_bytes(urllib.request.urlopen(row['license_url'],timeout=45).read())
                license = license_cache.read_bytes()
        if hashlib.sha256(payload).hexdigest() != row['dll_sha256'] or hashlib.sha256(license).hexdigest() != row['license_sha256']:
            raise ValueError('Pinned client DLL or license differs: '+name)
        for target, content in ((ASSETS/(name+'.bytes'),payload),(ASSETS/(name+'-license.txt'),license)):
            if target.exists() and target.read_bytes() != content:
                raise ValueError('Preserve an unrelated client dependency: '+str(target))
        target = ASSETS/(name+'.bytes')
        target.write_bytes(payload)
        guid = meta(target)
        license_target = ASSETS/(name+'-license.txt')
        license_target.write_bytes(license)
        meta(license_target)
        block = (f'  - Name: {name}\n    Data: {{fileID: 4900000, guid: {guid}, type: 3}}\n'
                 f"    Sha256: {row['dll_sha256']}\n    ModuleId: {row['module_id']}\n")
        if f'  - Name: {name}\n' in original:
            if block not in original:
                raise ValueError('Preserve an unknown client binding: '+name)
        else:
            additions += block
        assemblies.append(dict(Name=name,ModuleId=row['module_id']))
        outputs.extend(record(p) for p in (target,Path(str(target)+'.meta'),license_target,Path(str(license_target)+'.meta')))
    source = ROOT/'.tools/source-packages/current/884_156692/official-code'
    code_manifest = source.parent/'code-manifest.json'
    original_name = 'GFrameCore.Extensions.Cloneable'
    original_path = source/'managed/decoded'/(original_name+'.dll')
    original_evidence = json.loads(code_manifest.read_bytes())['assemblies'][original_name+'.dll']
    if record(original_path)['sha256'] != original_evidence['decoded_sha256']:
        raise ValueError('Original cloneable assembly differs from its decoded APK receipt')
    payload = original_path.read_bytes()
    target = ASSETS/(original_name+'.bytes')
    if target.exists() and target.read_bytes() != payload:
        raise ValueError('Preserve a changed original cloneable dependency')
    target.write_bytes(payload)
    guid = meta(target)
    module_id = '777f61be-a30e-46f3-85ce-af1f2dd5f4ca'
    block = (f'  - Name: {original_name}\n    Data: {{fileID: 4900000, guid: {guid}, type: 3}}\n'
             f"    Sha256: {record(target)['sha256']}\n    ModuleId: {module_id}\n")
    if f'  - Name: {original_name}\n' in original:
        if block not in original:
            raise ValueError('Preserve an unknown original cloneable binding')
    else:
        additions += block
    assemblies.append(dict(Name=original_name,ModuleId=module_id))
    outputs.extend(record(p) for p in (target,Path(str(target)+'.meta')))
    if catalog.read_text(encoding='utf8') != original:
        raise ValueError('Shared bindings changed during generation; review and retry')
    if additions:
        catalog.write_text(original.replace('  Prefabs:',additions+'  Prefabs:'),encoding='utf8',newline='\n')
    bind_licenses(catalog)
    runtime = ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities'
    validation = ROOT/'unity/Assets/Game/Editor/MainlineWorldStartup'
    RECEIPT.write_text(json.dumps(dict(
        operation='Restore pinned public DryIoc for original client injection, LiteDB for PlayerDataWrap/IMongoObj identities, R3 for original player state observers and the unchanged APK cloneable assembly for original monster formations. Preserve original application IL; use publisher packages and licenses.',
        inputs=[record(ASSETS/name) for name in ('Framework.Core.bytes','Framework.FirstPass.Runtime.bytes','Console.bytes')]+[record(LOCK),record(code_manifest),record(original_path)],
        original_cloneable=dict(manifest='884_156692',extraction=original_evidence),
        code_inputs=[record(source/path) for path in (
            'client-source/UtilsHandlerBinder.cs','common-source/MeManager.cs',
            'common-source/Common/Player.cs','common-source/Common/PlayerDataWrap.cs',
            'common-source/Common/PlayerPlayStateHandler.cs','common-source/Common/PlayerPlayFightStateObserver.cs',
            'common-source/Common/BattleFormula.cs','common-source/Common/Formula.cs',
            'common-source/Common/TimeManager.cs')],
        integration=[record(runtime/'MainlineSharedClientServices.cs'),record(runtime/'MainlineSharedClientServices.cs.meta')],
        validation=[record(path) for path in sorted(validation.iterdir()) if path.is_file()],
        packages=packages,assemblies=assemblies,outputs=outputs,binding=record(catalog),
        tools=[record(Path(__file__)),record(ROOT/'scripts/prepare_unity_map14_shared_ec_runtime.py')]),
        ensure_ascii=False,indent=2)+'\n',encoding='utf8',newline='\n')
    print('Bound pinned original client dependency group and publisher license.')


if __name__ == '__main__':
    build()
