"""Migrate the original cost radar tip and its original client cost queries."""
import json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,sha,write
from prepare_unity_story_sound_runtime import runtime_meta
CODE=ROOT/'.tools/source-packages/current/871_156598/official-code'
RECEIPT=ROOT/'assets/reference/official-map/map14-radar-cost-tip-source-manifest.json'
def record(path):return dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path.read_bytes()))
def prepare():
    manifest=json.loads((CODE.parent/'code-manifest.json').read_bytes())
    assemblies=[CODE/'managed/decoded'/name for name in ('Assembly-CSharp.dll','Console.dll')]
    for path in assemblies:
        if manifest['manifest']!='871_156598' or sha(path.read_bytes())!=manifest['assemblies'][path.name]['decoded_sha256']:raise ValueError('Original cost tip IL changed')
    source=CODE/'client-source/MapControllerEntityRadarTipStageCost.cs'
    text=source.read_text(encoding='utf-8-sig')
    text=re.sub(r'(?m)^using [^\n]+;\n','',text)
    text=re.sub(r'(?m)^\s*//IL_[^\n]*\n','\n',text).replace('[Inject]\n','')
    text=text.replace('private IResourceLoader ResourceLoader','public IWorldInputResourceLoader ResourceLoader')
    text=text.replace('private DisposableBag mDisposableBag;','private RadarCostResourceBag mDisposableBag;\n\n    public IRadarStageCostQueries CostQueries { get; set; }\n    public IRadarItemIconQueries ItemIcons { get; set; }')
    text=text.replace('ItemInfo itemInfo = Parsers.Instance.ItemInfoParser.Get(current.Key);','string icon = ItemIcons.GetItemIcon(current.Key);')
    text=text.replace('Disposable.AddTo<AssetOperationHandle<Sprite>>(ResourceLoader.LoadSprite(itemInfo.Icon), ref mDisposableBag).Result','mDisposableBag.Add(ResourceLoader.LoadSprite(icon)).Result')
    text=text.replace('EcEntity entity = myEnt.Entity;','ECViewEntityData entity = myEnt.Entity;').replace('StageUtils.GetStageCost(entity)','CostQueries.GetStageCost(entity)')
    output=ROOT/'unity/Assets/Game/Runtime/Official/EntityRadarCost'
    write(output/source.name,('''// Original client cost radar tip; see map14 radar-cost source receipt.
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using TMPro;
using Zjcs.Official.EntityRadar;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldInput;
using MultiLanguageText = Zjcs.Official.World.NativeLabelUI.MultiLanguageText;
namespace Zjcs.Official.EntityRadarCost
{
'''+text+'\n}\n').encode())
    native=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14RadarCost';editor=ROOT/'unity/Assets/Game/Editor/Map14/RadarCost'
    refs=['Zjcs.Official.EntityRadar','Zjcs.Official.EntityLifecycle','Zjcs.Official.EntityViews','Zjcs.Official.EntityWorldInput','Zjcs.Official.LegacyWorldStages','Zjcs.Official.EntityWorldStages','Zjcs.Official.EntityStageOperations','Zjcs.Official.World.NativeLabelUI','Unity.TextMeshPro']
    outputs=[]
    for folder,name,references,platforms in (
        (output,'Zjcs.Official.EntityRadarCost',refs,[]),
        (native,'Zjcs.Offline.World.NativeMap14RadarCost',refs+['Zjcs.Official.EntityRadarCost','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews'],[]),
        (editor,'Zjcs.Editor.Map14.RadarCost',refs+['Zjcs.Official.EntityRadarCost','Zjcs.Offline.World.NativeMap14RadarCost','Zjcs.Offline.World.NativeMap14EntityRadar','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews','Zjcs.Editor.Map14.RadarCombined'],['Editor'])):
        folder.mkdir(parents=True,exist_ok=True)
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=references,includePlatforms=platforms)))
        for path in sorted(folder.iterdir()):
            if path.is_file() and path.suffix!='.meta':runtime_meta(path);outputs.extend((path,Path(str(path)+'.meta')))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
    inputs=[source,CODE/'client-source/StageUtils.cs']+[CODE/'common-source/Common'/(name+'.cs') for name in ('WorldStageComponent','StageBehaviourComponent','FormulaHandler','EntitySourceComponent')]
    dependencies=[ROOT/'unity/Assets/Game/Runtime/Official'/name for name in ('EntityRadar/MapControllerEntityRadarTipBase.cs','EntityWorldInput/WorldEntityInputBindings.cs','World/NativeLabelUI/MultiLanguageText.cs')]
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=14,assemblies=[record(p) for p in assemblies],inputs=[record(p) for p in inputs],dependencies=[record(p) for p in dependencies],tools=[record(Path(__file__).resolve())],outputs=[record(p) for p in outputs],
        operation='Copy full original cost radar tip; inject original item icon/local sprite/text services. Adapt both consumed client StageUtils.GetStageCost overloads to reflection over unchanged Console stage/source/behaviour/formula objects, preserving null/type/empty branches, original mutable cost dictionary and status query order. No cost deduction or server rule is added.',
        validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14RadarCostAcceptance.Run',graphicalPlayMode=True),
        limitations=['Original StageCostTip prefab payload and formal map/HUD/player/input/save/Windows Player still require binding and verification.'])))
    print('Original cost radar tip recorded:',len(outputs),'outputs')
if __name__=='__main__':prepare()
