"""Copy the original root visibility client and bind its actual native events."""
import json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map14_sources import encode,sha,source_path,write
from prepare_unity_story_sound_runtime import CODE,runtime_meta
from prepare_unity_map14_client_view_bindings import record

OFFICIAL=ROOT/'unity/Assets/Game/Runtime/Official/MapRootVisibility'
NATIVE=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14RootVisibility'
EDITOR=ROOT/'unity/Assets/Game/Editor/Map14/RootVisibility'
RECEIPT=ROOT/'assets/reference/official-map/map14-root-visibility-source-manifest.json'

def build():
    inputs=[CODE/'client-source'/(n+'.cs') for n in ('MyGridVisibilityAgent','MyActionTransfer','IMapRootManagerProxy','IMapMyCharacterManagerProxy')]
    inputs.append(CODE/'common-source/MeManager.cs')
    inputs.extend(CODE/'common-source/Common'/(n+'.cs') for n in ('GridVisibilityAgentComponent','GridVisibilityAgentHandle','ActionAgentComponent','ActionType','CharacterComponent','MapGroundGridBoolData','GridVisibilityCompressUtils'))
    text=inputs[0].read_text(encoding='utf-8-sig');text=re.sub(r'(?m)^\s*//IL_[^\n]*\n','',text)
    text=text.replace('using Common;','using Zjcs.Official.World.NativeTerrain.Common;\nusing Zjcs.Official.MapRootFog;\nusing Zjcs.Official.EntityLifecycle;\nusing Object = UnityEngine.Object;')
    text=text.replace('using Framework.DependencyInjection;','')
    text=text.replace('public class MyGridVisibilityAgent','namespace Zjcs.Official.MapRootVisibility\n{\npublic class MyGridVisibilityAgent')+'\n}\n'
    text=text.replace('GridVisibilityAgentComponentInfo','object').replace('GridVisibilityAgentComponent','FogVisibilityAgentData').replace('GridGroundComponentInfo','FogGroundInfo').replace('GridGroundComponent','FogGroundData').replace('MySerializable userData','object userData')
    start=text.index('\t[Inject]');end=text.index('\n\tpublic event ',start)
    text=text[:start]+'\tpublic IFogVisibilityPlayer Player {get;set;}\n\tprivate Zjcs.Official.EntityContainers.MyContainer ClientContainer => (Zjcs.Official.EntityContainers.MyContainer)myContainer;\n'+text[end:]
    text=text.replace('myContainer.Inited','ClientContainer.Inited').replace('myContainer.OnInited','ClientContainer.OnInited')
    text=text.replace('CharacterComponent character = MeManager.Instance.MapData.MapCharacter.Character;','FogCharacterData character = Player.Character;')
    text=text.replace('EcEntityIdentifier id = character.GetComponent<ActionAgentComponent>().FindAction(ActionType.WorldTransfer);','var id = character.FindWorldTransferAction();')
    text=text.replace('MapRootManagerProxy.MyContainer.GetEntity(id)','Player.GetEntity(id)')
    text=text.replace('MyActionTransfer component = ((Component)entity).GetComponent<MyActionTransfer>();','IFogTransferClient component = Player.GetTransferClient(entity);')
    text=text.replace('(Object)(object)component != (Object)null && component.Component != null','component != null && component.Component != null')
    text=text.replace('MapMyCharacterManager.MyMeUiCharacter?.GridPos ?? character.GridTransform.Pos','Player.MyMeUiGridPos ?? character.GridPos')
    write(OFFICIAL/'MyGridVisibilityAgent.cs',('// Complete original client class with consumed player/native/container type boundaries.\n'+text).encode())
    write(OFFICIAL/'RootVisibilityInputs.cs',b'''using System;
using System.Collections.Generic;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.MapRootFog;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.MapRootVisibility
{
 public interface IFogVisibilityPlayer
 {
  FogCharacterData Character {get;} MyEntity GetEntity(EcEntityIdentifier id);
  IFogTransferClient GetTransferClient(MyEntity entity);Pos2d? MyMeUiGridPos {get;}
 }
 public abstract class FogCharacterData {public abstract EcEntityIdentifier FindWorldTransferAction();public abstract Pos2d GridPos {get;}}
 public interface IFogTransferClient {FogTransferData Component {get;}}
 public abstract class FogTransferData {public abstract Pos2d Pos {get;}}
 public abstract class FogMultiCellHandle
 {
  public abstract event Action<HashSet<Pos2d>,bool> OnMultiCellsUnlock;
 }
 public abstract class FogVisibilityAgentData:FogVisibilityData,IEntityComponentInfoSource,IDisposable
 {
  public abstract override object Original {get;}public abstract object ComponentInfo {get;}
  public abstract T GetComponent<T>() where T:class;
  public abstract FogMultiCellHandle Handle {get;}
  public abstract event Action<Pos2d,bool,bool,bool> OnPosVisibilityUpdate;
  public abstract event Action<Pos2d,bool> OnOneCellUnlock;
  public abstract void Dispose();
 }
}
''')
    # Native adapters are maintained alongside this generator; no shared rule is
    # copied into these adapters. Keep their full source hashes in this receipt.
    outputs=[]
    refs=['Zjcs.Official.EntityLifecycle','Zjcs.Official.EntityContainers','Zjcs.Official.StoryPlayables','Zjcs.Official.MapRootFog','Zjcs.Official.World.NativeTerrain']
    for folder,name,references,platforms in (
        (OFFICIAL,'Zjcs.Official.MapRootVisibility',refs,[]),
        (NATIVE,'Zjcs.Offline.World.NativeMap14RootVisibility',refs+['Zjcs.Official.MapRootVisibility','Zjcs.Official.EntityViews','Zjcs.Offline.World.NativeMap14RootFog','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews'],[]),
        (EDITOR,'Zjcs.Editor.Map14.RootVisibility',refs+['Zjcs.Official.MapRootVisibility','Zjcs.Official.EntityViews','Zjcs.Official.EntityLoading','Zjcs.Offline.World.NativeMap14RootFog','Zjcs.Offline.World.NativeMap14RootVisibility','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews','Zjcs.Offline.World.NativeMap14ClientEntities','Zjcs.Editor.Map14.RootFog'],['Editor'])):
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=references,includePlatforms=platforms)))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
        for p in folder.iterdir():
            if p.suffix!='.meta':runtime_meta(p);outputs.extend((p,Path(str(p)+'.meta')))
    metadata=json.loads((CODE.parent/'code-manifest.json').read_bytes());assemblies=[]
    for name in ('Assembly-CSharp.dll','Console.dll'):
        p=CODE/'managed/decoded'/name
        if sha(p.read_bytes())!=metadata['assemblies'][name]['decoded_sha256']:raise ValueError('Original visibility assembly version differs')
        assemblies.append(record(p))
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=14,assemblies=assemblies,inputs=[record(p) for p in inputs],tools=[record(Path(__file__))],outputs=[record(p) for p in sorted(set(outputs))],
        operation='Copy complete original MyGridVisibilityAgent including original lifecycle, deferred container initialization, single/multiple unlock, queued reveal, target notifications and source-position branch order. Replace external player-manager/transfer-view services with required consumed contracts, preserving override/transfer/UI-character/native-character fallback order. Adapt native component types and original container base view only. Native event leases attach each callback directly to original Console .NET events, preserving duplicate delegate add/remove semantics and dispatch order; no synthetic event broker or shared rules.',
        validation_entry='Zjcs.Editor.Map14.OfficialMap14RootVisibilityAcceptance.Run',validation='Unity 2022.3.57f1c2 internal-display D3D11 isolated Play Mode passed: original bind/start/end ordering around deferred container Init; native single/multiple unlock events, duplicate subscription removal semantics, queued reveal, player/transfer/UI/native position branches, target notifications, and teardown. Acceptance calls the original Console visibility component and injects fixture visibility bytes only to exercise its branches; it does not define a new-game visibility policy. Formal map pointer flow, player/transfer host, full root prefab/map and Windows Player remain unverified.',limitations=['Formal original player/transfer view host and full root prefab/map/Windows Player remain unfinished.'])))
    print('Original root visibility prepared:',len(outputs),'outputs')

if __name__=='__main__':build()
