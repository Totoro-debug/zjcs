"""Recover original task map entry selection and its selected configuration."""
import argparse,hashlib,json,re,shutil
from pathlib import Path
from official_apk import Apk,ROOT
from official_tables import Constructors
from official_map_codec import unpack
from prepare_home_task_map_data import CODE,method,sha

RUNTIME=ROOT/'unity/Assets/Game/Runtime/Official/HomeTaskUI/HomeTaskMapEntryNavigation.cs'
PAYLOAD=ROOT/'unity/Assets/Game/Resources/Home/home-task-map-entry.json'

def build():
 inputs={}
 def read(relative):
  path=ROOT/relative;inputs[relative]=sha(path);return path.read_text('utf-8-sig')
 def original(relative):return read((CODE/relative).relative_to(ROOT).as_posix())
 source=original('common-source/Common/EntityHandler.cs')
 find=method(source,'public int FindRegionNearEntry(')
 usable=method(source,'public static bool IsEntryUsable(').replace('public static bool','public bool',1)
 jump=method(original('client-source/MapGoToUtil.cs'),'public static bool GotoMapRegion(')
 check=method(original('common-source/Common/MapPeriodicityInteractionInfoParser.cs'),'public int CheckCanInteraction(')
 for name in ('EntityUtils','MapInfoManager','MapEntryInfoPackParser','MapEntryInfoPack','MapSingleEntryInfo','MapEntryType','MapRegionStatus','MapEntityPeriodicityInteractionInfoPackParser','MapEntityPeriodicityInteractionInfoPack','MapPartInfoBase'):
  original('common-source/Common/'+name+'.cs')
 for name,expected in (('MapEntryType',['None','Entry','ActivedEntry','WayPoint','PeriodicityInteractionWayPoint']),('MapRegionStatus',['None','Private','Public'])):
  enum=original('common-source/Common/'+name+'.cs');assert re.findall(r'\w+',enum[enum.index('{')+1:enum.rindex('}')])==expected,name
 text=find+'\n'+usable
 replacements={
  'Player player':'IHomeTaskMapEntryState player',
  'MapInfoManager.Instance.GetMapPartInfo':'Data.GetMapPartInfo',
  'MapPartInfoBase':'HomeTaskMapPart',
  'MapEntryInfoPackParser':'IHomeTaskMapEntryData',
  'MapRegionInfoParser':'IHomeTaskMapEntryData',
  'Parsers.Instance.MapEntryInfoPackParser':'Data',
  'Parsers.Instance.MapRegionInfoParser':'Data',
  'player.GetActivedMapEntities(mapClassId, MapWorldActiveableEntityType.Entry)':'player.GetActivedMapEntries(mapClassId)',
  'MapSingleEntryInfo':'HomeTaskMapSingleEntry',
  'MapEntryType':'HomeTaskMapEntryType',
  'MapRegionStatus':'HomeTaskMapRegionStatus',
  'MapRegionInfo mapRegionInfo':'HomeTaskMapEntryRegion mapRegionInfo',
  'Parsers.Instance.MapEntityPeriodicityInteractionInfoPackParser.GetPeriodicityInteractionId':'Data.GetPeriodicityInteractionId',
  'Parsers.Instance.MapPeriodicityInteractionInfoParser.CheckCanInteraction':'Data.CheckCanInteraction',
 }
 # Parser expressions must be replaced before their type names.
 for a,b in sorted(replacements.items(),key=lambda p:-len(p[0])):
  text=re.sub(r'\b'+a+r'\b',b,text) if a in ('MapEntryType','MapRegionStatus') else text.replace(a,b)
 jump=jump.replace('public static bool GotoMapRegion(this Player self, IMapRootManagerProxy mapRootMgr,','public bool GotoMapRegion(IHomeTaskMapEntryState self, IHomeTaskMapEntryRoute mapRootMgr,')
 jump=jump.replace('MapUtils.GetGroupCurrentMapClassIdByMapClassIdAutoWithTagId(MeManager.MainPlayer, num)','mapRootMgr.GetGroupCurrentMap(num)')
 jump=jump.replace('string text = LanguageManager.Instance.GetText("PLAYER_MAP_GROUP_GOTO_TARGET_MAP_IS_SAME");\n\t\t\t\tBroadcastManager.Instance.SystemMsg(text);','mapRootMgr.Broadcast("PLAYER_MAP_GROUP_GOTO_TARGET_MAP_IS_SAME");')
 jump=jump.replace('MeManager.MainPlayer.TryGetLastMapEntity(num, MapWorldActiveableEntityType.Entry, out var _, out var bestRegion)','self.TryGetLastMapEntry(num, out var bestRegion)')
 jump=jump.replace('EntityUtils.FindRegionNearEntry(MeManager.MainPlayer, num, startRegion)','FindRegionNearEntry(self, num, startRegion)').replace('MeManager.MainPlayer.DoEnterMap(num, entryId)','mapRootMgr.DoEnterMap(num, entryId)')
 check=check.replace('MapPeriodicityInteractionInfo mapPeriodicityInteractionInfo = Get(groupId);','HomeTaskMapPeriodicity mapPeriodicityInteractionInfo = Data.GetPeriodicity(groupId);')
 for forbidden in ('MeManager','Parsers.','MapInfoManager.','MapUtils.','LanguageManager.','BroadcastManager.'):
  assert forbidden not in text+jump+check,forbidden
 header='''// Selected official EntityHandler and MapGoToUtil methods. Only service/type bindings differ.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeTaskUI
{
public sealed class HomeTaskMapEntryNavigation
{
    public IHomeTaskMapEntryData Data {get;}
    public HomeTaskMapEntryNavigation(IHomeTaskMapEntryData data){Data=data??throw new ArgumentNullException(nameof(data));}
'''
 code=header+text+'\n'+jump+'\n}\npublic sealed class HomeTaskMapPeriodicityRules\n{\n public IHomeTaskMapEntryData Data {get;}\n public HomeTaskMapPeriodicityRules(IHomeTaskMapEntryData data){Data=data??throw new ArgumentNullException(nameof(data));}\n'+check+'\n}\n}\n'
 RUNTIME.write_text('\n'.join(line.rstrip() for line in code.splitlines())+'\n','utf8',newline='\n')
 tables={};proofs={}
 with Apk.current() as apk:
  assert apk.version=='884_156692'
  constructors=Constructors(CODE)
  mapping={name.rsplit('/',1)[-1]:kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',constructors.source('Tables'))}
  for name in ('map_region','map_world','map_periodicity_interaction'):
   obj=apk.asset('defaultpackage_assets_config_luban_map.bundle','/'+name+'.g.bytes');raw=obj.read().m_Script.encode('utf8','surrogateescape')
   tables[name]=constructors.table(raw,mapping[name]);proofs[name]={'source':dict(apk.provenance(obj)),'payload_sha256':hashlib.sha256(raw).hexdigest(),'object_sha256':hashlib.sha256(obj.get_raw_data()).hexdigest()}
  for name in ('MapEntryInfoPack','MapEntityPeriodicityInteractionInfoPack'):
   obj=apk.asset('config_map','/'+name+'.g.bytes');raw=obj.read().m_Script.encode('utf8','surrogateescape');tables[name]=unpack(raw)[0]
   proofs[name]={'source':dict(apk.provenance(obj)),'payload_sha256':hashlib.sha256(raw).hexdigest(),'object_sha256':hashlib.sha256(obj.get_raw_data()).hexdigest()}
  for name in constructors.sources:
   path=CODE/'common-source/Common'/(name+'.cs')
   if not path.exists():path=CODE/'common-source/Common.LubanGen'/(name+'.cs')
   read(path.relative_to(ROOT).as_posix())
 tasks=json.loads(read('unity/Assets/Game/Resources/Home/home-mainline-task-data.json'))
 maps=sorted({c['MapClassId'] for row in tasks['tasks'] if (c:=row['FinishCondition']).get('MapClassId',-1)>0})
 assert maps==[10,11,12],maps
 worlds=[row for row in tables['map_world'] if row['ClassId'] in maps]
 assert {row['ClassId'] for row in worlds}==set(maps)
 data={'manifest':'884_156692','maps':maps,'worlds':worlds,'regions':[row for row in tables['map_region'] if row['ClassId'] in maps],
       'entries':{m:tables['MapEntryInfoPack'][m] for m in maps},'periodicityIds':{m:tables['MapEntityPeriodicityInteractionInfoPack'].get(m,{}) for m in maps},'periodicity':tables['map_periodicity_interaction']}
 PAYLOAD.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
 checksum=PAYLOAD.with_name('home-task-map-entry-hash.json');checksum.write_text(json.dumps({'manifest':'884_156692','output_sha256':sha(PAYLOAD)},indent=2)+'\n','utf8',newline='\n')
 paths=[RUNTIME,RUNTIME.with_name('HomeTaskMapEntryServices.cs'),ROOT/'unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskMapEntryData.cs',PAYLOAD,checksum]
 for path in list(paths):
  meta=Path(str(path)+'.meta');guid=hashlib.md5(('zjcs-official:'+path.relative_to(ROOT/'unity').as_posix()).encode()).hexdigest()
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+('MonoImporter' if path.suffix=='.cs' else 'TextScriptImporter')+':\n  externalObjects: {}\n','utf8',newline='\n')
  paths.append(meta)
 for name in ('official_apk','official_bundle_reader','official_tables','official_map_codec','prepare_home_task_map_data','prepare_home_task_navigation'):
  read('scripts/'+name+'.py')
 read('scripts/fixtures/home_task_map_entry_validation.cs')
 read('scripts/fixtures/home_task_map_entry_native_validation.cs')
 read('scripts/fixtures/home_task_navigation_validation.cs')
 proof={'manifest':'884_156692','generator_sha256':sha(Path(__file__)),'inputs':inputs,'tables':proofs,'outputs':{p.relative_to(ROOT).as_posix():sha(p) for p in paths},'scope':'Original regional entry breadth-first search, entry usability, periodic interaction result and group-aware task route. Live activation/period counts and actual map/window owner remain required ports.'}
 (ROOT/'assets/reference/home-task-map-entry-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
 print('Recovered task entry selection for',maps,'and',len(data['regions']),'original regions',flush=True)
 return proof

def stage(project,proof):
 from validation_workspace import validation_directory
 from prepare_home_task_navigation import build as build_navigation,stage as stage_navigation
 assert project.resolve().is_relative_to(validation_directory().resolve())
 navigation=build_navigation();stage_navigation(project,navigation)
 for relative,digest in proof['outputs'].items():
  assert sha(ROOT/relative)==digest
  target=project/(ROOT/relative).relative_to(ROOT/'unity');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/relative,target)
 folder=project/'Assets/HomeTaskMapEntryValidation';folder.mkdir(exist_ok=True)
 shutil.copy2(ROOT/'scripts/fixtures/home_task_map_entry_validation.cs',folder/'HomeTaskMapEntryValidation.cs')
 (folder/'HomeTaskMapEntryValidation.asmdef').write_text(json.dumps({'name':'HomeTaskMapEntryValidation','references':['Zjcs.Offline.HomeTaskUI','Zjcs.Official.HomeTaskUI','Unity.Newtonsoft.Json'],'includePlatforms':['Editor']}),'utf8')
 native=project/'Assets/HomeCurrencyWindowsNativeValidation'
 shutil.copy2(ROOT/'scripts/fixtures/home_task_map_entry_native_validation.cs',native/'HomeTaskMapEntryNativeValidation.cs')
 path=native/'HomeCurrencyWindowsNativeValidation.cs';body=path.read_text('utf8');marker='        ConfigureTaskRowNavigation();';assert body.count(marker)==1
 path.write_text(body.replace(marker,marker+'\n        ConfigureTaskMapEntryNative();'),'utf8',newline='\n')

if __name__=='__main__':
 parser=argparse.ArgumentParser(__doc__);parser.add_argument('--project',type=Path);args=parser.parse_args();proof=build()
 if args.project:stage(args.project,proof)
