"""Migrate original world-stage input, base checks and behaviour lookup."""
import json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_sound_runtime import runtime_meta
CODE=ROOT/'.tools/source-packages/current/871_156598/official-code'
RECEIPT=ROOT/'assets/reference/official-map/map14-world-input-source-manifest.json'
CLIENT=('AWorldEntityInput','WorldStageInput','WorldEntityInputPriority','MyStageBehaviourUtils','IMyStageBehaviour','ECStageBehaviourBaseView','MyEntityPlayerInteraction')
def record(path):return dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path.read_bytes()))
def prepare():
    manifest=json.loads((CODE.parent/'code-manifest.json').read_bytes())
    if manifest['manifest']!='871_156598':raise ValueError('Original world input source version differs')
    assemblies=[CODE/'managed/decoded'/name for name in ('Assembly-CSharp.dll','Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes())!=manifest['assemblies'][path.name]['decoded_sha256']:raise ValueError('Original world input assembly changed')
    sources=[CODE/'client-source'/(name+'.cs') for name in CLIENT]+[CODE/'common-source/Common'/(name+'.cs') for name in ('GridRect','AwardDialogType')]
    output=ROOT/'unity/Assets/Game/Runtime/Official/EntityWorldInput'
    for source in sources:
        text=source.read_text(encoding='utf-8-sig')
        text=re.sub(r'(?m)^using [^\n]+;\n','',text).replace('namespace Common;','')
        text=re.sub(r'(?m)^\s*//IL_[^\n]*\n','\n',text)
        text=re.sub(r'(?m)^\s*\[(?:MessagePackObject|Key)\([^\n]+\)\]\n','',text)
        text=text.replace('[Inject]\n','').replace('List<GameMonsterInfo>','object')
        text=re.sub(r'\bEcEntity\b','ECViewEntityData',text)
        text=text.replace('IMapRootManagerProxy','IWorldInputRootLookup')
        if source.stem=='AWorldEntityInput':
            text=text.replace('protected IMapInputSupportHandlerProxy MapInput','public IWorldInputSupport MapInput')
            text=text.replace('private IWorldInputRootLookup MapRootManagerProxy','public IWorldInputRootLookup BaseMapRootManagerProxy')
            text=text.replace('Formula.Instance.GetPosListGridRect','Geometry.GetPosListGridRect')
            text=text.replace('MeManager.MainTeam','TeamContext.MainTeam').replace('MeManager.Instance.IsLeader','TeamContext.IsLeader')
            text=text.replace('MeManager.Instance.MapData.TryGetStatusByContainerId','TeamContext.TryGetStatusByContainerId')
            text=text.replace('MapInfoManager.Instance.GetMapPartInfo(MapRootManagerProxy.CurrentMapClassId).TeamCheck(status)','TeamContext.TeamCheck(BaseMapRootManagerProxy.CurrentMapClassId, status)')
            text=text.replace('public abstract bool ClickOverriden', 'public IWorldInputGeometry Geometry { get; set; }\n    public IWorldInputTeamContext TeamContext { get; set; }\n\tpublic abstract bool ClickOverriden')
        elif source.stem=='WorldStageInput':
            text=text.replace('DisposableBag','WorldInputResourceBag').replace('private IResourceLoader ResourceLoader','public IWorldInputResourceLoader ResourceLoader')
            text=text.replace('protected IWorldInputRootLookup MapRootManagerProxy','public IWorldInputRootLookup MapRootManagerProxy')
            text=text.replace('protected IMapMyCharacterManagerProxy MapMyCharacterManager','public IWorldInputPlayerContext MapMyCharacterManager')
            text=text.replace('public override bool ClickOverriden','public IWorldInputEntityState EntityState { get; set; }\n    public IWorldInputPresentation Presentation { get; set; }\n\tpublic override bool ClickOverriden')
            text=text.replace('entity.Removing || entity.Ended','EntityState.IsRemoving(entity) || EntityState.HasEnded(entity)')
            text=text.replace('TryGetComponent<MyEntitySource>(ref myEntitySource)','TryGetComponent<MyEntitySource>(out myEntitySource)')
            text=text.replace('MeManager.MainPlayer.Id','MapMyCharacterManager.MainPlayerId')
            text=text.replace('Parsers.Instance.MonsterInfoParser.Get(monsterId)','Presentation.GetMonsterInfo(monsterId)').replace('MonsterInfo monsterInfo','WorldInputMonsterInfo monsterInfo')
            text=text.replace('LanguageUtils.GetMonsterIcon','Presentation.GetMonsterIcon')
            text=text.replace('Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(spriteName), ref mWorldInputResourceBag)','mWorldInputResourceBag.Add(ResourceLoader.LoadSprite(spriteName))')
            text=text.replace('MapUtils.GetMapEntityNameKey','Presentation.GetMapEntityNameKey')
            text=text.replace('mMyEntity.myContainer.ClassId','((Zjcs.Official.EntityContainers.MyContainer)mMyEntity.myContainer).ClassId')
            text=text.replace('IMapCharacter mapCharacterFromController = MyWorldMapCharacter.GetMapCharacterFromController(MapMyCharacterManager.MyMeUiCharacter);','IWorldInputMapCharacter mapCharacterFromController = MapMyCharacterManager.GetMapCharacterFromController();')
            text=text.replace('mWorldStageComponent.IsActive()','((IWorldStageOperationState)mWorldStageComponent).IsActive()')
            text=text.replace('input is WorldPlayerInput worldPlayerInput','input is IWorldPlayerInput worldPlayerInput')
        elif source.stem=='ECStageBehaviourBaseView':
            text=text.replace('protected virtual void Init()', 'public IWorldStageBehaviourQueries StageQueries { get; set; }\n\tprotected virtual void Init()')
            text=text.replace('Parsers.Instance.StageInfoParser.Get(value)','StageQueries.GetStageInfo(value)').replace('StageInfo stageInfo','WorldInputStageInfo stageInfo')
        elif source.stem=='MyEntityPlayerInteraction':
            text=text.replace('MyComponent<EntityPlayerInteractionComponent>','MyComponent<EntityPlayerInteractionComponentData>')
        imports='''// Original client/shared input source; see world input source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.LegacyWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using WorldStageComponent = Zjcs.Official.LegacyWorldStages.LegacyWorldStageComponentData;
using EntityOwnershipType = Zjcs.Official.EntityTipDotViews.EntityOwnershipType;
using IMyVisibility = Zjcs.Official.EntityLifecycle.IMyVisibility;
'''
        write(output/source.name,(imports+'namespace Zjcs.Official.EntityWorldInput\n{\n'+text+'\n}\n').encode())
    native=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14WorldInput';native.mkdir(parents=True,exist_ok=True)
    editor=ROOT/'unity/Assets/Game/Editor/Map14/WorldInput';editor.mkdir(parents=True,exist_ok=True)
    refs=['Zjcs.Official.EntityViews','Zjcs.Official.EntityLifecycle','Zjcs.Official.EntityContainers','Zjcs.Official.EntityClientComponents','Zjcs.Official.EntityWorldStages','Zjcs.Official.EntityStageOperations','Zjcs.Official.EntityTipDotViews','Zjcs.Official.LegacyWorldStages','Zjcs.Official.StoryPlayables','Zjcs.Official.World.NativeTerrain','Zjcs.Official.World.Map13EntityVisibilityViews']
    refs.append('Zjcs.Official.World.Map13EntityEvents')
    outputs=[]
    for folder,name,references,platforms in (
        (output,'Zjcs.Official.EntityWorldInput',refs,[]),
        (native,'Zjcs.Offline.World.NativeMap14WorldInput',refs+['Zjcs.Official.EntityWorldInput','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews'],[]),
        (editor,'Zjcs.Editor.Map14.WorldInput',refs+['Zjcs.Official.EntityLoading','Zjcs.Official.EntityTransformViews','Zjcs.Official.EntityWorldInput','Zjcs.Offline.World.NativeMap14WorldInput','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews','Zjcs.Offline.World.NativeMap14ClientEntities','Zjcs.Offline.World.NativeMap14ClientComponents','Zjcs.Editor.Map14.ClientComponents'],['Editor'])):
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=references,includePlatforms=platforms)))
        for path in sorted(folder.iterdir()):
            if path.is_file() and path.suffix!='.meta':runtime_meta(path);outputs.extend((path,Path(str(path)+'.meta')))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
    inputs=sources+[CODE/'common-source/Common'/(name+'.cs') for name in ('EntityPlayerInteractionComponent','FormulaHandler','WorldStageComponent')]
    dependencies=[ROOT/'unity/Assets/Game/Runtime'/name for name in ('Official/ProfessionUI/ProfessionImageResourceBridge.cs','Offline/World/NativeMap14SharedViews/NativeMap14SharedViewEntity.cs','Offline/World/NativeMap14ClientEntities/NativeMap14ClientEntityState.cs')]
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=14,assemblies=[record(p) for p in assemblies],inputs=[record(p) for p in inputs],tools=[record(Path(__file__).resolve())],dependencies=[record(p) for p in dependencies],outputs=[record(p) for p in outputs],
        operation='Copy the complete original world-stage input/base, priority, grid rectangle, behaviour lookup/interface/base and player-interaction client component. Preserve exact team/source/interacting/visibility/activity short-circuit order, redirect selection, click arguments and resource lifetime. Bind original shared state and geometry explicitly; opaque original monster-list/stage-behaviour values retain native references. Player-input conflict consumes its original IsMyself interface. Resource bag reuses the already recovered APK R3 disposal semantics.',
        validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14WorldInputAcceptance.Run',graphicalPlayMode=True),
        limitations=['Formal map pointer dispatch, complete player/team/presentation/resource services, native root prefab binding, UI/save and Windows Player remain required. Logical input fixture checks do not claim formal mouse interaction.'])))
    print('Migrated original world entity input sources:',len(sources))
if __name__=='__main__':prepare()
