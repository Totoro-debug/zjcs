"""Bind the original container definition and root lifecycle without server state."""
import hashlib
import json
from pathlib import Path
from official_apk import Apk, ROOT
from prepare_unity_map14_shared_ec_runtime import meta
from prepare_unity_story_sound_runtime import runtime_meta


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def prepare():
    apk = Apk.current()
    if apk.version != '871_156598':
        raise ValueError('Selected map source version differs')
    obj = apk.asset('config_luban_ec', '/EC/container.g.bytes')
    raw = obj.read().m_Script.encode('utf-8', 'surrogateescape')
    folder = ROOT / 'unity/Assets/Official/World/Map/14/SharedRoot'
    resources = folder / 'Resources/Map14/SharedRoot'
    resources.mkdir(parents=True, exist_ok=True)
    payload = resources / 'container.g.bytes'
    payload.write_bytes(raw)
    binding = resources / 'container-binding.json'
    binding.write_text(json.dumps(dict(Resource='Map14/SharedRoot/container.g', Sha256=record(payload)['sha256']), indent=2)+'\n', encoding='utf-8')
    for path in folder.rglob('*'):
        if not path.name.endswith('.meta'): meta(path)
    meta(folder)
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedRoot.cs'
    acceptance = ROOT / 'unity/Assets/Game/Editor/Map14/HeroStatues/OfficialMap14SharedRootAcceptance.cs'
    for path in (runtime, acceptance): runtime_meta(path)
    code = ROOT / '.tools/source-packages/current/871_156598/official-code'
    code_manifest = code.parent / 'code-manifest.json'
    identities = json.loads(code_manifest.read_bytes())
    if identities['manifest'] != apk.version:
        raise ValueError('Original code and assets belong to different source versions')
    for name in ('Console.dll', 'Luban.dll'):
        if record(code / 'managed/decoded' / name)['sha256'] != identities['assemblies'][name]['decoded_sha256']:
            raise ValueError('Original shared assembly differs from the pinned input: ' + name)
    names = ['EntityHandler','EntityUtils','EcEntity','EcComponent','EcContainer','GridGroundComponent',
             'AreaAgentComponent','AreaAgentComponentInfo','MapComponent','WorldComponent',
             'GridVisibilityAgentComponent','GridVisibilityAgentHandle','ContainerInfo','TbContainerInfo','ContainerInfoParser',
             'IEcContainerStartEventReceiver','IEcContainerStartEntityEventReceiver','IEcContainerRemoveEntityEventReceiver',
             'IEcContainerDestroyEntityEventReceiver','EcContainerStartEventDelegate','EcContainerStartEntityEventDelegate',
             'EcContainerRemoveEntityEventDelegate','EcContainerDestroyEntityEventDelegate','EventDelegateBase','EventDelegateBasicBase']
    sources = [code / 'common-source/Common' / (name+'.cs') for name in names]
    sources += [code / 'common-source/ProfilerScope.cs', code / 'managed/decoded/Console.dll', code / 'managed/decoded/Luban.dll', code_manifest]
    outputs = [p for p in folder.rglob('*') if p.is_file()] + [Path(str(folder)+'.meta'), runtime, Path(str(runtime)+'.meta'), acceptance, Path(str(acceptance)+'.meta')]
    outputs += [acceptance.parent / 'OfficialMap14SharedDataAcceptance.cs']
    outputs += [runtime.parent / 'NativeMap14SharedEventSubscription.cs']
    receipt = ROOT / 'assets/reference/official-map/map14-shared-root-source-manifest.json'
    receipt.write_text(json.dumps(dict(manifest=apk.version, mapClassId=14,
        operation='Invoke unchanged original EntityUtils/EntityHandler instantiation and EcContainer root lifecycle; decode unchanged official EC/container table with original TbContainerInfo/ByteBuf.',
        table=dict(source=apk.provenance(obj), original_path='Config/Luban/EC/container.g.bytes', payload=record(payload)),
        inputs=[record(path) for path in sources], tools=[record(Path(__file__).resolve())],
        outputs=[record(path) for path in outputs],
        validation=dict(entry='OfficialMap14SharedRootAcceptance.Verify', graphical_play_mode=True,
                        root_components=9, lifecycle_passes=2, cells_per_pass=78400, fresh_placements=2961,
                        checks=['Original container/root lookup', 'Fresh original component identity, order and shared info binding',
                                'Initialized masks and area indices against original lite data',
                                'Startup/removal/destruction ordering and receiver teardown', 'Idempotent disposal']),
        limitations=['Runtime root lifecycle and fresh component instantiation do not define initial world entities, player visibility, region progression, saves or the formal map view.']), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    apk.archive.close()
    print('Bound original Map14 container table and root lifecycle')


if __name__ == '__main__': prepare()
