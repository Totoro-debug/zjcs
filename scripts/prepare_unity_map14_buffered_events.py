"""Port the official entity event buffering, signatures, dispatcher pools and replay."""
import json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map14_sources import encode,sha,source_path,write
from prepare_unity_story_sound_runtime import CODE,runtime_meta
from prepare_unity_story_playable_runtime import logging_adapter

def record(p):
    raw=p.read_bytes();entry=dict(path=source_path(p))
    if p.suffix in ('.cs','.asmdef','.py'):
        raw=raw.replace(b'\r\n',b'\n');entry['hashLineEndings']='LF'
    return dict(entry,sha256=sha(raw))

LIFECYCLE='''
    public IBufferEventMapInputs Inputs { get; set; }
    private bool initialized;
    public bool Active { get; private set; }
    public void Initialize()
    {
        if(initialized)return;initialized=true;
        try{InitializeCore();}catch(Exception error){UnityEngine.Debug.LogException(error);}
    }
    public void Dispose()
    {
        if(!initialized)return;
        try{DisposeCore();}catch(Exception error){UnityEngine.Debug.LogException(error);}
        initialized=false;
    }
    public void Activate(){Active=true;try{OnActivate();}catch(Exception error){UnityEngine.Debug.LogException(error);}}
    public void Deactivate(){Active=false;}
'''

def build():
    outputs=[];inputs=[]
    # The unchanged pack parser initializes its folder from this Launch type.
    config_const=CODE/'launch-source/Common/ConfigConst.cs'
    inputs.append(config_const)
    target=ROOT/'unity/Assets/Game/Runtime/Official/SharedLaunch/ConfigConst.cs'
    text=config_const.read_text(encoding='utf-8-sig').replace('namespace Common;','namespace Common\n{')+'\n}\n'
    write(target,text.encode());runtime_meta(target)
    outputs.extend((target,Path(str(target)+'.meta')))
    official=ROOT/'unity/Assets/Game/Runtime/Official/EntityBufferedEvents'
    native=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14BufferedEvents'
    names=['EcEntityBufferEventHandleModule','IEcEntityBufferEventDispatcher','EcEntityBufferEventDispatcherBase','WorldStageBufferEventDispatcher','EntityPlayerInteractionBufferEventDispatcher','EntityPlayerInteractionComponentPlayerRemoveEventMessage']
    for name in names:
        p=CODE/'client-source'/(name+'.cs');inputs.append(p);text=p.read_text(encoding='utf-8-sig')
        text,count=logging_adapter(text)
        text=re.sub(r'(?m)^using (?:Common|Framework(?:\.[\w.]+)?|GameFramework|MessagePipe|R3);\n','',text)
        if name=='EcEntityBufferEventHandleModule':
            text=text.replace('SceneScopeBackgroundServiceBase, IMapScopeBackgroundService, IEcEntityBufferEventHandleModule, IEcContainerAddEntityEventReceiver, IEventReceiver, IEcContainerRemoveEntityEventReceiver','IEcEntityBufferEventHandleModule, IDisposable')
            text=text.replace('private record struct UnsubscribeEventData(IEcEntityBufferEventHandle Handle);','private readonly struct UnsubscribeEventData { public readonly IEcEntityBufferEventHandle Handle; public UnsubscribeEventData(IEcEntityBufferEventHandle handle){Handle=handle;} }')
            a=text.index('\tprivate Dictionary<Type, Type> mDispatcherTypes =');b=text.index('\n\t};',a)+len('\n\t};')
            text=text[:a]+'\tprivate Dictionary<Type, Type> mDispatcherTypes;'+text[b:]
            text=text.replace('private DisposableBag mDisposableBag;','private IDisposable unsubscribeSubscription,contextSubscription;')
            a=text.index('\t[Inject]');b=text.index('\n\tprotected override void Initialize()',a)
            text=text[:a]+'''    private BufferEventChannel<UnsubscribeEventData> UnsubscribeEventPublisher { get; } = new BufferEventChannel<UnsubscribeEventData>();
    private BufferEventChannel<UnsubscribeEventData> UnsubscribeEventSubscriber => UnsubscribeEventPublisher;
    private BufferEventChannel<IEcEntityBufferEventDispatcher.EcEntityBufferEventContextEventData> EcEntityBufferEventContextEventPublisher { get; } = new BufferEventChannel<IEcEntityBufferEventDispatcher.EcEntityBufferEventContextEventData>();
    private BufferEventChannel<IEcEntityBufferEventDispatcher.EcEntityBufferEventContextEventData> EcEntityBufferEventContextEventSubscriber => EcEntityBufferEventContextEventPublisher;
'''+LIFECYCLE+text[b:]
            text=text.replace('protected override void Initialize()','private void InitializeCore()').replace('protected override void Dispose()','private void DisposeCore()').replace('protected override void OnActivate()','private void OnActivate()')
            text=text.replace('mDispatcherPool = new Dictionary<Type, Queue<IEcEntityBufferEventDispatcher>>();','mDispatcherTypes = new Dictionary<Type,Type>{{Inputs.PlayerInteractionType,typeof(EntityPlayerInteractionBufferEventDispatcher)},{Inputs.WorldStageType,typeof(WorldStageBufferEventDispatcher)}};\n\t\tmDispatcherPool = new Dictionary<Type, Queue<IEcEntityBufferEventDispatcher>>();')
            text=re.sub(r'Disposable.AddTo<IDisposable>\(SubscriberExtensions.Subscribe<UnsubscribeEventData>[^\n]+;', 'unsubscribeSubscription = UnsubscribeEventSubscriber.Subscribe(OnUnsubscribe);',text)
            text=re.sub(r'Disposable.AddTo<IDisposable>\(SubscriberExtensions.Subscribe<IEcEntityBufferEventDispatcher.EcEntityBufferEventContextEventData>[^\n]+;', 'contextSubscription = EcEntityBufferEventContextEventSubscriber.Subscribe(OnEcEntityBufferEventContextCallback);',text)
            text=text.replace('mDisposableBag.Dispose();','unsubscribeSubscription.Dispose();\n\t\tcontextSubscription.Dispose();')
            text=text.replace('MeManager.Instance.MapData','Inputs').replace('MeManager.Instance != null','Inputs != null')
            text=text.replace(' += (IEcContainerAddEntityEventReceiver)this',' += Container_OnEntityAdd').replace(' -= (IEcContainerAddEntityEventReceiver)this',' -= Container_OnEntityAdd')
            text=text.replace(' += (IEcContainerRemoveEntityEventReceiver)this',' += Container_OnEntityRemove').replace(' -= (IEcContainerRemoveEntityEventReceiver)this',' -= Container_OnEntityRemove')
            text=text.replace('((IEcContainerAddEntityEventReceiver)this).Container_OnEntityAdd','Container_OnEntityAdd').replace('((IEcContainerRemoveEntityEventReceiver)this).Container_OnEntityRemove','Container_OnEntityRemove')
            text=text.replace('void IEcContainerAddEntityEventReceiver.Container_OnEntityAdd','public void Container_OnEntityAdd').replace('void IEcContainerRemoveEntityEventReceiver.Container_OnEntityRemove','public void Container_OnEntityRemove')
            text=text.replace('EcContainer','BufferContainerData').replace('EcEntity entity','BufferEntityData entity').replace('EcEntity value','BufferEntityData value').replace('EcComponent component','BufferComponentData component').replace('component.GetType()','component.ComponentType').replace('MySerializable','object')
            # The public consumed subscription keeps the original stable view;
            # only container iteration needs its component-list projection.
            text=text.replace('Subscribe<T>(BufferEntityData entity','Subscribe<T>(ECViewEntityData entity')
            text=text.replace('Remove(signature, ref ecEntityBufferEventContext)','Remove(signature, out ecEntityBufferEventContext)').replace('Remove(subscriptionSignature, ref ecEntityBufferEventDispatcher)','Remove(subscriptionSignature, out ecEntityBufferEventDispatcher)').replace('TryDequeue(ref ecEntityBufferEventDispatcher)','TryDequeue(out ecEntityBufferEventDispatcher)')
        elif name=='IEcEntityBufferEventDispatcher':
            text=text.replace('public record struct EcEntityBufferEventContextEventData(EcComponent EcComponent, IEcEntityBufferEventContext EcEntityBufferEventContext);','public readonly struct EcEntityBufferEventContextEventData { public readonly BufferComponentData EcComponent; public readonly IEcEntityBufferEventContext EcEntityBufferEventContext; public EcEntityBufferEventContextEventData(BufferComponentData component,IEcEntityBufferEventContext context){EcComponent=component;EcEntityBufferEventContext=context;} }')
        elif name in ('WorldStageBufferEventDispatcher','EntityPlayerInteractionBufferEventDispatcher'):
            stage=name=='WorldStageBufferEventDispatcher'
            text=re.sub(r': EcEntityBufferEventDispatcherBase<[^\n]+',': EcEntityBufferEventDispatcherBase<BufferComponentData>',text,count=1)
            a=text.index('\n{')+len('\n{');text=text[:a]+'\n    private IDisposable subscription;'+text[a:]
            if stage:
                text=text.replace('mEcComponent.StateUpdateEvent += (IEcEntityWorldStageComponentStateUpdateEventReceiver)this;','subscription = mEcComponent.SubscribeStage(WorldStageComponent_OnStateUpdate);').replace('mEcComponent.StateUpdateEvent -= (IEcEntityWorldStageComponentStateUpdateEventReceiver)this;','subscription.Dispose(); subscription=null;')
                text=text.replace('void IEcEntityWorldStageComponentStateUpdateEventReceiver.WorldStageComponent_OnStateUpdate(WorldStageComponent stageComp, int preState, MySerializable param)','private void WorldStageComponent_OnStateUpdate(ECWorldStageStateData stageComp, int preState, object param)')
            else:
                text=text.replace('mEcComponent.OnInteractingPlayerRemove += (IEcEntityPlayerInteractionRemoveEventReceiver)this;','subscription = mEcComponent.SubscribePlayerRemove(EntityPlayerInteractionComponent_Remove);').replace('mEcComponent.OnInteractingPlayerRemove -= (IEcEntityPlayerInteractionRemoveEventReceiver)this;','subscription.Dispose(); subscription=null;')
                text=text.replace('void IEcEntityPlayerInteractionRemoveEventReceiver.EntityPlayerInteractionComponent_Remove(EntityPlayerInteractionComponent playerInteractionComponenet, long playerId)','private void EntityPlayerInteractionComponent_Remove(BufferComponentData playerInteractionComponenet, long playerId)')
        text=text.replace('IPublisher<','BufferEventChannel<').replace('where T : EcComponent','where T : BufferComponentData').replace('Load(EcComponent component)','Load(BufferComponentData component)')
        pos=text.index('public ');text='// Migrated complete original application buffer source; see Map14 buffered-event receipt.\nusing System;\nusing Zjcs.Official.EntityViews;\nusing Zjcs.Official.EntityWorldStages;\nusing Zjcs.Official.StoryPlayables;\n'+text[:pos]+'namespace Zjcs.Official.EntityBufferedEvents\n{\n'+text[pos:]+'\n}\n'
        write(official/(name+'.cs'),text.encode())
    for folder,name,refs in [(official,'Zjcs.Official.EntityBufferedEvents',['Zjcs.Official.EntityViews','Zjcs.Official.EntityWorldStages','Zjcs.Official.StoryPlayables']),
        (native,'Zjcs.Offline.World.NativeMap14BufferedEvents',['Zjcs.Official.EntityContainers','Zjcs.Official.EntityLoading','Zjcs.Official.EntityLifecycle','Zjcs.Official.LegacyWorldStages','Zjcs.Official.EntityStageOperations','Zjcs.Official.EntityBufferedEvents','Zjcs.Official.EntityViews','Zjcs.Official.EntityWorldStages','Zjcs.Official.StoryPlayables','Zjcs.Offline.World.NativeMap14ClientEntities','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews'])]:
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=refs)))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
        for p in folder.iterdir():
            if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    editor=ROOT/'unity/Assets/Game/Editor/Map14/BufferedEvents'
    refs=json.loads((editor.parent/'ClientViewBindings/Zjcs.Editor.Map14.ClientViewBindings.asmdef').read_bytes())['references']
    refs=sorted(set(refs+['Zjcs.Official.EntityBufferedEvents','Zjcs.Offline.World.NativeMap14BufferedEvents','Zjcs.Editor.Map14.ClientViewBindings']))
    write(editor/'Zjcs.Editor.Map14.BufferedEvents.asmdef',encode(dict(name='Zjcs.Editor.Map14.BufferedEvents',autoReferenced=False,references=refs,includePlatforms=['Editor'])))
    runtime_meta(editor);outputs.append(Path(str(editor)+'.meta'))
    for p in editor.iterdir():
        if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    from prepare_unity_map14_shared_views import build as build_shared_views
    build_shared_views()
    changed=[ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedEventSubscription.cs',ROOT/'scripts/prepare_unity_map14_shared_views.py',ROOT/'assets/reference/official-map/map14-shared-view-source-manifest.json']
    for name in ('map14-native-visibility-source-manifest.json','map14-visibility-handlers-source-manifest.json'):
        receipt=ROOT/'assets/reference/official-map'/name;value=json.loads(receipt.read_bytes())
        for p in changed:
            rows=[e for e in value['outputs'] if e['path']==source_path(p)]
            if len(rows)!=1:raise ValueError('Dependent event receiver receipt differs: '+source_path(p))
            rows[0].update(record(p))
        write(receipt,encode(value));changed.append(receipt)
    outputs.extend(changed)
    adapter=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedData.cs'
    outputs.append(adapter)
    for name in ('map14-shared-serialization-source-manifest.json','map14-shared-ec-runtime-source-manifest.json','map14-client-entity-state-source-manifest.json'):
        receipt=ROOT/'assets/reference/official-map'/name;value=json.loads(receipt.read_bytes())
        matches=0
        for section in ('outputs','dependencies'):
            for row in value.get(section,[]):
                if row.get('path')==source_path(adapter):row.update(record(adapter));matches+=1
                elif name=='map14-client-entity-state-source-manifest.json' and row.get('path')==source_path(changed[0]):row.update(record(changed[0]))
        if matches!=1:raise ValueError('Shared parser dependency receipt differs: '+name)
        if name=='map14-shared-ec-runtime-source-manifest.json':
            value['serialization_extension'].update(record(ROOT/'assets/reference/official-map/map14-shared-serialization-source-manifest.json'))
        write(receipt,encode(value));outputs.append(receipt)
    metadata=json.loads((CODE.parent/'code-manifest.json').read_bytes());assemblies=[]
    for name in ('Console.dll','Assembly-CSharp.dll','GameFramework.Runtime.dll','Framework.FirstPass.Runtime.dll','Launch.dll'):
        p=CODE/'managed/decoded'/name
        if sha(p.read_bytes())!=metadata['assemblies'][name]['decoded_sha256']:raise ValueError('Original buffered event assembly version differs')
        assemblies.append(record(p))
    inputs.extend(CODE/'common-source/Common'/(name+'.cs') for name in ('EntityPlayerInteractionComponent','IEcEntityPlayerInteractionRemoveEventReceiver','EcEntityPlayerInteractionRemoveEventDelegate','IEcContainerAddEntityEventReceiver','EcContainerAddEntityEventDelegate','EcEntityIdentifier'))
    inputs.extend(CODE/'common-source/Common'/(name+'.cs') for name in ('EcContainer','GridVisibilityAgentHandle','EntityHandler','MapRootEntityLiteInfoPackParser'))
    inputs.extend((CODE/'gameframework-csharp9-source/GameFramework/SceneScopeBackgroundServiceBase.cs',CODE/'framework-firstpass-source/Framework/BackgroundServiceBase.cs'))
    write(ROOT/'assets/reference/official-map/map14-buffered-events-source-manifest.json',encode(dict(manifest='871_156598',mapClassId=14,assemblies=assemblies,inputs=[record(p) for p in inputs],outputs=[record(p) for p in sorted(set(outputs))],tools=[record(Path(__file__))],operation='Copy complete original event buffering module and both pooled dispatchers, dispatcher base and player removal context. Preserve signature hash, last context per signature, inactive buffering, activation replay/clear, immediate active dispatch, handle mutation/exception behavior, entity component order and pool/disposal order. Adapt component/event inputs to unchanged native objects, C# record struct syntax, scene lifecycle inputs and diagnostics. Replace two private one-subscriber MessagePipe transports and R3 subscription storage with explicit synchronous owned channel leases; no shared stage or interaction rules are implemented.',validation_entry='Zjcs.Editor.Map14.OfficialMap14BufferedEventsAcceptance.Run',validation='Unity 2022.3.57f1c2 / D3D11 on the internal display: graphical import/compile and Play Mode passed. Original signature buffering/replay, active dispatch, native stage/player-removal events, dynamic native add/remove and pooled dispatcher reuse/teardown passed. Stable client views, original visibility factory/handlers, display consumption/data, four statues, radar/cost/input/container regressions passed; current runtime frames inspected. Root static project check is recorded in the owning map page.',limitations=['Formal map/display host and Windows Player remain required.'])))
    print('Original buffered event runtime prepared:',len(outputs),'outputs')

if __name__=='__main__':build()
