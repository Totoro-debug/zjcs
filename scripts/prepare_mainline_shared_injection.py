"""Restore the inspected original Core injection call; do not ship AOT stubs."""
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import sys

from official_apk import ROOT
from official_metadata import Metadata
from prepare_unity_map14_shared_ec_runtime import ASSETS, RESOURCES, meta
from validation_workspace import validation_directory
from prepare_unity_story_sound_runtime import runtime_meta

sys.path.insert(0, str(ROOT / '.tools/character-art-python'))
from capstone import Cs, CS_ARCH_ARM64, CS_MODE_LITTLE_ENDIAN
from elftools.elf.elffile import ELFFile

SOURCE = ROOT / '.tools/source-packages/current/884_156692/official-code'
RECEIPT = ROOT / 'assets/reference/official-map/mainline-shared-injection-source-manifest.json'


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def existing_binding(catalog, payload):
    """Validate all live bindings before replacing this port's own payload."""
    original = catalog.read_text(encoding='utf8')
    pattern = r'  - Name: (.+)\n    Data: \{fileID: 4900000, guid: ([a-f0-9]+), type: 3\}\n    Sha256: ([a-f0-9]+)\n    ModuleId: ([a-f0-9-]+)\n'
    matches = list(re.finditer(pattern, original))
    if len(matches) != original.count('  - Name: '):
        raise ValueError('Preserve an unknown shared assembly binding')
    for match in matches:
        name, guid, digest, _ = match.groups()
        current = ASSETS/(name+'.bytes')
        if record(current)['sha256'] != digest or f'guid: {guid}\n' not in Path(str(current)+'.meta').read_text():
            raise ValueError('Preserve changed shared assembly: '+name)
    owned = [m for m in matches if m[1] == 'Framework.Core']
    if owned or payload.exists() or RECEIPT.exists():
        if len(owned) != 1 or not RECEIPT.exists() or not payload.exists():
            raise ValueError('Preserve an unrecorded Core payload or binding')
        previous = json.loads(RECEIPT.read_bytes())
        for current in (payload, Path(str(payload)+'.meta')):
            rows = [r for r in previous['outputs'] if r['path'] == current.relative_to(ROOT).as_posix()]
            if len(rows) != 1 or record(current)['sha256'] != rows[0]['sha256']:
                raise ValueError('Preserve changed original injection port')
        for row in previous['outputs']:
            if record(ROOT/row['path'])['sha256'] != row['sha256']:
                raise ValueError('Preserve changed injection dependency: '+row['path'])
        if owned[0][4] != previous['contract']['module_id']:
            raise ValueError('Preserve a changed Core module identity')
        return original, owned[0].group()
    return original, None


def build():
    payload = ASSETS/'Framework.Core.bytes'
    catalog = RESOURCES/'shared-assembly-catalog.asset'
    original, old_block = existing_binding(catalog, payload)
    evidence_path = SOURCE / 'aot-metadata/source-manifest.json'
    evidence = json.loads(evidence_path.read_bytes())
    if evidence['manifest'] != '884_156692':
        raise ValueError('Review selected AOT input identity')
    required = [SOURCE/'aot-metadata/analysis-elf.so', SOURCE/'aot-metadata/DummyDll/Framework.Core.dll',
                SOURCE/'aot-metadata/DummyDll/Microsoft.Extensions.DependencyInjection.Abstractions.dll',
                SOURCE/'aot-metadata/dump.cs']
    for path in required:
        expected = next(r['sha256'] for r in evidence['outputs'] if r['path'] == path.relative_to(ROOT).as_posix())
        if record(path)['sha256'] != expected:
            raise ValueError('Original AOT input differs: ' + str(path))
    metadata = SOURCE / 'global-metadata.dat'
    if record(metadata)['sha256'] != 'c0fe5e062fb3ed1814781e54fd1b4859757970632655138adc091820ded88e0e':
        raise ValueError('Selected original AOT metadata differs')
    if record(required[0])['sha256'] != '86b5d4d886aa45102cf990b4a827686e3fba74734251f65ff32fe06878f93bb4':
        raise ValueError('Review the original restored native image')
    dump = required[3].read_text(encoding='utf8')
    if not re.search(r'RVA: 0xC4504F8[^\n]*\n\s*\|\-ServiceProviderServiceExtensions.GetService<object>',dump):
        raise ValueError('Review original generic service lookup identity')
    if not re.search(r'RVA: 0xC102B78[^\n]*\n\s*\|\-ContainerTools.InjectPropertiesAndFields<object>',dump):
        raise ValueError('Review original generic instance injection identity')
    attribute_dump = dump[dump.index('public abstract class Attribute : '):]
    if not re.search(r'RVA: 0x10912910[^\n]*\n\s*protected void \.ctor\(\)',attribute_dump):
        raise ValueError('Review original Attribute constructor branch target')
    launch = SOURCE/'managed/decoded/Launch.dll'
    code_manifest = SOURCE.parent/'code-manifest.json'
    code = json.loads(code_manifest.read_bytes())
    if code['manifest'] != '884_156692' or record(launch)['sha256'] != code['assemblies']['Launch.dll']['decoded_sha256']:
        raise ValueError('Original Launch assembly differs')
    version_source = SOURCE/'launch-source/IVersionManager.cs'
    if record(version_source)['sha256'] != '38d423fe71146f53977170c9bb29d5d7390fe6c19bd54f647d3b5c71be5a0311':
        raise ValueError('Review original decompiled version interface')
    version_target = ROOT/'unity/Assets/Game/Runtime/Official/SharedLaunch/IVersionManager.cs'
    version_text = version_source.read_text(encoding='utf8')
    if version_target.exists() and version_target.read_text(encoding='utf8') != version_text:
        raise ValueError('Preserve a changed version interface')
    with required[0].open('rb') as stream:
        elf = ELFFile(stream)
        segments = [dict(s.header) for s in elf.iter_segments() if s['p_type'] == 'PT_LOAD']

        def native(address, count):
            segment, = [s for s in segments if s['p_vaddr'] <= address < s['p_vaddr']+s['p_filesz']]
            stream.seek(address-segment['p_vaddr']+segment['p_offset'])
            return stream.read(count)

        raw = native(0xb872300, 0xd0)
        if hashlib.sha256(raw).hexdigest() != '9eaa8f933136f56aa8df94fdaffd88d32e429a17ca70058925c59f9299f019c1':
            raise ValueError('Original Inject native body differs')
        constructor = native(0xb872148, 8)
        if hashlib.sha256(constructor).hexdigest() != '222882407fb2e6ff6d1c1606322789dc6c01539d762510e83c098a89376c553c':
            raise ValueError('Original InjectAttribute constructor differs')
        injector_bodies = []
        for name, address, length, expected in (
                ('constructor',0xb86cbc8,48,'26b32cda3ff7162b71ec10f329a23278ee4ff90afa6e362ef7690fa0501ae718'),
                ('Inject',0xb872c30,128,'d6af2b235f0f07e6701c88b0d241c2c0296f8c0babb9d54e1f6a5bc2bf0ce4c9')):
            body = native(address,length)
            if hashlib.sha256(body).hexdigest() != expected:
                raise ValueError('Original instance injector body differs: '+name)
            injector_bodies.append(dict(method=name,address=hex(address),length=length,sha256=expected,
                instructions=[dict(address=hex(i.address),op=i.mnemonic,arguments=i.op_str)
                              for i in Cs(CS_ARCH_ARM64,CS_MODE_LITTLE_ENDIAN).disasm(body,address)]))
        selector_bodies = []
        for name, address, length, expected in (
                ('Properties',0xb872cb0,452,'d02f60e98d1d801899065f0500e7b774580c7345165cf1347888c2495eaca043'),
                ('closure.constructor',0xb872e74,8,'c0a768f7d72f8941a4f284d48b281f3603dcd22baee034f613fef7bbefae6b97'),
                ('constructor',0xb872e7c,136,'ae44f2d19e6deb3fce48013a42e583421369ae24a655be0b0297abb354ae4dbb'),
                ('static.constructor',0xb872f04,156,'f5a9f46ec5395874bbd23502d64d6244702aa64b836e36950249cd2127d5e3ae'),
                ('singleton.static.constructor',0xb872fa0,104,'607969b95d39c5abb0fe7ca20c53c71f89b85e019b715ba8e4cdcc2e99fee5e0'),
                ('singleton.constructor',0xb873008,8,'595b3db07fdcd8c91af0795b96d0f36f848b5d0be65242f87c8b7df86000aac2'),
                ('declared.properties',0xb873010,40,'2e2269bc12050137e8439ea10f8161a29082f909a8d54e3e2e50025c6b6e7205'),
                ('attribute.predicate',0xb873038,100,'6916b4474cfd050829be4b06e680d6b91728c7a64759b7952f44780e77be8da8'),
                ('service.info',0xb87309c,40,'1702c219d84402c8ffa17bd34927bc7b3494407fabe2248a28b7e5c52da53b85'),
                ('service.details',0xb8730c4,96,'afda428bf58fa5059e0ca49c2c1bbadb74d85cb9612eb31945bae89bf437ae29'),
                ('selector',0xb873124,920,'b054cbac54ee124dccd0347ed6772fc21bc6571644f6a6d119f8a9574ff36905'),
):
            body = native(address,length)
            if hashlib.sha256(body).hexdigest() != expected:
                raise ValueError('Original property selector body differs: '+name)
            selector_bodies.append(dict(method=name,address=hex(address),length=length,sha256=expected,
                instructions=[dict(address=hex(i.address),op=i.mnemonic,arguments=i.op_str)
                              for i in Cs(CS_ARCH_ARM64,CS_MODE_LITTLE_ENDIAN).disasm(body,address)]))
        pointer, = struct.unpack('<Q', native(0x1245d4b0, 8))
        encoded, = struct.unpack('<Q', native(pointer, 8))
        index = (encoded & 0x1fffffff) >> 1
        types = Metadata(metadata.read_bytes())
        interface = types.type_for(index)
        if encoded >> 29 != 1 or interface['byval'] != index or (interface['namespace'],interface['name']) != ('Framework.DependencyInjection.Abstractions','IInstanceInjector'):
            raise ValueError('Original Inject interface dispatch identity differs')
    instructions = [dict(address=hex(i.address), op=i.mnemonic, arguments=i.op_str)
                    for i in Cs(CS_ARCH_ARM64,CS_MODE_LITTLE_ENDIAN).disasm(raw,0xb872300)]
    tool = ROOT / 'scripts/prepare_mainline_shared_injection_contract.ps1'
    destination = validation_directory() / 'injection-contract'
    seed = hashlib.sha256(raw+constructor+tool.read_bytes()).hexdigest()
    subprocess.run(['pwsh','-NoProfile','-File',str(tool),'-OriginalCore',str(required[1]),
                    '-OriginalLaunch',str(launch),
                    '-DependencyInjectionAbstractions',str(ASSETS/'Microsoft.Extensions.DependencyInjection.Abstractions.bytes'),
                    '-DryIoc',str(ASSETS/'DryIoc.bytes'),'-OriginalFramework',str(ASSETS/'Framework.FirstPass.Runtime.bytes'),
                    '-Destination',str(destination),'-IdentitySeed',seed],check=True)
    binding = json.loads((destination/'contract.json').read_text(encoding='utf-8-sig'))
    if catalog.read_text(encoding='utf8') != original:
        raise ValueError('Shared bindings changed during generation; review and retry')
    payload.write_bytes((destination/'Framework.Core.dll').read_bytes())
    guid = meta(payload)
    block = (f'  - Name: Framework.Core\n    Data: {{fileID: 4900000, guid: {guid}, type: 3}}\n'
             f"    Sha256: {record(payload)['sha256']}\n    ModuleId: {binding['module_id']}\n")
    if old_block is not None:
        updated = original.replace(old_block,block)
    else:
        updated = original.replace('  Prefabs:',block+'  Prefabs:')
    catalog.write_text(updated,encoding='utf8',newline='\n')
    version_target.write_text(version_text,encoding='utf8',newline='\n')
    if not Path(str(version_target)+'.meta').exists():
        runtime_meta(version_target)
    RECEIPT.write_text(json.dumps(dict(manifest='884_156692',
        operation='Port the five inspected original injection types and two original selector closures, native provider.GetService<IInstanceInjector>().Inject(instance) call and original DryIocInstanceInjector constructor/ContainerTools.InjectPropertiesAndFields<object> call. Copy the complete decompiled Launch.IVersionManager interface with normalized line endings after checking its six original method signatures. Original Console and Framework.FirstPass.Runtime IL remain unchanged. Do not ship dummy method bodies.',
        inputs=[record(p) for p in [evidence_path,metadata,*required,code_manifest,launch,version_source,ASSETS/'Console.bytes',ASSETS/'Microsoft.Extensions.DependencyInjection.Abstractions.bytes',ASSETS/'DryIoc.bytes',ASSETS/'Framework.FirstPass.Runtime.bytes']],
        native=dict(address='0xb872300',length=len(raw),sha256=hashlib.sha256(raw).hexdigest(),instructions=instructions,
                    service_lookup='Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<object>',service_lookup_address='0xc4504f8',
                    interface_type_index=index,interface_dispatch=interface['namespace']+'.'+interface['name'],
                    attribute_constructor_address='0xb872148',attribute_constructor_sha256=hashlib.sha256(constructor).hexdigest(),attribute_base_constructor_address='0x10912910'),
        instance_injector=dict(bodies=injector_bodies,target='DryIoc.ContainerTools.InjectPropertiesAndFields<object>',target_address='0xc102b78'),
        property_selector=dict(bodies=selector_bodies),
        contract=binding,tools=[record(Path(__file__)),record(tool),record(ROOT/'scripts/fixtures/mainline_shared_injection.cs')],
        integration=[record(ROOT/path) for path in (
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.Injection.cs',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.Injection.cs.meta',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.cs')],
        outputs=[record(p) for p in (payload,Path(str(payload)+'.meta'),version_target,Path(str(version_target)+'.meta'))],binding=record(catalog),
        limitations=['This is a partial Core port, not the full original Framework.Core assembly.',
                    'Dummy RVA/token and JetBrains static analysis annotations are excluded; runtime AttributeUsage is preserved.',
                    'Original container registration, offline client services, player startup, map interaction and Windows Player remain pending.']),
        ensure_ascii=False,indent=2)+'\n',encoding='utf8',newline='\n')
    print('Bound inspected Core Inject, property marker and IInstanceInjector ABI.')


if __name__ == '__main__':
    build()
