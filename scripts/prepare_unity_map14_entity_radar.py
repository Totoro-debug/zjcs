"""Migrate original entity radar support, scan, tip lifecycle and pooling."""
import json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,sha,write
from prepare_unity_story_sound_runtime import runtime_meta
CODE=ROOT/'.tools/source-packages/current/871_156598/official-code'
RECEIPT=ROOT/'assets/reference/official-map/map14-entity-radar-source-manifest.json'
NAMES=('IMapControllerEntityRadarSupport','MapControllerEntityRadarSupport','ECMapControllerEntityRadarSupportView','MapControllerEntityRadarSupportUtils','MapControllerEntityRadarTipProviderBase','MapControllerEntityRadarTipProvider','MapControllerEntityRadarTipBase','MapControllerEntityRadarTipManager','MapControllerEntityRadarTipStageCamp','MapControllerEntityRadar','MapMeCharacterUIBase','HorizontalAnchor','VerticalAnchor')
def record(path):return dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path.read_bytes()))
def method(text,signature):
    start=text.index(signature);brace=text.index('{',start);depth=1;end=brace+1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[start:end]
def prepare():
    manifest=json.loads((CODE.parent/'code-manifest.json').read_bytes())
    if manifest['manifest']!='871_156598':raise ValueError('Original radar version differs')
    assemblies=[CODE/'managed/decoded'/name for name in ('Assembly-CSharp.dll','Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes())!=manifest['assemblies'][path.name]['decoded_sha256']:raise ValueError('Original radar IL changed')
    output=ROOT/'unity/Assets/Game/Runtime/Official/EntityRadar'
    inputs=[CODE/'client-source'/(name+'.cs') for name in NAMES]
    imports='''// Original client radar implementation; see entity radar source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.EntityWorldInput;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap14Labels;
using Zjcs.Official.EntityContainers;
using MyContainer = Zjcs.Official.EntityContainers.MyContainer;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using IGameObjectPoolItem = Zjcs.Official.StoryPlayables.IGameObjectPoolItem;
'''
    for source in inputs:
        text=source.read_text(encoding='utf-8-sig')
        text=re.sub(r'(?m)^using [^\n]+;\n','',text)
        text=re.sub(r'(?m)^\s*//IL_[^\n]*\n','\n',text).replace('[Inject]\n','')
        text=re.sub(r'\bEcEntity\b','ECViewEntityData',text)
        text=re.sub(r'\bGridTransformComponent\b','ECGridTransformData',text)
        text=re.sub(r'\bWorldStageComponent\b','ECWorldStageStateData',text)
        text=re.sub(r'\bMySerializable\b','object',text)
        text=re.sub(r'\bIMyMapController\b','IRadarCharacterController',text)
        text=text.replace('IMapMyCharacterManagerProxy','IRadarCharacterManager')
        text=text.replace('private IRadarCharacterManager MapMyCharacterManager','public IRadarCharacterManager MapMyCharacterManager')
        text=text.replace('private IMapRootManagerProxy MapRootManagerProxy','public IWorldInputRootLookup MapRootManagerProxy')
        text=text.replace('private IMapInputSupportHandlerProxy MapInput','public IRadarWorldQueries MapInput')
        text=text.replace('return MyEntity.myContainer;','return (MyContainer)MyEntity.myContainer;').replace('return mMyEntity.myContainer;','return (MyContainer)mMyEntity.myContainer;')
        if source.stem in ('MapControllerEntityRadarSupport','ECMapControllerEntityRadarSupportView'):
            marker='\t[SerializeField]'
            text=text.replace(marker,'\tpublic IRadarRemovalEvents RemovalEvents { get; set; }\n\n'+marker,1)
        elif source.stem=='IMapControllerEntityRadarSupport':
            text=text.replace('\n{','\n{\n    IRadarRemovalEvents RemovalEvents { get; }',1)
        elif source.stem=='MapControllerEntityRadarTipProvider':
            text=text.replace('IEcContainerRemoveEntityEventReceiver','IRadarRemoveEntityReceiver')
            text=text.replace('MeManager.Instance.MapData.RemoveEntityEvent += (IRadarRemoveEntityReceiver)this;','mRadarSupport.RemovalEvents.Add(this);')
            text=text.replace('MeManager.Instance.MapData.RemoveEntityEvent -= (IRadarRemoveEntityReceiver)this;','mRadarSupport.RemovalEvents.Remove(this);')
        elif source.stem=='MapControllerEntityRadar':
            text=text.replace('Parsers.GameSettings.MapStageCostShowDistance','MapInput.MapStageCostShowDistance')
            text=text.replace('myMapController.MyEntity','myMapController.MyEntity')
            text=text.replace('mMapContainer = mMapController.MyEntity.myContainer;','mMapContainer = (MyContainer)mMapController.MyEntity.myContainer;')
            text=text.replace('mMapController.MyEntity.Entity.GridTransform','mMapController.MyEntity.Entity.GetComponent<ECGridTransformData>()')
            text=text.replace('mGridTransformComponent.PosUpdatedEvent += (IGridTransformPosUpdatedEventReceiver)this;','mGridTransformComponent.AddPosUpdatedReceiver(this);')
            text=text.replace('mGridTransformComponent.PosUpdatedEvent -= (IGridTransformPosUpdatedEventReceiver)this;','mGridTransformComponent.RemovePosUpdatedReceiver(this);')
            text=text.replace('Object.op_Implicit((Object)(object)mMapContainer)','((Object)(object)mMapContainer != (Object)null)')
            text=text.replace('Object.op_Implicit((Object)(object)entity.myGridTransform)','((Object)(object)entity.myGridTransform != (Object)null)')
            text=text.replace('GamePosData gamePosData','IRadarGamePosData gamePosData')
            text=text.replace('stageComp.IsActive()','((IWorldStageOperationState)stageComp).IsActive()')
            text=text.replace('stageComp.Entity.Id','((Zjcs.Official.LegacyWorldStages.LegacyWorldStageComponentData)stageComp).Entity.Id')
            a=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler message =');b=text.index('\n\t\t\treturn null;',a)
            text=text[:a]+'\t\t\tDebug.LogError("MapControllerEntityRadarTipManager GetTip failed, EntityClassId:" + support.MyEntity.ClassId + ", RadarTipType:" + prefab.TipType + ", PrefabName:" + prefab.name);'+text[b:]
            signature='private ECWorldStageView GetECWorldStageView(MyEntity myEntity)'
            original=method(text,signature)
            replacement='''private ECWorldStageView GetECWorldStageView(MyEntity myEntity)
    {
        if ((Object)(object)myEntity == (Object)null) return null;
        ECViewDriver componentInChildren = ((Component)myEntity).GetComponentInChildren<ECViewDriver>(true);
        if ((Object)(object)componentInChildren == (Object)null)
        { Debug.LogError("物件" + myEntity.ClassId + "没有ECViewDriver"); return null; }
        ECWorldStageView view = componentInChildren.GetView<ECWorldStageView>();
        if (view == null)
        { Debug.LogError("物件" + myEntity.ClassId + "没有ECWorldStageView"); return null; }
        return view;
    }'''
            text=text.replace(original,replacement)
        write(output/source.name,(imports+'namespace Zjcs.Official.EntityRadar\n{\n'+text+'\n}\n').encode())
    utility=CODE/'client-source/MapMyEntityUtils.cs';inputs.append(utility)
    text=method(utility.read_text(encoding='utf-8-sig'),'public static Vector3 GetOffsetByAnchor(MyEntity entity, HorizontalAnchor horAnchor, VerticalAnchor verAnchor)')
    text=re.sub(r'(?m)^\s*//IL_[^\n]*\n','\n',text)
    text=text.replace('GridTransformComponentInfo info = entity.myGridTransform.Info;','IRadarBodyInfo info = entity.Entity.GetComponent<IRadarBodyInfo>();').replace('GridTransformComponentBodyPosInfo','RadarBodyPosInfo')
    text=text.replace('Vector2.op_Implicit((zero3 - info.RealCenter) * 1f)','(Vector3)((zero3 - info.RealCenter) * 1f)')
    write(output/'MapMyEntityUtils.cs',(imports+'namespace Zjcs.Official.EntityRadar\n{\npublic static class MapMyEntityUtils\n{\n'+text+'\n}\n}\n').encode())
    native=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14EntityRadar';editor=ROOT/'unity/Assets/Game/Editor/Map14/EntityRadar'
    refs=['Zjcs.Official.EntityViews','Zjcs.Official.EntityLifecycle','Zjcs.Official.EntityTransformViews','Zjcs.Official.EntityWorldStages','Zjcs.Official.EntityStageOperations','Zjcs.Official.EntityWorldInput','Zjcs.Official.StoryPlayables','Zjcs.Official.StoryPlayableManager','Zjcs.Official.World.NativeTerrain','Zjcs.Official.World.NativeMap14Labels','Zjcs.Official.EntityContainers','Zjcs.Official.EntityLoading','Zjcs.Official.World.Map13EntityEvents']
    refs.append('Zjcs.Official.LegacyWorldStages')
    outputs=[]
    for folder,name,references,platforms in (
        (output,'Zjcs.Official.EntityRadar',refs,[]),
        (native,'Zjcs.Offline.World.NativeMap14EntityRadar',refs+['Zjcs.Official.EntityRadar','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews'],[]),
        (editor,'Zjcs.Editor.Map14.EntityRadar',refs+['Zjcs.Official.EntityRadar','Zjcs.Offline.World.NativeMap14EntityRadar','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews','Zjcs.Offline.World.NativeMap14ClientEntities','Zjcs.Editor.Map14.EntityAwardHook'],['Editor'])):
        folder.mkdir(parents=True,exist_ok=True)
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=references,includePlatforms=platforms)))
        for path in sorted(folder.iterdir()):
            if path.is_file() and path.suffix!='.meta':runtime_meta(path);outputs.extend((path,Path(str(path)+'.meta')))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
    inputs += [CODE/'common-source/Common'/(name+'.cs') for name in ('PlayerMap','GamePosData','GridGroundComponent','GridTransformComponentInfo','IEcContainerRemoveEntityEventReceiver','EcContainerRemoveEntityEventDelegate')]
    dependencies=[ROOT/'unity/Assets/Game/Runtime'/name for name in ('Official/StoryPlayableManager/GameObjectPool.cs','Official/World/NativeMap14Labels/UIBase.cs','Offline/World/NativeMap14SharedEntities/NativeMap14SharedEventSubscription.cs')]
    metadata=CODE/'global-metadata.dat';native_image=ROOT/'.tools/source-packages/commerce-native/libil2cpp-restored.bin'
    if sha(metadata.read_bytes())!='58884b94b56ef35665f44d3f186fce8b8c854cf3bab0a74e4733a18b6a9175b4' or sha(native_image.read_bytes())!='8e49906e085ba3819c4b47fd3bd12dcdd594ff0fcc75d418a87ac35a4566305b':
        raise ValueError('APK native instantiation implementation changed')
    inputs += [metadata,native_image]
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=14,assemblies=[record(p) for p in assemblies],inputs=[record(p) for p in inputs],dependencies=[record(p) for p in dependencies],tools=[record(Path(__file__).resolve())],outputs=[record(p) for p in outputs],
        operation='Copy complete original radar/support/view/provider/tip-base/pool-manager/controller-lifecycle/camp-tip and anchor enums, plus complete consumed GetOffsetByAnchor method. Preserve scan ranges/order, enter/leave and override callbacks, stage-event subscriptions, native removal notifications, anchor formula, original tip pooling and animation lifecycle. Player, input initialization and manager services are explicit host bindings.',
        apkNativeFacts=dict(type='Framework.DependencyInjection.ServiceProviderExtensions',instantiateAddress='0x0b74e034',instantiateMetadataIndex=337733,instantiateParameters=4,setNameAddress='0x0b74e1ac',setNameMetadataIndex=337736,setName='Copy original prefab.name to new instance before injection/reactivation; do not retain Unity (Clone) suffix. Fixture starts with an inactive prefab and supplies this verified naming boundary.'),
        validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14EntityRadarAcceptance.Run',graphicalPlayMode=True),
        limitations=['Combined/cost/fast-kill tip components and original radar prefab payloads remain required. Formal player/map/HUD/pointer/UI/save services and Windows Player are not completed by the isolated component checks.'])))
    print('Original radar core recorded:',len(outputs),'outputs')
if __name__=='__main__':prepare()
