"""Port the official pooled visibility handlers and their actual action consumer."""
import json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map14_sources import encode,sha,source_path,write
from prepare_unity_story_sound_runtime import CODE,runtime_meta

def record(p):
    raw=p.read_bytes();e=dict(path=source_path(p))
    if p.is_relative_to(ROOT/'unity') and p.suffix in ('.cs','.asmdef'):
        raw=raw.replace(b'\r\n',b'\n');e['hashLineEndings']='LF'
    return dict(e,sha256=sha(raw))
def build():
    outputs=[];inputs=[]
    action=ROOT/'unity/Assets/Game/Runtime/Official/EntityActions'
    handlers=ROOT/'unity/Assets/Game/Runtime/Official/EntityVisibilityHandlers'
    ns='Zjcs.Official.EntityVisibilityHandlers'
    def source(name,common=False):
        p=CODE/('common-source/Common' if common else 'client-source')/(name+'.cs');inputs.append(p)
        return p.read_text(encoding='utf-8-sig')
    def emit(folder,name,text,namespace,imports):
        text=re.sub(r'(?m)^using (?:Common|Framework(?:\.[\w.]+)?|GameFramework|GFrameCore(?:\.[\w.]+)?|Microsoft.Extensions.Logging|ZLogger);\n','',text)
        text=text.replace('namespace Common;','')
        pos=text.index('public ')
        text='// Migrated original application source; see Map14 visibility-handlers receipt.\nusing Object = UnityEngine.Object;\n'+imports+text[:pos]+'namespace '+namespace+'\n{\n'+text[pos:]+'\n}\n'
        p=folder/(name+'.cs');write(p,text.encode());runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    text=source('MyAction')
    a=text.index('\t\t\tILogger logger = Logger;');b=text.index('\n\t\t}',a)
    text=text[:a]+'\t\t\tUnityEngine.Debug.LogWarning("Original MyAction apply entity is absent.");'+text[b:]
    text=text.replace('MyComponent<ActionComponent, ActionComponentInfo>','MyComponent<ActionComponentData, object>')
    text=text.replace('myContainer.GetEntity(Component.ApplyId)','((Zjcs.Official.EntityContainers.MyContainer)myContainer).GetEntity(Component.ApplyId)')
    text=text.replace('[Inject]\n\tprotected ','public ').replace('MySerializable userData','object userData')
    emit(action,'MyAction',text,'Zjcs.Official.EntityActions','using Zjcs.Official.EntityLifecycle;\nusing Zjcs.Official.EntityViews;\nusing Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.StoryPlayableManager;\n')
    common='using Zjcs.Official.EntityViews;\nusing Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.World.Map13EntityVisibilityViews;\nusing Zjcs.Official.World.Map13EntityEvents;\nusing IEventReceiver=Zjcs.Official.World.Map13EntityEvents.IEventReceiver;\n'
    for name,is_common in [('ClientVisibilityBaseHandler',True),('EntityClientVisibilityHandlerFactory',False),('ClientVisibilityWaitActionHandler',False),('ClientVisibilityWaitAwardItemInitedHandler',False),('ClientVisibilityHideHanlder',False),('EcEntityStageResultItemUpdatedEventDelegate',True),('IEcEntityStageResultItemUpdatedEventReceiver',True)]:
        text=source(name,is_common)
        text=text.replace('EcEntity ent','ECViewEntityData ent').replace('EcEntity entity','ECViewEntityData entity').replace('public EcEntity Entity','public ECViewEntityData Entity')
        text=text.replace('StageResultAwardItemComponent','VisibilityAwardItemData')
        text=text.replace('SceneScopeBackgroundServiceBase, IMapScopeBackgroundService, ','')
        text=text.replace('[Inject]\n\tprivate IServiceProvider','public IServiceProvider')
        text=text.replace('[Inject]\n\tprivate IMapRootManagerProxy','public IVisibilityMapRoot')
        text=re.sub(r'ServiceProvider.CreateInstance<(\w+)>\(Array.Empty<object>\(\)\)',r'VisibilityHandlerConstruction.CreateInstance<\1>(ServiceProvider)',text)
        emit(handlers,name,text,ns,common+'using Awaiter=Cysharp.Threading.Tasks.UniTask.Awaiter;\nusing Zjcs.Official.EntityLifecycle;\nusing Zjcs.Official.EntityContainers;\nusing Zjcs.Official.EntityActions;\nusing Zjcs.Official.StorySourceFade;\n')
    for folder,name,refs in [(action,'Zjcs.Official.EntityActions',['Zjcs.Official.EntityLifecycle','Zjcs.Official.EntityContainers','Zjcs.Official.EntityViews','Zjcs.Official.StoryPlayables','Zjcs.Official.StoryPlayableManager']),
       (handlers,ns,['Zjcs.Official.EntityActions','Zjcs.Official.EntityLifecycle','Zjcs.Official.EntityViews','Zjcs.Official.EntityContainers','Zjcs.Official.StoryPlayables','Zjcs.Official.StorySourceFade','Zjcs.Official.World.Map13EntityEvents','Zjcs.Official.World.Map13EntityVisibilityViews','UniTask'])]:
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=refs)))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
        for p in folder.iterdir():
            if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    native=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14VisibilityHandlers'
    refs=['Zjcs.Official.EntityViews','Zjcs.Official.EntityActions','Zjcs.Official.EntityLifecycle','Zjcs.Official.EntityContainers','Zjcs.Official.EntityVisibilityHandlers','Zjcs.Official.StoryPlayables','Zjcs.Official.World.Map13EntityVisibilityViews','Zjcs.Official.World.Map13EntityEvents','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews']
    write(native/'Zjcs.Offline.World.NativeMap14VisibilityHandlers.asmdef',encode(dict(name='Zjcs.Offline.World.NativeMap14VisibilityHandlers',autoReferenced=False,references=refs)))
    runtime_meta(native);outputs.append(Path(str(native)+'.meta'))
    for p in native.iterdir():
        if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    editor=ROOT/'unity/Assets/Game/Editor/Map14/VisibilityHandlers'
    refs=json.loads((editor.parent/'DisplayData/Zjcs.Editor.Map14.DisplayData.asmdef').read_bytes())['references']
    refs=sorted(set(refs+['Zjcs.Official.EntityLoading','Zjcs.Editor.Map14.DisplayData','Zjcs.Official.EntityActions','Zjcs.Official.EntityContainers','Zjcs.Official.EntityVisibilityHandlers','Zjcs.Offline.World.NativeMap14ClientEntities','Zjcs.Offline.World.NativeMap14VisibilityHandlers']))
    write(editor/'Zjcs.Editor.Map14.VisibilityHandlers.asmdef',encode(dict(name='Zjcs.Editor.Map14.VisibilityHandlers',autoReferenced=False,references=refs,includePlatforms=['Editor'])))
    runtime_meta(editor);outputs.append(Path(str(editor)+'.meta'))
    for p in editor.iterdir():
        if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    for name in ('ActionComponent','ActionComponentInfo','StageResultAwardItemComponent','StageResultAwardItemComponentInfo'):
        source(name,True)
    from prepare_unity_map14_shared_views import build as build_shared_views
    build_shared_views()
    changed=[ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedEventSubscription.cs',ROOT/'scripts/prepare_unity_map14_shared_views.py',ROOT/'assets/reference/official-map/map14-shared-view-source-manifest.json']
    visibility_receipt=ROOT/'assets/reference/official-map/map14-native-visibility-source-manifest.json'
    visibility=json.loads(visibility_receipt.read_bytes())
    for p in changed:
        entry,=[e for e in visibility['outputs'] if e['path']==source_path(p)]
        entry.update(record(p))
    write(visibility_receipt,encode(visibility));outputs.extend(changed+[visibility_receipt])
    metadata=json.loads((CODE.parent/'code-manifest.json').read_bytes());assemblies=[]
    for name in ('Console.dll','Assembly-CSharp.dll'):
        p=CODE/'managed/decoded'/name
        if sha(p.read_bytes())!=metadata['assemblies'][name]['decoded_sha256']:raise ValueError('Original visibility handler IL version differs')
        assemblies.append(record(p))
    write(ROOT/'assets/reference/official-map/map14-visibility-handlers-source-manifest.json',encode(dict(manifest='871_156598',mapClassId=14,assemblies=assemblies,inputs=[record(p) for p in inputs],outputs=[record(p) for p in sorted(set(outputs))],tools=[record(Path(__file__))],operation='Copy complete original FIFO visibility handler factory, three handlers, common visible/event lifecycle and award update receiver/delegate. Copy complete original MyAction application lifecycle, effect play/stop, mutual-exclusion reference counting and duration updates. Adapt namespaces, DI/service inputs, original component data projection and diagnostic block; omit the unused map-scoped background service base. Award payload remains an opaque original object. Keep the original one-second action safety wait state machine and its timing/cancellation unchanged.',validation_entry='Zjcs.Editor.Map14.OfficialMap14VisibilityHandlersAcceptance.Run',validation='Unity 2022.3.57f1c2 internal-display D3D11 isolated import/compile and Play Mode passed: original enum order, forbidden and empty flags, FIFO reuse, hide without a bridge and original Release retaining Entity, actual MyAction with original native ApplyId through the real migrated container EntityAddEvent, old/new and matching/nonmatching branches, unchanged real one-second wait and cancellation, native award readiness and original event owner/order/teardown. Display-data, native visibility, four statue roots, cost/radar/input/container/loading and current rendered frames regressions passed. Root static metadata check required separately.',limitations=['MyAction uses no effect prefab in this integration check; original action prefab/effect playback, looping and teardown still require validation.','The hide nonnull fade bridge branch requires formal integration.','Formal four statue/map services, player/UI/simulated pointer/save, complete ProjectTools.Validate and Windows Player remain required.'])))
    print('Official visibility handlers/action consumer prepared:',len(set(outputs)),'outputs')
if __name__=='__main__':build()
