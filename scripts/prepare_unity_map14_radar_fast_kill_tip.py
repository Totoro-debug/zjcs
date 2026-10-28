"""Migrate original radar fast-game hint, with unchanged shared rule calls."""
import json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,sha,write
from prepare_unity_story_sound_runtime import runtime_meta
CODE=ROOT/'.tools/source-packages/current/871_156598/official-code'
RECEIPT=ROOT/'assets/reference/official-map/map14-radar-fast-kill-tip-source-manifest.json'
def record(path):return dict(path=path.relative_to(ROOT).as_posix(),sha256=sha(path.read_bytes()))
def prepare():
    manifest=json.loads((CODE.parent/'code-manifest.json').read_bytes())
    assemblies=[CODE/'managed/decoded'/name for name in ('Assembly-CSharp.dll','Console.dll')]
    for path in assemblies:
        if manifest['manifest']!='871_156598' or sha(path.read_bytes())!=manifest['assemblies'][path.name]['decoded_sha256']:raise ValueError('Original fast-game tip IL changed')
    source=CODE/'client-source/MapControllerEntityRadarTipFastKill.cs'
    text=source.read_text(encoding='utf-8-sig')
    text=re.sub(r'(?m)^using [^\n]+;\n','',text)
    text=text.replace('IMapRootManagerProxy','IRadarFastKillRoot').replace('IMyMapPositionHandlerProxy','IRadarFastKillPosition')
    text=text.replace('ServiceProviderServiceExtensions.GetService<IRadarFastKillRoot>(((Component)(object)entity).GetSceneContainer())','Services.ResolveRoot(entity)')
    text=text.replace('ServiceProviderServiceExtensions.GetService<IRadarFastKillPosition>(((Component)(object)entity).GetSceneContainer())','Services.ResolvePosition(entity)')
    text=text.replace('EntityUtils.TryGetCampOwnerEntity','Services.Queries.TryGetCampOwnerEntity').replace('MeManager.Instance.MapData.GetEntIdByLoEntId','Services.GetEntIdByLoEntId')
    text=text.replace('mMapRootManagerProxy.MyContainer.GetEntity','mMapRootManagerProxy.GetEntity')
    text=text.replace('Player mainPlayer = MeManager.MainPlayer','RadarFastKillPlayer mainPlayer = Services.MainPlayer')
    text=text.replace('EcEntity entity2','ECViewEntityData entity2').replace('WorldStageComponent','LegacyWorldStageComponentData').replace('IStageBehStageFast','IRadarStageFast')
    text=text.replace('MapUtils.GetGameFastType(','Services.Queries.IsFastGame(').replace(').IsFastGame();',');')
    text=text.replace('\n{','\n{\n    public IRadarFastKillServices Services { get; set; }',1)
    output=ROOT/'unity/Assets/Game/Runtime/Official/EntityRadarFastKill'
    write(output/source.name,('''// Complete original radar hint; see map14 fast-kill source receipt.
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityRadar;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.LegacyWorldStages;
namespace Zjcs.Official.EntityRadarFastKill
{
'''+text+'\n}\n').encode())
    native=ROOT/'unity/Assets/Game/Runtime/Offline/World/NativeMap14RadarFastKill';editor=ROOT/'unity/Assets/Game/Editor/Map14/RadarFastKill'
    refs=['Zjcs.Official.EntityRadar','Zjcs.Official.EntityLifecycle','Zjcs.Official.EntityViews','Zjcs.Official.EntityTransformViews','Zjcs.Official.LegacyWorldStages','Zjcs.Official.EntityWorldStages','Zjcs.Official.StoryPlayables','Zjcs.Official.World','Zjcs.Official.World.NativeTerrain']
    outputs=[]
    for folder,name,references,platforms in (
        (output,'Zjcs.Official.EntityRadarFastKill',refs,[]),
        (native,'Zjcs.Offline.World.NativeMap14RadarFastKill',refs+['Zjcs.Official.EntityRadarFastKill','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews'],[]),
        (editor,'Zjcs.Editor.Map14.RadarFastKill',refs+['Zjcs.Official.EntityRadarFastKill','Zjcs.Offline.World.NativeMap14RadarFastKill','Zjcs.Offline.World.NativeMap14SharedEntities','Zjcs.Offline.World.NativeMap14SharedViews','Zjcs.Editor.Map14.RadarCost'],['Editor'])):
        folder.mkdir(parents=True,exist_ok=True)
        write(folder/(name+'.asmdef'),encode(dict(name=name,autoReferenced=False,references=references,includePlatforms=platforms)))
        for path in sorted(folder.iterdir()):
            if path.is_file() and path.suffix!='.meta':runtime_meta(path);outputs.extend((path,Path(str(path)+'.meta')))
        runtime_meta(folder);outputs.append(Path(str(folder)+'.meta'))
    inputs=[source,CODE/'common-source/MapUtils.cs']+[CODE/'common-source/Common'/(name+'.cs') for name in ('EntityHandler','EntityUtils','EnumExt','IStageBehStageFast','MapStageFastForwardType','GameFastType')]
    dependencies=[ROOT/'unity/Assets/Game/Runtime/Official'/name for name in ('EntityRadar/MapControllerEntityRadarTipBase.cs','LegacyWorldStages/LegacyWorldStageBindings.cs')]
    write(RECEIPT,encode(dict(manifest='871_156598',mapClassId=14,assemblies=[record(p) for p in assemblies],inputs=[record(p) for p in inputs],dependencies=[record(p) for p in dependencies],tools=[record(Path(__file__).resolve())],outputs=[record(p) for p in outputs],
        operation='Copy complete original fast-game radar hint, retaining lazy service resolution, camp-owner existence guard, region/player reads before redirection, redirected stage lookup and rule argument order. Explicit services supply existing host state. Unchanged Console EntityHandler, MapUtils.GetGameFastType and EnumExt.IsFastGame own camp lookup and fast-game rules.',
        validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14RadarFastKillAcceptance.Run',graphicalPlayMode=True),
        limitations=['Original fast-kill tip prefab and formal map/player/region/parsers/battle/input/UI/save/Windows Player require integration. Hint visibility does not execute or settle combat.'])))
    print('Original fast-game radar tip recorded:',len(outputs),'outputs')
if __name__=='__main__':prepare()
