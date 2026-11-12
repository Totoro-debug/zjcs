"""Migrate selected original task condition navigation without inventing map routes."""
import argparse,hashlib,json,re,shutil
from pathlib import Path
from official_apk import ROOT
from prepare_home_task_map_data import CODE,method,sha
from prepare_home_mainline_task_data import read_texts
from official_apk import Apk

DEST=ROOT/'unity/Assets/Game/Runtime/Official/HomeTaskUI/HomeTaskJumpConditions.cs'

def build():
 inputs={}
 def read(relative):
  path=CODE/relative;inputs[path.relative_to(ROOT).as_posix()]=sha(path);return path.read_text('utf-8-sig')
 names=['ClientMapGameTotalCountCondition','ClientWorldMapProgressCondition','ClientMapLogicEntityEndByTagCondition','ClientMapRegionGoddessCondition','ClientTotalOpenChestCondition','ClientDemonBattleLayerPassCondition']
 pieces=[]
 for name in names:
  raw=read('client-source/'+name+'.cs')
  bodies=[method(raw,'public bool CanJump('),method(raw,'public void Jump(')]
  if name=='ClientWorldMapProgressCondition':bodies.append(method(raw,'public bool CanTrace('))
  if name=='ClientMapLogicEntityEndByTagCondition':
   bodies.extend([method(raw,'private LanguageUtils.TextKeyArgWrap GetMapRegionDesc('),method(raw,'private bool IsInTargetMap('),method(raw,'public bool CanTrace(')])
  text='\n'.join(bodies)
  text=re.sub(r'public bool CanJump\(PlayerTask taskData\)','public override bool CanJump()',text)
  text=re.sub(r'public void Jump\(PlayerTask taskData, IMapInputSupportHandlerProxy mapInput, INavigateManager navigateManager, Action<NavigateResult> jumpEndCallback\)','public override void Jump(long taskId, Action<object> jumpEndCallback)',text)
  text=text.replace('public bool CanTrace()','public override bool CanTrace()')
  text=text.replace('MeManager.MainPlayer.WorldMapRegionProgressDict.Count','Host.WorldMapRegionProgressCount')
  text=text.replace('MeManager.MainPlayer?.IsOnMap() ?? false','Host.IsOnMap()')
  text=text.replace('MeManager.MainPlayer.IsOnMap','Host.IsOnMap').replace('MeManager.MainPlayer.IsMapAccessible','Host.IsMapAccessible')
  text=text.replace('MeManager.Instance.MapData.MapContainer == null','!Host.HasMapContainer').replace('MeManager.Instance.MapData.MapContainer.ClassId','Host.CurrentMapClassId')
  text=text.replace('MapUtils.GetGroupCurrentMapClassIdByMapClassIdAutoWithTagId(MeManager.MainPlayer, classId)','Host.GetGroupCurrentMap(classId)').replace('MapUtils.GetGroupCurrentMapClassIdByMapClassIdAutoWithTagId(MeManager.MainPlayer, MapClassId)','Host.GetGroupCurrentMap(MapClassId)')
  text=text.replace('AloneWorldUtils.TryOpenMapEnterByFastClear(MapClassId, navigateManager)','Host.TryOpenMapEnterByFastClear(MapClassId)').replace('AloneWorldUtils.TryOpenMapGalaxyPanel','Host.TryOpenMapGalaxyPanel')
  text=text.replace('TimeManager.Instance.GetDateTime()','Host.GetDateTime()')
  text=text.replace('Parsers.Instance.MapRegionInfoParser.Get(MapClassId, Region).GetMapRegionUnlockTime(MapPlanesRankInfo.CreateFromPlanes())','Host.GetRegionUnlockTime(MapClassId, Region)')
  text=text.replace('Parsers.Instance.MapRegionInfoParser.Get(MapClassId, Region).IsUnlocked(dateTime2, MapPlanesRankInfo.CreateFromPlanes())','Host.IsRegionUnlocked(MapClassId, Region, dateTime2)')
  text=text.replace('MapRegionInfo previousRegion = Parsers.Instance.MapRegionInfoParser.GetPreviousRegion(MapClassId, Region);','HomeTaskJumpRegion previousRegion = Host.GetPreviousRegion(MapClassId, Region);')
  text=text.replace('MeManager.MainPlayer.TryGetLastMapEntity(num, MapWorldActiveableEntityType.Entry, out var _, out var bestRegion)','Host.TryGetLastMapEntry(num, out var bestRegion)')
  text=text.replace('CultureTimeSpanArg cultureTimeSpanArg = new CultureTimeSpanArg(mapRegionUnlockTime - dateTime, ETimeSpanFormatType.Detail);','object cultureTimeSpanArg = Host.FormatDetailedTimeSpan(mapRegionUnlockTime - dateTime);')
  text=text.replace('BroadcastManager.Instance.SystemMsg(LanguageManager.Instance.GetTextFormated("MapUnLockTip", cultureTimeSpanArg));','Host.Broadcast("MapUnLockTip", cultureTimeSpanArg);')
  text=text.replace('BroadcastManager.Instance.SystemMsg(LanguageManager.Instance.GetText("MainlandPanel.MapUnLockLimit"));','Host.Broadcast("MainlandPanel.MapUnLockLimit");')
  text=text.replace('LanguageUtils.TextKeyArgWrap','HomeTaskJumpText').replace('LanguageUtils.GetRegionNameKey','Host.GetRegionNameKey')
  text=text.replace('WindowManager.Instance.Popup((ConfirmDialog dialog) =>','Host.Popup(dialog =>')
  text=text.replace('IMapRootManagerProxy service = ServiceProviderServiceExtensions.GetService<IMapRootManagerProxy>(((Component)(object)dialog).GetSceneContainer());','// The bound map owner retains the dialog scene scope.')
  text=text.replace('MeManager.MainPlayer.GotoMapRegion(service, MapClassId, Region);','Host.GotoMapRegion(MapClassId, Region);')
  text=text.replace('navigateManager.Navigate(NavigateUtils.CreatRegionData(MapClassId, Region), jumpEndCallback);','Host.NavigateRegion(MapClassId, Region, jumpEndCallback);')
  text=text.replace('navigateManager.Navigate(NavigateUtils.CreateNavigateData(ENavigateType.FantasyLadder), jumpEndCallback);','Host.NavigateFantasyLadder(jumpEndCallback);')
  text=text.replace('navigateManager.TraceTaskWhenEnterMap(taskData.TaskId);','Host.TraceTaskWhenEnterMap(taskId);')
  text=text.replace('MapLogicEntityTagInfo mapLogicEntityTagInfo = Parsers.Instance.MapLogicEntityTagInfoParser.Get(Tag);','HomeTaskJumpTag mapLogicEntityTagInfo = Host.GetTag(Tag);')
  text=text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerMapTryEnterToEntityNearEntryPoint(mapLogicEntityTagInfo.MapClass, mapLogicEntityTagInfo.LogicId);','Host.EnterNearEntity(mapLogicEntityTagInfo.MapClass, mapLogicEntityTagInfo.LogicId);')
  text=text.replace('MapWorldLoEntityInfoWrap containerEntityInfo = MapRegionEntityInfoManager.Instance.GetContainerEntityInfo(mapLogicEntityTagInfo.MapClass, mapLogicEntityTagInfo.LogicId);','HomeTaskJumpRegion containerEntityInfo = Host.GetEntityRegion(mapLogicEntityTagInfo.MapClass, mapLogicEntityTagInfo.LogicId);')
  text=text.replace('containerEntityInfo.PosInfo.Region','containerEntityInfo.Region')
  text=text.replace('MapTraceUtils.IsInTheMap(Parsers.Instance.MapLogicEntityTagInfoParser.Get(Tag).MapClass)','Host.IsInTheMap(Host.GetTag(Tag).MapClass)').replace('MapTraceUtils.IsInTheMap(MapClassId)','Host.IsInTheMap(MapClassId)').replace('return CanTraceEntityToJump();','return Host.CanTraceEntityToJump(MapClassId, Region);')
  text=text.replace('UniTaskExtensions.Forget<bool>(ServiceProviderServiceExtensions.GetService<IMapLeaveHandlerProxy>(ApplicationScope.Current).LeaveMap());','Host.LeaveMap();')
  extras='private bool IsAllMap => MapClassId == -1;\n' if name=='ClientMapGameTotalCountCondition' else ''
  if name=='ClientMapGameTotalCountCondition':
   shared=read('common-source/Common/MapGameTotalCountCondition.cs');assert 'sAllMap = -1' in shared
  for forbidden in ('MeManager','Parsers.','navigateManager','ServiceProvider','ServiceProxy','MapTraceUtils','TimeManager','WindowManager','LanguageManager','BroadcastManager','TaskData','MapPlanesRankInfo','AloneWorldUtils'):
   assert forbidden not in text,(name,forbidden)
  pieces.append('public sealed class '+name+':HomeTaskJumpCondition\n{\n'+extras+text+'\n}\n')
 header='''// Selected official condition methods; explicit local service boundaries.
using System;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomeTaskUI
{
public abstract class HomeTaskJumpCondition
{
    public IHomeTaskJumpHost Host;
    public int MapClassId,Region;
    public string Tag;
    public bool IsJumpable;
    public abstract bool CanJump();
    public abstract void Jump(long taskId,Action<object> completed);
    public virtual bool CanTrace()=>false;
}
'''
 DEST.write_text(header+'\n'.join(pieces)+'}\n','utf8',newline='\n')
 texts={};language={}
 keys={'confirmation','confirm','cancel','jump_to_map_tip0','jump_to_map_tip1','jump_to_map_tip2','jump_to_home_tip0','MapUnLockTip','MainlandPanel.MapUnLockLimit','task_start_trace_target_tip','task_stop_trace_target_tip','PLAYER_MAP_GROUP_GOTO_TARGET_MAP_IS_SAME'}
 with Apk.current() as apk:
  assert apk.version=='884_156692'
  for name in ('wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile','wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile'):
   verified=apk.reader.read('WwiseBnk',name);texts.update(read_texts(verified.data,keys));language[name]={'source':dict(verified.provenance),'payload_sha256':hashlib.sha256(verified.data).hexdigest()}
 assert keys==texts.keys(),keys-texts.keys()
 text_path=ROOT/'unity/Assets/Game/Resources/Home/home-task-navigation-texts.json'
 text_path.write_text(json.dumps({'manifest':'884_156692','texts':texts},ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
 ports=DEST.with_name('HomeTaskJumpServices.cs')
 read('client-source/NavigateUtils.cs')
 redirector=read('client-source/ClientConditionRedirector.cs')
 for name in names:assert 'new '+name+'(_buf)' in redirector,name
 assert '2096036047' not in redirector
 read('common-source/Common/MaxProfessionRankCondition.cs');read('common-source/Common/ICondition.cs')
 for relative in ('scripts/prepare_home_mainline_task_data.py','scripts/official_apk.py','scripts/official_bundle_reader.py'):
  inputs[relative]=sha(ROOT/relative)
 adapter=ROOT/'unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskNavigation.cs'
 paths=[DEST,ports,adapter,text_path]
 for path in list(paths):
  meta=Path(str(path)+'.meta')
  guid=hashlib.md5(('zjcs-official:'+path.relative_to(ROOT/'unity').as_posix()).encode()).hexdigest()
  importer='TextScriptImporter' if path.suffix=='.json' else 'MonoImporter'
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+importer+':\n  externalObjects: {}\n','utf8',newline='\n')
  paths.append(meta)
 inputs['scripts/fixtures/home_task_row_profile_validation.cs']=sha(ROOT/'scripts/fixtures/home_task_row_profile_validation.cs')
 inputs['scripts/fixtures/home_task_navigation_validation.cs']=sha(ROOT/'scripts/fixtures/home_task_navigation_validation.cs')
 inputs['unity/Assets/Game/Resources/Home/home-mainline-task-data.json']=sha(ROOT/'unity/Assets/Game/Resources/Home/home-mainline-task-data.json')
 inputs['scripts/prepare_home_task_map_data.py']=sha(ROOT/'scripts/prepare_home_task_map_data.py')
 proof={'manifest':'884_156692','generator_sha256':sha(Path(__file__)),'inputs':inputs,'language':language,'outputs':{p.relative_to(ROOT).as_posix():sha(p) for p in paths},'scope':'Selected original CanJump/Jump/CanTrace condition branches; map access, confirmations and trace entity services remain explicit host boundaries. No production route completion claim.'}
 (ROOT/'assets/reference/home-task-navigation-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
 print('Migrated six original task navigation condition classes',flush=True)
 return proof
def stage(project,proof):
 from validation_workspace import validation_directory
 from prepare_home_task_row_profile_validation import build as build_rows,stage as stage_rows
 from prepare_home_task_player import build as build_player
 from prepare_home_task_map_data import build as build_maps
 assert project.resolve().is_relative_to(validation_directory().resolve())
 player=build_player();build_maps();native=build_rows();stage_rows(project,player,native)
 for relative,digest in proof['outputs'].items():
  assert sha(ROOT/relative)==digest
  target=project/(ROOT/relative).relative_to(ROOT/'unity');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/relative,target)
 folder=project/'Assets/HomeCurrencyWindowsNativeValidation'
 shutil.copy2(ROOT/'scripts/fixtures/home_task_navigation_validation.cs',folder/'HomeTaskNavigationValidation.cs')
 path=folder/'HomeCurrencyWindowsNativeValidation.cs';body=path.read_text('utf8');marker='        yield return ExerciseTaskRowsWithProfile();';assert body.count(marker)==1
 path.write_text(body.replace(marker,'        ConfigureTaskRowNavigation();\n'+marker+'\n        yield return ExerciseTaskNavigation();'),'utf8',newline='\n')
 print('Staged original task navigation with native confirmation pointers',flush=True)
if __name__=='__main__':
 parser=argparse.ArgumentParser(__doc__);parser.add_argument('--project',type=Path);args=parser.parse_args();proof=build()
 if args.project:stage(args.project,proof)
