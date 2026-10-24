"""Compile original travel NPC sorting/status/greedy selection for UI vectors."""
import hashlib
import json
import random
import subprocess
from pathlib import Path
from generate_battle_reference import method

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/official-code'
OUT = ROOT / '.tools/gameplay-fixtures/combat'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    source_path = SOURCE/'client-source/NpcAdventureTravelDialog.cs'
    original = source_path.read_text('utf8')
    markers = ['private int CompareTargetData(', 'private int GetNpcStatusScore(',
               'private NpcAdventureTravelNpcStatus GetNpcStatus(', 'private HashSet<int> GetUnselectNpcIds(',
               'private int GetNpcMeetConditionCount(', 'private double GetExtraRewardNpcAddProbability(',
               'private void DoQuickSelectNpcIds(']
    rules = json.loads((ROOT/'assets/official/rules.json').read_text('utf8'))
    tables = json.loads((ROOT/'assets/official/dungeons.json').read_text('utf8'))['tables']
    code = [r'''
using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;using Common=PinnedTravelSelection;
namespace PinnedTravelSelection {
public enum Profession {} public enum NpcGiftType {} public enum Quality {}
public enum PropType {TravelNpcExtraRewardAddProbability=6012}
public enum NpcAdventureTravelNpcStatus {Selected,Unselect,Traveling,Rest}
public enum FunctionType {PinNpc}
public class ActorTargetData {} public class NpcTargetData:ActorTargetData {public int NpcClassId;}
public class PlayerNpcDataWrap {public bool IsCollected;public DateTime CollectedTime;}
public class NpcManager {public Dictionary<int,PlayerNpcDataWrap> values=new Dictionary<int,PlayerNpcDataWrap>();public PlayerNpcDataWrap GetByNpcClassId(int id)=>values.GetValueOrDefault(id);}
public class NpcInfo {public Profession TravelProfession;public int MapGroupId;public NpcGiftType PreferenceGiftType;}
public class NpcInfoParser {public Dictionary<int,NpcInfo> values=new Dictionary<int,NpcInfo>();public NpcInfo Get(int id)=>values.GetValueOrDefault(id);}
public class NpcAdventureTravelInfo {public Quality Quality;public Dictionary<Profession,int> ProfessionDict;public Dictionary<int,int> MapGroupDict;public Dictionary<NpcGiftType,int> PreferenceDict;}
public class NpcAdventureSettings {public int NpcDailyTravelCount;public Dictionary<Quality,int> QualityTravelNpcCountDict,QualityTravelExtraRewardConditionAddDict;}
public class Settings {public NpcAdventureSettings NpcAdventureSettings=new NpcAdventureSettings();}
public class Parsers {public static Parsers Instance=new Parsers();public static Settings GameSettings=new Settings();public NpcInfoParser NpcInfoParser=new NpcInfoParser();}
public class Target {public DateTime StartTravelTime;public int Duration;public HashSet<int> TravelNpcIds=new HashSet<int>();}
public class PlayerNpcAdventureTravelDataWrap {public Dictionary<int,Target> TravelTargetDataDict=new Dictionary<int,Target>();}
public class PlayerNpcAdventureDataWrap {public PlayerNpcAdventureTravelDataWrap TravelData=new PlayerNpcAdventureTravelDataWrap();public Dictionary<int,int> Counts=new Dictionary<int,int>();public int GetDailyTravelNpcCount(DateTime date,int id)=>Counts.GetValueOrDefault(id);}
public class Player {
 public bool CanPin;public NpcManager NpcManager=new NpcManager();public PlayerNpcAdventureDataWrap NpcAdventureData=new PlayerNpcAdventureDataWrap();
 public Dictionary<int,Dictionary<PropType,double>> Props=new Dictionary<int,Dictionary<PropType,double>>();
 public bool IsFunctionLimit(FunctionType t)=>!CanPin;
 public Dictionary<PropType,double> GetNpcAdventureTravelNpcProps(int id)=>Props.GetValueOrDefault(id,new Dictionary<PropType,double>());
}
public static class MeManager {public static Player MainPlayer;}
public class TimeManager {public static TimeManager Instance=new TimeManager();public DateTime Now;public DateTime GetDateTime()=>Now;}
public static class Extensions {
''', method((SOURCE/'common-source/Common/PlayerNpcAdventureDataExt.cs').read_text('utf8'),
             'public static int GetNpcAdventureTravelNpcRemainCount('),
            method((SOURCE/'common-source/Common/PlayerNpcAdventureDataExt.cs').read_text('utf8'),
                   'public static bool IsFinishedTravelTargetData(').replace('PlayerNpcAdventureTravelTargetDataWrap', 'Target'),
            '}', 'public class Selection { public List<int> mNpcIds=new List<int>(); public List<ActorTargetData> mActorTargetDatas=new List<ActorTargetData>(); public NpcAdventureTravelInfo mInfo;',
            '\n'.join(method(original, marker).replace('PlayerNpcAdventureTravelTargetDataWrap','Target') for marker in markers), r'''
 public object Run(){
  var statuses=mActorTargetDatas.Select(x=>new {id=(x as NpcTargetData).NpcClassId,status=(int)GetNpcStatus((x as NpcTargetData).NpcClassId)}).ToArray();
  mActorTargetDatas.Sort(CompareTargetData);
  var sorted=mActorTargetDatas.Select(x=>(x as NpcTargetData).NpcClassId).ToArray();
  var scores=sorted.Select(id=>new {id=id,score=GetExtraRewardNpcAddProbability(id,mInfo.ProfessionDict,mInfo.MapGroupDict,mInfo.PreferenceDict)}).ToArray();
  DoQuickSelectNpcIds();
  return new {statuses=statuses,sorted=sorted,scores=scores,quick=mNpcIds.ToArray()};
 }
}
public static class Reference {
 static DateTime Epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
 static Dictionary<T,int> Enums<T>(JsonElement d) where T:struct,Enum=>d.EnumerateObject().ToDictionary(p=>(T)Enum.ToObject(typeof(T),int.Parse(p.Name)),p=>p.Value.GetInt32());
 static Dictionary<int,int> Ints(JsonElement d)=>d.EnumerateObject().ToDictionary(p=>int.Parse(p.Name),p=>p.Value.GetInt32());
 public static string[] Evaluate(string inputs,string config){
  var r=JsonDocument.Parse(config).RootElement;
  var settings=r.GetProperty("game_settings").GetProperty("NpcAdventureSettings");
  Parsers.GameSettings.NpcAdventureSettings.NpcDailyTravelCount=settings.GetProperty("NpcDailyTravelCount").GetInt32();
  Parsers.GameSettings.NpcAdventureSettings.QualityTravelNpcCountDict=Enums<Quality>(settings.GetProperty("QualityTravelNpcCountDict"));
  Parsers.GameSettings.NpcAdventureSettings.QualityTravelExtraRewardConditionAddDict=Enums<Quality>(settings.GetProperty("QualityTravelExtraRewardConditionAddDict"));
  foreach(var npc in r.GetProperty("npc").EnumerateArray())Parsers.Instance.NpcInfoParser.values[npc.GetProperty("Id").GetInt32()]=new NpcInfo{TravelProfession=(Profession)npc.GetProperty("TravelProfession").GetInt32(),MapGroupId=npc.GetProperty("MapGroupId").GetInt32(),PreferenceGiftType=(NpcGiftType)npc.GetProperty("PreferenceGiftType").GetInt32()};
  var output=new List<string>();
  foreach(var c in JsonDocument.Parse(inputs).RootElement.EnumerateArray()){
   MeManager.MainPlayer=new Player();var p=MeManager.MainPlayer;
   TimeManager.Instance.Now=Epoch.AddSeconds(c.GetProperty("now").GetInt64());p.CanPin=c.GetProperty("can_pin").GetBoolean();
   p.NpcAdventureData.Counts=Ints(c.GetProperty("counts"));
   foreach(var pin in c.GetProperty("pins").EnumerateObject())p.NpcManager.values[int.Parse(pin.Name)]=new PlayerNpcDataWrap{IsCollected=true,CollectedTime=Epoch.AddSeconds(pin.Value.GetInt64())};
   foreach(var props in c.GetProperty("properties").EnumerateObject())p.Props[int.Parse(props.Name)]=props.Value.EnumerateObject().ToDictionary(e=>(PropType)int.Parse(e.Name),e=>e.Value.GetDouble());
   int slot=1;foreach(var t in c.GetProperty("targets").EnumerateArray())p.NpcAdventureData.TravelData.TravelTargetDataDict[slot++]=new Target{StartTravelTime=Epoch.AddSeconds(t.GetProperty("start").GetInt64()),Duration=t.GetProperty("duration").GetInt32(),TravelNpcIds=t.GetProperty("npcs").EnumerateArray().Select(x=>x.GetInt32()).ToHashSet()};
   var cfg=c.GetProperty("cfg");var selection=new Selection{mInfo=new NpcAdventureTravelInfo{Quality=(Quality)cfg.GetProperty("Quality").GetInt32(),ProfessionDict=Enums<Profession>(cfg.GetProperty("ProfessionDict")),MapGroupDict=Ints(cfg.GetProperty("MapGroupDict")),PreferenceDict=Enums<NpcGiftType>(cfg.GetProperty("PreferenceDict"))},mNpcIds=c.GetProperty("selected").EnumerateArray().Select(x=>x.GetInt32()).ToList(),mActorTargetDatas=c.GetProperty("ids").EnumerateArray().Select(x=>(ActorTargetData)new NpcTargetData{NpcClassId=x.GetInt32()}).ToList()};
   output.Add(JsonSerializer.Serialize(selection.Run()));
  }return output.ToArray();
 }
}}
''']
    ids = [row['Id'] for row in rules['npc'] if row['CanDevelop'] and row['TravelProfession']>0 and row['NpcType']!=2]
    rng = random.Random(854156383)
    now = 2000000000
    cases = []
    for index, cfg in enumerate(tables['npc_adventure_travel'][::max(1,len(tables['npc_adventure_travel'])//80)]):
        candidates = rng.sample(ids,min(len(ids),12))
        count_ids = rng.sample(candidates,2)
        targets = [dict(start=now-(7200 if index%2 else 7199), duration=120,npcs=[candidates[0]])]
        cases.append(dict(name='original-catalog-'+str(cfg['TravelId']),cfg=cfg,ids=candidates,now=now,
                          selected=candidates[-1:] if index%3==0 else [],counts={str(i):1 for i in count_ids},
                          targets=targets,properties={str(i):{'6012':rng.randrange(0,3000)} for i in candidates},
                          pins={str(candidates[1]):now-1,str(candidates[2]):now-2},can_pin=index%2==0))
    cfg=tables['npc_adventure_travel'][0]
    for prop in [0,-5000,600,1200]:
        for can_pin in [False,True]:
            cases.append(dict(name='tie-and-negative-'+str(prop)+'-'+str(can_pin),cfg=cfg,ids=ids[:8],now=now,
                              selected=[],counts={},targets=[],properties={str(i):{'6012':prop} for i in ids[:8]},
                              pins={str(ids[0]):now,str(ids[1]):now},can_pin=can_pin))
    cases.append(dict(name='all-unavailable',cfg=cfg,ids=ids[:8],now=now,selected=[],counts={str(i):1 for i in ids[:8]},
                      targets=[],properties={},pins={},can_pin=True))
    cases.append(dict(name='no-actors',cfg=cfg,ids=[],now=now,selected=[],counts={},targets=[],properties={},pins={},can_pin=False))
    harness=OUT/'PinnedTravelSelection.cs';harness.write_text('\n'.join(code),'utf8')
    inputs=OUT/'travel-selection-input.json';inputs.write_text(json.dumps(cases,ensure_ascii=False),'utf8')
    runner=OUT/'run-travel-selection-reference.ps1'
    def ps(path):return str(path).replace("'","''")
    runner.write_text("$ErrorActionPreference='Stop'\nAdd-Type -Path '"+ps(harness)+"'\n[PinnedTravelSelection.Reference]::Evaluate([IO.File]::ReadAllText('"+ps(inputs)+"'),[IO.File]::ReadAllText('"+ps(ROOT/'assets/official/rules.json')+"'))",'utf8')
    result=subprocess.run(['pwsh','-NoProfile','-File',str(runner)],capture_output=True,text=True,encoding='utf8')
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    lines=result.stdout.splitlines()
    if len(lines)!=len(cases):raise ValueError('C# selection output count differs')
    for case,line in zip(cases,lines):case['expected']=json.loads(line)
    original_paths=['client-source/NpcAdventureTravelDialog.cs','common-source/Common/PlayerNpcAdventureDataExt.cs']
    record=dict(manifest='854_156383',cases=cases,original_code_sha256={p:hashlib.sha256((SOURCE/p).read_bytes()).hexdigest() for p in original_paths},harness_sha256=hashlib.sha256(harness.read_bytes()).hexdigest(),table_sha256={name:hashlib.sha256((ROOT/'assets/official'/name).read_bytes()).hexdigest() for name in ['rules.json','dungeons.json']})
    output=OUT/'travel-selection-reference.json';output.write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n','utf8')
    print('Pinned C# travel NPC selection:',len(cases),'vectors;',output)


if __name__=='__main__':main()
