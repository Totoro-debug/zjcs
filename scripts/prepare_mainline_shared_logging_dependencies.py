"""Add the pinned ZLogger dependency closure without regenerating original EC data."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_unity_map14_shared_ec_runtime import ASSETS, CACHE, RESOURCES, LOGGING_PACKAGES, meta, packages

LOCK = json.loads(LOGGING_PACKAGES.read_bytes())
MODULES = {row['name']: row['module_id'] for row in LOCK['packages']}


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    catalog = RESOURCES / 'shared-assembly-catalog.asset'
    original = catalog.read_text(encoding='utf8')
    # Preserve every existing original module and binding, including extensions
    # owned by later serialization migrations. Only append the locked packages.
    import re
    for name, guid, digest in re.findall(r'  - Name: (.+)\n    Data: \{fileID: 4900000, guid: ([a-f0-9]+), type: 3\}\n    Sha256: ([a-f0-9]+)', original):
        payload = ASSETS / (name + '.bytes')
        if record(payload)['sha256'] != digest or f'guid: {guid}\n' not in Path(str(payload)+'.meta').read_text():
            raise ValueError('Preserve changed shared assembly: ' + name)
    sources = packages(MODULES)
    outputs = []
    additions = ''
    for row in sources:
        name = row['name']
        payload = ASSETS / (name + '.bytes')
        raw = (CACHE / (name + '.dll')).read_bytes()
        if hashlib.sha256(raw).hexdigest() != next(r['dll_sha256'] for r in LOCK['packages'] if r['name'] == name):
            raise ValueError('Pinned DLL identity differs: ' + name)
        if payload.exists() and payload.read_bytes() != raw:
            raise ValueError('Preserve changed logging dependency: ' + name)
        payload.write_bytes(raw)
        guid = meta(payload)
        block = (f'  - Name: {name}\n    Data: {{fileID: 4900000, guid: {guid}, type: 3}}\n'
                 f"    Sha256: {row['dll_sha256']}\n    ModuleId: {MODULES[name]}\n")
        if f'  - Name: {name}\n' in original:
            if block not in original:
                raise ValueError('Preserve changed logging binding: ' + name)
        else:
            additions += block
        for license in ASSETS.glob(name + '-license*.txt'):
            meta(license)
            outputs.extend((record(license), record(Path(str(license)+'.meta'))))
        outputs.extend((record(payload), record(Path(str(payload)+'.meta'))))
    if additions:
        catalog.write_text(original.replace('  Prefabs:', additions+'  Prefabs:'), encoding='utf8', newline='\n')
    receipt = ROOT / 'assets/reference/official-map/mainline-shared-logging-source-manifest.json'
    receipt.write_text(json.dumps(dict(
        operation='Complete the pinned ZLogger 2.5.10 netstandard dependency ABI; retain all original EC and framework IL. Unity supplies System.Memory, Buffers, Numerics.Vectors and Tasks.Extensions; runtime validation is documented on the owning feature page.',
        inputs=[record(ASSETS/'ZLogger.bytes'),record(ASSETS/'Framework.FirstPass.Runtime.bytes')],
        packages=sources, module_ids=MODULES,
        tools=[record(Path(__file__)),record(LOGGING_PACKAGES),record(ROOT/'scripts/prepare_unity_map14_shared_ec_runtime.py'),record(ROOT/'scripts/prepare_unity_shared_ec_contracts.ps1')],
        integration=[record(ROOT/path) for path in (
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedLogging.cs',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedLogging.cs.meta',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedAssemblies.cs',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/MainlineSharedWorld.cs',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.Logging.cs',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.Logging.cs.meta')],
        outputs=outputs, binding=record(catalog)), ensure_ascii=False, indent=2)+'\n',encoding='utf8',newline='\n')
    print(f'Bound {len(MODULES)} pinned ZLogger dependency assemblies with package licenses.')


if __name__ == '__main__':
    build()
