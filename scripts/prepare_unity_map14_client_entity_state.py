"""Record the native original container state/event bridge and explicit bindings."""
import json
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_sound_runtime import runtime_meta
CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
RECEIPT = ROOT / 'assets/reference/official-map/map14-client-entity-state-source-manifest.json'
def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))
def prepare():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    if manifest['manifest'] != '871_156598':
        raise ValueError('Native client state version differs')
    assemblies = [CODE / 'managed/decoded' / name for name in ('Console.dll', 'Assembly-CSharp.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Original assembly changed: ' + path.name)
    inputs = [CODE / 'common-source/Common' / (name + '.cs') for name in (
        'EcContainer', 'EcEntity', 'EcEntityIdentifier', 'EntitySourceComponent', 'EntityOwnershipData',
        'GridTransformComponent', 'EntityUtils')]
    inputs += [CODE / 'client-source' / (name + '.cs') for name in ('MyContainer', 'MyClientEntityHandler', 'MyClientStoryNPCEntityHandler')]
    for event in ('Start', 'Destroy', 'PrepareReplace', 'Replace'):
        inputs += [CODE / 'common-source/Common' / ('EcContainer' + event + 'EntityEventDelegate.cs'),
                   CODE / 'common-source/Common' / ('IEcContainer' + event + 'EntityEventReceiver.cs')]
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14ClientEntities'
    refs = ['Zjcs.Offline.World.NativeMap14SharedEntities', 'Zjcs.Offline.World.NativeMap14SharedViews',
            'Zjcs.Official.EntityContainers', 'Zjcs.Official.EntityLoading', 'Zjcs.Official.EntityLifecycle',
            'Zjcs.Official.EntityTipDotViews', 'Zjcs.Official.EntityViews', 'Zjcs.Official.StoryPlayables',
            'Zjcs.Official.World.NativeTerrain', 'Zjcs.Official.World.Map13EntityVisibilityViews']
    editor = ROOT / 'unity/Assets/Game/Editor/Map14/ClientEntities'
    outputs = []
    for folder, name, references, platforms in (
        (runtime, 'Zjcs.Offline.World.NativeMap14ClientEntities', refs, []),
        (editor, 'Zjcs.Editor.Map14.ClientEntities', refs + ['Zjcs.Offline.World.NativeMap14ClientEntities', 'Zjcs.Editor.Map14.EntityContainers'], ['Editor'])):
        target = folder / (name + '.asmdef')
        write(target, encode(dict(name=name, autoReferenced=False, references=references, includePlatforms=platforms)))
        for path in sorted(folder.iterdir()):
            if path.is_file() and path.suffix != '.meta':
                runtime_meta(path); outputs += [path, Path(str(path) + '.meta')]
        runtime_meta(folder); outputs.append(Path(str(folder) + '.meta'))
    dependencies = [ROOT / 'unity/Assets/Game/Runtime' / path for path in (
        'Offline/World/NativeMap14SharedEntities/NativeMap14SharedEventSubscription.cs',
        'Offline/World/NativeMap14SharedEntities/NativeMap14SharedRoot.cs',
        'Offline/World/NativeMap14SharedEntities/NativeMap14SharedData.cs',
        'Offline/World/NativeMap14SharedViews/NativeMap14SharedViewEntity.cs',
        'Official/EntityContainers/EntityContainerBindings.cs', 'Official/EntityLoading/EntityLoadingBindings.cs')]
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14, assemblies=[record(p) for p in assemblies],
        inputs=[record(p) for p in inputs], tools=[record(Path(__file__).resolve())],
        dependencies=[record(p) for p in dependencies], outputs=[record(p) for p in outputs],
        operation='Bind the unchanged original Console container, entity instantiation and mutable state to original migrated client container interfaces. Keep one reference-identity wrapper per original entity across identifier replacement. Subscribe through the original four dispatchers, preserving original event ordering and boxed original identifier values. Require an explicit low-memory policy and movement implementation; supply no server world state.',
        validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14ClientEntityStateAcceptance.Run', graphicalPlayMode=True,
            checks=['Original container add/prepare/replace/remove methods and callback order',
                    'Stable entity projection and dictionary identity across identifier replacement',
                    'Original prefab instantiation and source/ownership/grid writes',
                    'Native subscription disposal', 'Original client container/loading/four-statue regression']),
        limitations=['Movement is an explicit unbound service.', 'Formal native world population, prefab loader and map-host binding, pointer/UI/save, full ProjectTools.Validate and Windows Player remain required.'])))
    print('Recorded native client entity state bridge:', len(outputs), 'outputs')
if __name__ == '__main__':
    prepare()
