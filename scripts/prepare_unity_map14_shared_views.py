"""Record original shared grid/stage consumers and their explicit view ABI."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_unity_map13_sources import encode, write
from prepare_unity_story_sound_runtime import CODE, record as original_record, runtime_meta


def record(path):
    entry=original_record(path)
    if path.suffix in ('.cs','.asmdef') and path.is_relative_to(ROOT/'unity'):
        import hashlib
        entry['sha256']=hashlib.sha256(path.read_bytes().replace(b'\r\n',b'\n')).hexdigest()
        entry['hashLineEndings']='LF'
    return entry


def build():
    folder = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedViews'
    runtime_meta(folder)
    event = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedEventSubscription.cs'
    utilities = event.parent/'NativeMap14SharedEntityUtilities.cs'
    for path in [event, utilities, *folder.glob('*.cs'), *folder.glob('*.asmdef')]:
        runtime_meta(path)
    sources = [CODE/'common-source/Common'/name for name in (
        'EcEntity.cs', 'EcEntityIdentifier.cs', 'GridTransformComponent.cs', 'GridTransformComponentInfo.cs',
        'WorldStageComponent.cs', 'EntityUtils.cs', 'EntityHandler.cs', 'IEntityHandler.cs', 'EventDelegateBasicBase.cs', 'EventDelegateBase.cs', 'IEventReceiver.cs',
        'IGridTransformPosUpdatedEventReceiver.cs', 'GridTransformPosUpdatedEventDelegate.cs',
        'VisibilityComponent.cs', 'IEcEntityClientVisibilityUpdatedEventReceiver.cs', 'EcEntityClientVisibilityUpdatedEventDelegate.cs',
        'StageResultAwardItemComponent.cs', 'IEcEntityStageResultItemUpdatedEventReceiver.cs', 'EcEntityStageResultItemUpdatedEventDelegate.cs',
        'EntityPlayerInteractionComponent.cs', 'IEcEntityPlayerInteractionRemoveEventReceiver.cs', 'EcEntityPlayerInteractionRemoveEventDelegate.cs',
        'IEcContainerAddEntityEventReceiver.cs', 'EcContainerAddEntityEventDelegate.cs',
        'IEcEntityWorldStageComponentStateUpdateEventReceiver.cs', 'EcEntityWorldStageComponentStateUpdateEventDelegate.cs',
        'IEcContainerStartEventReceiver.cs', 'IEcContainerStartEntityEventReceiver.cs',
        'IEcContainerRemoveEntityEventReceiver.cs', 'IEcContainerDestroyEntityEventReceiver.cs',
        'IEcContainerPrepareReplaceEntityEventReceiver.cs', 'IEcContainerReplaceEntityEventReceiver.cs',
        'EcContainerStartEventDelegate.cs', 'EcContainerStartEntityEventDelegate.cs',
        'EcContainerRemoveEntityEventDelegate.cs', 'EcContainerDestroyEntityEventDelegate.cs',
        'EcContainerPrepareReplaceEntityEventDelegate.cs', 'EcContainerReplaceEntityEventDelegate.cs')]
    sources.append(CODE/'client-source/UtilsHandlerBinder.cs')
    metadata = json.loads((CODE.parent/'code-manifest.json').read_bytes())
    assemblies = [CODE/'managed/decoded'/name for name in ('Console.dll', 'Assembly-CSharp.dll')]
    for assembly in assemblies:
        if record(assembly)['sha256'] != metadata['assemblies'][assembly.name]['decoded_sha256']:
            raise ValueError('Original application IL version differs')
    outputs = [event, Path(str(event)+'.meta'), utilities, Path(str(utilities)+'.meta'), Path(str(folder)+'.meta')]
    outputs += [p for p in folder.rglob('*') if p.is_file()]
    acceptance = ROOT/'unity/Assets/Game/Editor/Map14/HeroStatues/OfficialMap14HeroStatueAcceptance.cs'
    write(ROOT/'assets/reference/official-map/map14-shared-view-source-manifest.json', encode(dict(
        manifest='871_156598', mapClassId=14, assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sources],
        outputs=[record(p) for p in outputs], tools=[record(Path(__file__).resolve())], validation=record(acceptance),
        operation='Bind original EntityUtils to original EntityHandler without replacing an existing handler and release only the owned binding. Read original EcEntity identity, grid/info, original coordinate conversions, stage state/config/index/activity through unchanged Console IL. Implement twelve selected original void receiver interfaces at runtime (grid, visibility, stage, award-item update, player interaction removal and seven container lifecycle receivers) and register/unregister with original delegate operators; forward original argument identities and previous values to migrated display consumers. Dispose owned additional component projections with the view. No shared rule/dispatch iteration is reimplemented.',
        limitations=['Stage Select remains null in original shared client data; the legacy display DTO int slot is not a shared selector rule.',
            'Entity creation/state restoration, global parser/container host, redirects to another entity, visibility handlers, tip graph, actual UI, saves and Player require further integration.'])))
    print('Original shared grid/stage view binding recorded.')


if __name__ == '__main__':
    build()
