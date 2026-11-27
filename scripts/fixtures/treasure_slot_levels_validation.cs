using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Official.TreasureSlots;
using Zjcs.Official.HomeExperienceRules;
using S = Zjcs.Official.TreasureSlots;
using O = TreasureSlotOracle;
using H = Zjcs.Official.HomeExperienceRules;

public static class TreasureSlotLevelsValidation
{
    static string Work => Environment.GetEnvironmentVariable("ZJCS_TREASURE_SLOT_LEVELS_OUTPUT");
    static int checks, scenarios;
    static JObject raw, slotsRaw;
    static OfflineTreasureSlotCatalog slots;
    static OfflineTreasureSlotLevelCatalog levels;
    static Dictionary<int,int> elements;
    static readonly long Now = 1795767000;
    static void Check(bool value,string name) { checks++; if(!value) throw new Exception("Treasure slot levels: "+name); }
    static string State(object value) => JsonConvert.SerializeObject(value);
    public static void Run()
    {
        try {
            raw=JObject.Parse(File.ReadAllText(Path.Combine(Work,"OriginalTreasureSlotLevels.json")));
            slotsRaw=JObject.Parse(File.ReadAllText(Path.Combine(Work,"OriginalTreasureSlots.json")));
            elements=slotsRaw["Items"].ToDictionary(row=>(int)row["ClassId"],row=>(int)row["ElementType"]);
            slots=OfflineTreasureSlotCatalog.LoadDefault(); levels=OfflineTreasureSlotLevelCatalog.LoadDefault();
            CheckKernel(); CheckConditions(); Debug.Log("TREASURE_SLOT_LEVELS_KERNEL_PASSED: "+checks); CheckSaved();
            File.WriteAllText(Path.Combine(Work,"result.json"),State(new{success=true,checks,scenarios,limits=raw["Limits"].Count()}));
            Debug.Log("TREASURE_SLOT_LEVELS_PASSED: "+checks+" checks / "+scenarios+" state scenarios"); EditorApplication.Exit(0);
        } catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static O.TreasureSlotLevelTables OriginalTables() => new O.TreasureSlotLevelTables(
        raw["Limits"].ToObject<O.SystemLevelLimitInfo[]>(),raw["Gaps"].ToObject<O.SystemLevelGapLimitInfo[]>(),
        raw["LevelCosts"].ToObject<O.TreasureLevelInfo[]>(),raw["BlessCosts"].ToObject<O.TreasureBlessInfo[]>(),raw["Seasons"].ToObject<O.TreasureLevelSeason[]>());
    static S.Player Local() => new S.Player(slots.CreateSettings(),id=>elements.TryGetValue(id,out int e)
        ?new S.SlotItem{Id=id,Count=1,Info=new S.SlotItemInfo{ElementType=(S.ElementType)e}}:null,_=>{});
    static O.Player Original()
    {
        O.Parsers.GameSettings=new O.TreasureSlotSettings{TreasureMaxSlotCount=slots.MaxSlotCount,TreasurePlanMaxCount=slots.MaxPlanCount,
            TreasureSlotElementTypes=slotsRaw["TreasureSlotElementTypes"].Select(x=>(O.ElementType)(int)x).ToList()};
        return new O.Player(O.Parsers.GameSettings,id=>elements.TryGetValue(id,out int e)
            ?new O.SlotItem{Id=id,Count=1,Info=new O.SlotItemInfo{ElementType=(O.ElementType)e}}:null);
    }
    static void Seed(S.Player p,int level,int bless,bool all)
    {
        foreach(var e in p.Settings.TreasureSlotElementTypes) {
            var g=p.GetTreasureElementSlotData(e);
            for(int i=0;i<(all?slots.MaxSlotCount:1);i++) {
                g.InsertTreasureToSlot(elements.First(row=>row.Value==(int)e).Key,i);
                g.TreasureSlots[i].BaseLevel=level;g.TreasureSlots[i].BlessLevel=bless;
            }
            g.UpdateTreasureSlotsShareLevel(); if(!all)break;
        }
    }
    static void Seed(O.Player p,int level,int bless,bool all)
    {
        foreach(var e in O.Parsers.GameSettings.TreasureSlotElementTypes) {
            var g=p.GetTreasureElementSlotData(e);
            for(int i=0;i<(all?slots.MaxSlotCount:1);i++) {
                g.InsertTreasureToSlot(elements.First(row=>row.Value==(int)e).Key,i);
                g.TreasureSlots[i].BaseLevel=level;g.TreasureSlots[i].BlessLevel=bless;
            }
            g.UpdateTreasureSlotsShareLevel(); if(!all)break;
        }
    }
    static object Graph(S.Player p) => new {p.TreasureSlotData.SeasonId,Groups=p.TreasureSlotData.TreasureSlotDataDict.Select(e=>new{
        Element=(int)e.Key,e.Value.ShareLevel,e.Value.BaseShareLevel,Slots=e.Value.TreasureSlots.Select(s=>new{s.Key,s.Value.TreasureClassId,s.Value.BaseLevel,s.Value.BlessLevel})}),
        Records=p.BlessRecords.Select(r=>new{r.Key,Groups=r.Value.ElementTreasureSlotBlessDataDict.Select(e=>new{Element=(int)e.Key,Slots=e.Value.TreasureSlotBlessLevelDict})})};
    static object Graph(O.Player p) => new {p.TreasureSlotData.SeasonId,Groups=p.TreasureSlotData.TreasureSlotDataDict.Select(e=>new{
        Element=(int)e.Key,e.Value.ShareLevel,e.Value.BaseShareLevel,Slots=e.Value.TreasureSlots.Select(s=>new{s.Key,s.Value.TreasureClassId,s.Value.BaseLevel,s.Value.BlessLevel})}),
        Records=p.BlessRecords.Select(r=>new{r.Key,Groups=r.Value.ElementTreasureSlotBlessDataDict.Select(e=>new{Element=(int)e.Key,Slots=e.Value.TreasureSlotBlessLevelDict})})};
    static List<string> Events(S.Player p)
    {
        var result=new List<string>();p.OnTreasureLevelUp+=(item,old,n)=>result.Add("level:"+item.Id+":"+old+":"+n);
        p.OnTreasureRefresh+=(item,n)=>result.Add("refresh:"+item.Id+":"+n);p.OnTreasureSlotShareLevelUpdate+=e=>result.Add("share:"+(int)e);
        p.OnAllTreasureBlessUpdate+=()=>result.Add("all");p.OnSeasonBlessDataUpdate+=s=>result.Add("season:"+s);return result;
    }
    static List<string> Events(O.Player p)
    {
        var result=new List<string>();p.OnTreasureLevelUp+=(item,old,n)=>result.Add("level:"+item.Id+":"+old+":"+n);
        p.OnTreasureRefresh+=(item,n)=>result.Add("refresh:"+item.Id+":"+n);p.OnTreasureSlotShareLevelUpdate+=e=>result.Add("share:"+(int)e);
        p.OnAllTreasureBlessUpdate+=()=>result.Add("all");p.OnSeasonBlessDataUpdate+=s=>result.Add("season:"+s);return result;
    }
    static void CheckKernel()
    {
        var lt=levels.CreateTables();var ot=OriginalTables();O.Parsers.Tables=ot;
        var seen=new HashSet<int>();int element=(int)slots.CreateSettings().TreasureSlotElementTypes[0];
        foreach(var row in raw["Limits"]) {
            int rank=(int)row["SubRank"],baseLevel=(int)row["PlayerLevel"],blessLevel=(int)row["PlayerBlessLevel"];
            Check(State(lt.Limits.GetLevelLimitInfo((S.SubRank)rank,baseLevel,blessLevel))==State(ot.Limits.GetLevelLimitInfo((O.SubRank)rank,baseLevel,blessLevel)),"exact player/subrank/bless table row");
            int cap=lt.Limits.GetTreasureBaseLevelLimit((S.SubRank)rank);
            Check(cap==ot.Limits.GetTreasureBaseLevelLimit((O.SubRank)rank)&&lt.Limits.GetTreasureBlessLevelLimit((S.SubRank)rank)==ot.Limits.GetTreasureBlessLevelLimit((O.SubRank)rank),"original separate base/bless maxima");
            foreach(int worldDelta in new[]{0,1}) foreach(int training in new[]{1,Math.Max(1,cap-1),cap,cap+1}) foreach(bool all in new[]{false,true}) {
                var p=Local();var q=Original();p.Tables=lt;q.Tables=ot;p.SubRank=(S.SubRank)rank;q.SubRank=(O.SubRank)rank;
                p.BaseLevel=q.BaseLevel=baseLevel;p.PlayerBlessLevel=q.PlayerBlessLevel=blessLevel;p.Level=q.Level=baseLevel+blessLevel;
                p.MyPlanesRank=(S.Rank)((int)p.Rank+worldDelta);q.MyPlanesRank=(O.Rank)(int)p.MyPlanesRank;O.PlanesInfoUtils.PlanesRank=q.MyPlanesRank;
                p.ServerRecordSeason=q.ServerRecordSeason=lt.GetSeasonByRank(p.Rank);
                Seed(p,Math.Min(training,Math.Max(1,cap)),Math.Max(0,training-cap),all);Seed(q,Math.Min(training,Math.Max(1,cap)),Math.Max(0,training-cap),all);
                foreach(bool funded in new[]{false,true}) foreach(bool auto in new[]{false,true}) {
                    p.ReadItemAmount=q.ReadItemAmount=id=>funded?100000000L:0L;p.MergeSourceAvailable=q.MergeSourceAvailable=id=>auto;
                    lt.CalculateMergeable=(x,id,rec)=>funded?100000L:0L;ot.CalculateMergeable=(x,id,rec)=>funded?100000L:0L;
                    int a=S.TreasureHelper.CanTreasureSlotLevelUp(p,(S.ElementType)element,0,auto),b=O.TreasureHelper.CanTreasureSlotLevelUp(q,(O.ElementType)element,0,auto);
                    seen.Add(a);Check(a==b,"original ordered level/cap/material qualification");
                }
                Check(p.CanTresureSlotBlessLevelUp()==q.CanTresureSlotBlessLevelUp(),"world rank and all-slot blessing prerequisites");
                Check(State(Graph(p))==State(Graph(q)),"qualification lazy creation order matches source");scenarios++;
            }
        }
        foreach(int rank in Enum.GetValues(typeof(S.SubRank))) {
            var p=Local();var q=Original();p.Tables=lt;q.Tables=ot;p.SubRank=(S.SubRank)rank;q.SubRank=(O.SubRank)rank;
            Check((int)p.Rank==(int)q.Rank,"complete official rank switch");
            foreach(int n in Enumerable.Range(1,55)) Check(State(S.TreasureHelper.GetTreasureLevelUpCostMaterials(p,null,n))==State(O.TreasureHelper.GetTreasureLevelUpCostMaterials(q,null,n)),"base and blessing fees including missing rows");
        }
        foreach(var row in raw["Gaps"]) foreach(int delta in new[]{-1,0,1}) foreach(int minimum in new[]{0,1,(int)row["LimitLevel"]-1,(int)row["LimitLevel"]}) {
            int n=(int)row["Level"]+delta,season=(int)row["SeasonId"],type=(int)row["DevelopType"];
            bool a=lt.Gaps.TryGetSystemLevelGapInfo((S.DevelopType)type,n,season,out int al),b=ot.Gaps.TryGetSystemLevelGapInfo((O.DevelopType)type,n,season,out int bl);
            Check(a==b&&al==bl,"ordered level gap interval endpoints");
            a=lt.Gaps.IsBlessLevelGapLimit((S.DevelopType)type,n,minimum,out al,season);b=ot.Gaps.IsBlessLevelGapLimit((O.DevelopType)type,n,minimum,out bl,season);
            Check(a==b&&al==bl,"gap minimum and configured open switch");
        }
        var empty=Local();empty.Tables=lt;Check(S.TreasureHelper.CanTreasureSlotLevelUp(empty,(S.ElementType)element,0,false)==53307,"missing-slot first error");
        empty.GetTreasureElementSlotData((S.ElementType)element).InsertTreasureToSlot(0,0);
        Check(S.TreasureHelper.CanTreasureSlotLevelUp(empty,(S.ElementType)element,0,false)==1189,"empty/unowned-slot next error");
        Check(seen.Contains(0)&&seen.Contains(53302)&&seen.Contains(53312)&&seen.Contains(53314)&&seen.Contains(1188),"exercise successful/base-cap/all-slot/player-cap/material branches: "+string.Join(",",seen));
        var blessRow=raw["Limits"].First(row=>(int)row["TreasureBlessLevel"]>0);
        var invalid=Local();var invalidOriginal=Original();invalid.Tables=lt;invalidOriginal.Tables=ot;
        invalid.SubRank=(S.SubRank)(int)blessRow["SubRank"];invalidOriginal.SubRank=(O.SubRank)(int)blessRow["SubRank"];
        invalid.MyPlanesRank=invalid.Rank;invalidOriginal.MyPlanesRank=invalidOriginal.Rank;O.PlanesInfoUtils.PlanesRank=invalidOriginal.Rank;
        invalid.BaseLevel=invalidOriginal.BaseLevel=999999;
        int invalidCap=lt.Limits.GetTreasureBaseLevelLimit(invalid.SubRank);Seed(invalid,invalidCap,0,true);Seed(invalidOriginal,invalidCap,0,true);
        Check(S.TreasureHelper.CanTreasureSlotLevelUp(invalid,(S.ElementType)element,0,true)==53313&&O.TreasureHelper.CanTreasureSlotLevelUp(invalidOriginal,(O.ElementType)element,0,true)==53313,"missing exact player level row precedes material checks");
        var noCosts=new S.TreasureSlotLevelTables(raw["Limits"].ToObject<S.SystemLevelLimitInfo[]>(),raw["Gaps"].ToObject<S.SystemLevelGapLimitInfo[]>(),
            Array.Empty<S.TreasureLevelInfo>(),Array.Empty<S.TreasureBlessInfo>(),raw["Seasons"].ToObject<S.TreasureLevelSeason[]>());
        var noCostsOriginal=new O.TreasureSlotLevelTables(raw["Limits"].ToObject<O.SystemLevelLimitInfo[]>(),raw["Gaps"].ToObject<O.SystemLevelGapLimitInfo[]>(),
            Array.Empty<O.TreasureLevelInfo>(),Array.Empty<O.TreasureBlessInfo>(),raw["Seasons"].ToObject<O.TreasureLevelSeason[]>());
        invalid=Local();invalidOriginal=Original();invalid.Tables=noCosts;invalidOriginal.Tables=noCostsOriginal;O.Parsers.Tables=noCostsOriginal;
        invalid.SubRank=S.SubRank.Silver1;invalidOriginal.SubRank=O.SubRank.Silver1;O.PlanesInfoUtils.PlanesRank=O.Rank.Silver;
        Seed(invalid,1,0,false);Seed(invalidOriginal,1,0,false);
        Check(S.TreasureHelper.CanTreasureSlotLevelUp(invalid,(S.ElementType)element,0,true)==53315&&O.TreasureHelper.CanTreasureSlotLevelUp(invalidOriginal,(O.ElementType)element,0,true)==53315,"missing fee row preserves original error");O.Parsers.Tables=ot;
        // State transitions and their event order use the untouched original Player methods.
        foreach(var seasonRow in raw["Seasons"]) foreach(bool record in new[]{false,true}) {
            int rank=(int)seasonRow["Rank"],season=(int)seasonRow["Season"];
            int sub=(int)Enum.GetValues(typeof(S.SubRank)).Cast<S.SubRank>().First(r=>(int)r.ToRank()==rank);
            var p=Local();var q=Original();p.Tables=lt;q.Tables=ot;p.SubRank=(S.SubRank)sub;q.SubRank=(O.SubRank)sub;
            p.MyPlanesRank=(S.Rank)rank;q.MyPlanesRank=(O.Rank)rank;O.PlanesInfoUtils.PlanesRank=q.MyPlanesRank;
            p.ServerRecordSeason=q.ServerRecordSeason=season;int cap=Math.Max(1,lt.Limits.GetTreasureBaseLevelLimit(p.SubRank));
            Seed(p,cap,0,true);Seed(q,cap,0,true);
            if(record) {
                var pr=new S.PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap(p.Settings);pr.Init(p,p.Rank);p.BlessRecords.Add(season,pr);
                var qr=new O.PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap(O.Parsers.GameSettings);qr.Init(q,q.Rank);q.BlessRecords.Add(season,qr);
            }
            var pe=Events(p);var qe=Events(q);
            p.TreasureSlotLevelUp((S.ElementType)element,0,1);q.TreasureSlotLevelUp((O.ElementType)element,0,1);
            Check(State(Graph(p))==State(Graph(q))&&State(pe)==State(qe),"original blessing record mutation and event order");
            if(record)Check(p.GetTreasureSlotData((S.ElementType)element,0).BlessLevel==1,"record adds blessing");
            else Check(p.GetTreasureSlotData((S.ElementType)element,0).BlessLevel==0,"missing record does not upgrade");
            p.TryUpdatePlayerTreasureSlotData(p.Rank);q.TryUpdatePlayerTreasureSlotData(q.Rank);
            Check(State(Graph(p))==State(Graph(q))&&State(pe)==State(qe),"season restore or clear including double all-bless notification");
            if(!record)Check(pe.Count(x=>x=="all")==2,"missing seasonal record publishes original double clear signal");
            p.ClearTreasureBlessLevel();q.ClearTreasureBlessLevel();p.TreasureSlotData.SeasonId=q.TreasureSlotData.SeasonId=0;
            p.TryUpdatePlayerTreasureSlotData(p.Rank);q.TryUpdatePlayerTreasureSlotData(q.Rank);
            Check(State(Graph(p))==State(Graph(q))&&State(pe)==State(qe),"cleared cache restores preserved season history");scenarios++;
        }
    }
    static void CheckConditions()
    {
        foreach(string op in new[]{">",">=","<","<=","==","!=","unexpected"}) foreach(int count in new[]{0,1,2}) {
            var p=Local();var q=Original();p.ReadItemAmount=q.ReadItemAmount=id=>count;p.BaseLevel=q.BaseLevel=count;
            var pc=new S.HasItemCondition{ClassId=4015,Count=1,Op=op};var qc=new O.HasItemCondition{ClassId=4015,Count=1,Op=op};
            Check(pc.IsMet(new S.ICondition.DataHub(p))==qc.IsMet(new O.ICondition.DataHub(q)),"original item condition comparison");
            Check(new S.LevelCondition{Level=1,Op=op}.IsMet(new S.ICondition.DataHub(p))==new O.LevelCondition{Level=1,Op=op}.IsMet(new O.ICondition.DataHub(q)),"base-level merge gate comparison");
        }
        foreach(int count in new[]{0,1,2}) {
            var p=Local();var q=Original();p.ItemManager.ItemTypeDict.Add(305,new List<S.SlotItem>{new S.SlotItem{Count=count}});
            q.ItemManager.ItemTypeDict.Add(305,new List<O.SlotItem>{new O.SlotItem{Count=count}});
            var pc=new S.HasItemTypeCondition{ItemType=305,Count=1};var qc=new O.HasItemTypeCondition{ItemType=305,Count=1};
            Check(pc.GetProgress(new S.ICondition.DataHub(p))==qc.GetProgress(new O.ICondition.DataHub(q))&&pc.IsMet(new S.ICondition.DataHub(p))==qc.IsMet(new O.ICondition.DataHub(q)),"merge gate counts raw owned item-type amounts");
        }
    }
    static void CheckSaved()
    {
        string root=Path.Combine(Work,"isolated-saves");var creation=CharacterCreationOfflineService.OpenDefault(root);
        string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="槽位升级验证";
        Check(creation.TryCreateCharacter(slot,draft,out var error),"create isolated character: "+error);
        Check(creation.TrySetInitialWeapon(slot,CharacterCreationOfflineService.SwordInitialWeaponType,out error),"select real weapon");
        var catalog=new ProfileSlotCatalog(root);var profile=OfflineMainlineProgressSession.OpenCharacter(catalog,slot);
        string file=Path.Combine(catalog.GetSlotDirectory(slot),"city-save.json");
        var common=OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
        Func<OfflinePlayerProgress,TreasureSlotLevelEnvironment> environment=candidate=>{
            var source=new OfflineHomeExperienceEnvironmentSource(()=>candidate,common,()=>false,p=>0);
            var state=source.Capture();var growth=OfflineHomeExperienceProjection.Capture(candidate,common,state);
            return new TreasureSlotLevelEnvironment{BaseLevel=growth.BaseLevel,Level=growth.Level,PlayerBlessLevel=growth.GetCurBlessPlayerLevel(),
                WorldRank=(int)state.PlanesRank,ServerRecordSeason=state.ServerRecordSeason,ActiveRecordSeason=state.CurrentRecordData?.Season};
        };
        var service=new OfflineTreasureSlotService(profile,slots,levels,environment);
        Check(environment(profile.Save.Player).BaseLevel>0,"real common growth computes fresh base level from experience instead of cached zero");
        CheckSourceGates(service);
        int element=elements.First().Value,id=elements.First().Key;
        // A real single occupied slot; all other trained slots are empty, as official storage permits.
        var graph=Local();graph.GetTreasureElementSlotData((S.ElementType)element).InsertTreasureToSlot(id,0);
        Check(profile.ApplyCharacterMutation(p=>{
            p.SubRank=(int)S.SubRank.Blackiron1;p.CharacterProperties.TreasurePlaneRank=(int)S.Rank.Blackiron;
            p.Inventory.Add(new OfflineItemStack{ItemId=id,Count=1});p.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=id,Active=true,HitCount=1});
            foreach(int material in new[]{1,41400,41401,41402})p.Inventory.Add(new OfflineItemStack{ItemId=material,Count=10000000});
            p.CharacterProperties.TreasureSlots=OfflineTreasureSlotService.Capture(graph);
        }).IsApplied,"seed legitimate inventory and slot state");
        var rawOld=JObject.Parse(File.ReadAllText(file));var savedSlots=(JObject)rawOld["Player"]["CharacterProperties"]["TreasureSlots"];
        savedSlots.Remove("BlessRecordFormat");savedSlots.Remove("BlessRecords");File.WriteAllText(file,rawOld.ToString());
        profile.Reload();Check(profile.ReadStatus==SaveReadStatus.Primary,"old JSON without seasonal fields reads primary");
        var notifications=new List<string>();service.OnTreasureLevelUp+=(item,old,n)=>{notifications.Add("level");Check(File.ReadAllText(file).Contains("treasure:paid"),"events follow durable receipt");};
        service.OnTreasureRefresh+=(item,n)=>notifications.Add("refresh");service.OnTreasureSlotShareLevelUpdate+=e=>notifications.Add("share");
        service.OnAllTreasureBlessUpdate+=()=>notifications.Add("all");service.OnSeasonBlessDataUpdate+=s=>notifications.Add("season");
        byte[] before=File.ReadAllBytes(file);var inspect=service.InspectTreasureSlotLevelUp(element,0,false,Now);
        Check(inspect.Status==ProgressionResultStatus.Ready,"real ordinary level-up qualification: "+inspect.Status+"/"+inspect.ErrorCode);service.CurrentLevelSnapshot(Now);
        var detached=service.CurrentLevelSnapshot(Now);detached.Tables.GetLevelCost(2).Materials.Clear();
        Check(service.InspectTreasureSlotLevelUp(element,0,false,Now).Costs.Count==inspect.Costs.Count&&inspect.Costs.Count>0,"detached table mutation cannot change future upgrade fees");
        Check(File.ReadAllBytes(file).SequenceEqual(before)&&notifications.Count==0,"queries never save lazy slots, history migration or notifications");
        var amounts=inspect.Costs.ToDictionary(row=>row.Key,row=>profile.InventoryCount(row.Key));
        Check(service.UpgradeTreasureSlot(element,0,false,"treasure:paid",Now).IsApplied,"actual paid slot upgrade commits");
        Check(State(notifications)==State(new[]{"level","refresh","share"}),"ordinary official event sequence");
        foreach(var cost in inspect.Costs)Check(profile.InventoryCount(cost.Key)==amounts[cost.Key]-cost.Value,"real exact material debit");
        Check(service.Snapshot().GetTreasureSlotData((S.ElementType)element,0).BaseLevel==2,"actual base-level transition");
        before=File.ReadAllBytes(file);int emitted=notifications.Count;
        Check(service.UpgradeTreasureSlot(element,0,false,"treasure:paid",Now).Status==ProgressionResultStatus.Unchanged&&notifications.Count==emitted&&File.ReadAllBytes(file).SequenceEqual(before),"replay never charges or notifies");
        var cold=OfflineMainlineProgressSession.OpenCharacter(catalog,slot);
        Check(State(cold.Save.Player.CharacterProperties.TreasureSlots)==State(profile.Save.Player.CharacterProperties.TreasureSlots),"cold restores canonical state and history format");
        // Ensure another ordinary level is available, then exercise the actual write failure path.
        Check(profile.ApplyCharacterMutation(p=>p.SubRank=4).IsApplied,"advance isolated fixture to official bronze subrank");
        cold.Reload();Check(cold.ApplyCharacterMutation(p=>p.CharacterProperties.TreasureSlots.AwakeAutoMergeEnable=true).IsApplied,"independent owner writes a newer slot preference");
        before=File.ReadAllBytes(file);Directory.CreateDirectory(file+".tmp");
        try {Check(service.UpgradeTreasureSlot(element,0,false,"treasure:paid-retry",Now).Status==ProgressionResultStatus.InvalidState&&File.ReadAllBytes(file).SequenceEqual(before)&&notifications.Count==emitted,"write failure rolls back payment, slot, receipt and events");}
        finally {Directory.Delete(file+".tmp");}
        Check(service.UpgradeTreasureSlot(element,0,false,"treasure:paid-retry",Now).IsApplied,"same failed operation retries after storage recovery");
        Check(profile.Save.Player.CharacterProperties.TreasureSlots.AwakeAutoMergeEnable,"upgrade reload preserves another owner's latest committed preference");
        // Search actual selected rows and actual common growth for a valid blessing environment.
        JToken chosen=null;
        foreach(var row in raw["Limits"].Where(r=>(int)r["TreasureBlessLevel"]>0)) {
            var candidate=JsonUtility.FromJson<OfflinePlayerProgress>(JsonUtility.ToJson(profile.Save.Player));
            candidate.SubRank=(int)row["SubRank"];candidate.SeasonRank=(int)((S.SubRank)candidate.SubRank).ToRank();candidate.HasActiveSeasonRecord=true;
            candidate.CharacterProperties.TreasurePlaneRank=candidate.SeasonRank;candidate.Experience=100000000000L;candidate.BlessingExperience=100000000000L;
            var env=environment(candidate);var t=levels.CreateTables();int cap=t.Limits.GetTreasureBaseLevelLimit((S.SubRank)candidate.SubRank);
            var limit=t.Limits.GetLevelLimitInfo((S.SubRank)candidate.SubRank,env.BaseLevel,env.PlayerBlessLevel);
            if(limit!=null&&limit.TreasureLevel+limit.TreasureBlessLevel>cap) {chosen=row;break;}
        }
        Check(chosen!=null,"actual common projection provides a selected blessing limit row");
        int subrank=(int)chosen["SubRank"],rank=(int)((S.SubRank)subrank).ToRank();
        int baseCap=levels.CreateTables().Limits.GetTreasureBaseLevelLimit((S.SubRank)subrank);
        var trained=Local();
        foreach(var e in trained.Settings.TreasureSlotElementTypes) {
            var g=trained.GetTreasureElementSlotData(e);for(int i=0;i<slots.MaxSlotCount;i++) {g.InsertTreasureToSlot((int)e==element&&i==0?id:0,i);g.TreasureSlots[i].BaseLevel=baseCap;}g.UpdateTreasureSlotsShareLevel();
        }
        Check(profile.ApplyCharacterMutation(p=>{
            p.SubRank=subrank;p.SeasonRank=rank;p.HasActiveSeasonRecord=true;p.CharacterProperties.TreasurePlaneRank=rank;
            p.Experience=100000000000L;p.BlessingExperience=100000000000L;p.CharacterProperties.TreasureSlots=OfflineTreasureSlotService.Capture(trained);
        }).IsApplied,"persist all twenty trained slots and genuine player season record");
        notifications.Clear();var blessReady=service.InspectTreasureSlotLevelUp(element,0,false,Now);
        Check(blessReady.Status==ProgressionResultStatus.Ready,"actual blessing qualification with shared growth projection: "+blessReady.Status+"/"+blessReady.ErrorCode);
        Check(service.UpgradeTreasureSlot(element,0,false,"treasure:paid-bless",Now).IsApplied,"actual blessing transaction");
        var upgraded=profile.Save.Player.CharacterProperties.TreasureSlots;
        Check(upgraded.BlessRecords.Count==1&&upgraded.BlessRecords[0].Elements.Count==5&&upgraded.BlessRecords[0].Elements.All(e=>e.Slots.Count==4),"original Init writes complete seasonal subrecord");
        Check(upgraded.Elements.Single(e=>e.ElementType==element).Slots.Single(s=>s.Index==0).BlessLevel==1&&upgraded.BlessRecords[0].Elements.Single(e=>e.ElementType==element).Slots.Single(s=>s.Index==0).Level==1,"cache and seasonal blessing record commit together");
        Check(State(notifications)==State(new[]{"all","season","season","level","refresh","share"}),"season synchronization precedes blessing and slot notifications");
        before=File.ReadAllBytes(file);Check(service.SynchronizeTreasureSlotSeason("treasure:sync-noop",Now).Status==ProgressionResultStatus.Unchanged&&File.ReadAllBytes(file).SequenceEqual(before),"already synchronized season is a durable no-op");
        int seasonId=upgraded.SeasonId;
        Check(profile.ApplyCharacterMutation(p=>{var x=p.CharacterProperties.TreasureSlots;x.SeasonId=0;foreach(var g in x.Elements)foreach(var s in g.Slots)s.BlessLevel=0;}).IsApplied,"clear only cache to simulate season reload");
        notifications.Clear();Check(service.SynchronizeTreasureSlotSeason("treasure:restore",Now).IsApplied,"explicit season cache restoration commits");
        Check(service.Snapshot().GetTreasureSlotData((S.ElementType)element,0).BlessLevel==1&&State(notifications)==State(new[]{"all","season"}),"restore source history without paying again");
        int otherRank=(int)raw["Seasons"].First(r=>(int)r["Rank"]!=rank&&(int)r["Rank"]<11)["Rank"];
        int otherSub=(int)Enum.GetValues(typeof(S.SubRank)).Cast<S.SubRank>().First(r=>(int)r.ToRank()==otherRank);
        Check(profile.ApplyCharacterMutation(p=>{p.SubRank=otherSub;p.SeasonRank=otherRank;p.CharacterProperties.TreasurePlaneRank=otherRank;}).IsApplied,"select another genuine isolated player season");
        Check(service.SynchronizeTreasureSlotSeason("treasure:other-season",Now).IsApplied&&profile.Save.Player.CharacterProperties.TreasureSlots.BlessRecords.Count==2
            &&service.Snapshot().GetTreasureSlotData((S.ElementType)element,0).BlessLevel==0,"new seasonal cache does not overwrite previous history");
        Check(profile.ApplyCharacterMutation(p=>{p.SubRank=subrank;p.SeasonRank=rank;p.CharacterProperties.TreasurePlaneRank=rank;}).IsApplied,"restore original isolated season");
        Check(service.SynchronizeTreasureSlotSeason("treasure:original-season",Now).IsApplied&&profile.Save.Player.CharacterProperties.TreasureSlots.BlessRecords.Count==2
            &&service.Snapshot().GetTreasureSlotData((S.ElementType)element,0).BlessLevel==1,"returning season restores its own trained blessing");
        // Cache-only old saves have a separate migration marker and must retain their explicit season.
        Check(profile.ApplyCharacterMutation(p=>{var x=p.CharacterProperties.TreasureSlots;x.BlessRecordFormat=0;x.BlessRecords.Clear();}).IsApplied,"seed old cached-only blessing save");
        Check(service.SynchronizeTreasureSlotSeason("treasure:legacy-history",Now).IsApplied&&profile.Save.Player.CharacterProperties.TreasureSlots.BlessRecords.Single().SeasonId==seasonId,"migrate explicit cached blessing into matching historical season");
        Check(profile.ApplyCharacterMutation(p=>{var x=p.CharacterProperties.TreasureSlots;x.BlessRecordFormat=0;x.BlessRecords.Clear();x.SeasonId=0;}).IsApplied,"seed ambiguous cached-only blessing save");
        before=File.ReadAllBytes(file);Check(service.SynchronizeTreasureSlotSeason("treasure:ambiguous",Now).Status==ProgressionResultStatus.InvalidState&&File.ReadAllBytes(file).SequenceEqual(before),"unknown cached blessing season rejected without inventing history");
        Check(profile.ApplyCharacterMutation(p=>{p.CharacterProperties.TreasureSlots.SeasonId=seasonId;p.HasActiveSeasonRecord=false;p.CharacterProperties.TreasureSlots.Elements.ForEach(g=>g.Slots.ForEach(s=>s.BlessLevel=0));}).IsApplied,"seed absent player season record");
        before=File.ReadAllBytes(file);notifications.Clear();
        Check(!service.UpgradeTreasureSlot(element,0,false,"treasure:paid-missing-record",Now).IsApplied&&File.ReadAllBytes(file).SequenceEqual(before)&&notifications.Count==0,"absent real season record cannot consume fees for a no-op upgrade");
        Check(service.UpgradeTreasureSlot(element,-1,false,"treasure:invalid",Now).Status==ProgressionResultStatus.InvalidState,"invalid envelope stays a local error");
        CheckMergeTransactions(profile,service,graph,element,id);
    }
    static O.ICondition OriginalCondition(JToken row)
    {
        switch((string)row["Type"]) {
            case "HasItemCondition":return new O.HasItemCondition{ClassId=(int)row["ClassId"],Count=(int)row["Count"],Op=(string)row["Op"]};
            case "HasItemTypeCondition":return new O.HasItemTypeCondition{ItemType=(int)row["ItemType"],Count=(int)row["Count"]};
            case "LevelCondition":return new O.LevelCondition{Level=(int)row["Level"],Op=(string)row["Op"]};
            case "CompositeOrCondition":return new O.CompositeOrCondition{Children=row["Children"].Select(OriginalCondition).ToList()};
            default:throw new Exception("Unbound original source condition");
        }
    }
    static void CheckSourceGates(OfflineTreasureSlotService service)
    {
        foreach(var product in raw["MergeSourceProducts"]) foreach(int baseLevel in new[]{29,30}) foreach(int owned in new[]{0,1}) foreach(int furnace in new[]{0,1}) {
            var p=service.CurrentLevelSnapshot(Now);var q=Original();p.BaseLevel=q.BaseLevel=baseLevel;
            p.ReadItemAmount=q.ReadItemAmount=id=>id==4015?furnace:0;
            p.ItemManager.ItemTypeDict.Clear();p.ItemManager.ItemTypeDict.Add(305,new List<S.SlotItem>{new S.SlotItem{Count=owned}});
            q.ItemManager.ItemTypeDict.Add(305,new List<O.SlotItem>{new O.SlotItem{Count=owned}});
            var hub=new O.ICondition.DataHub(q);bool expected=OriginalCondition(raw["MergeSourceBagualu"]).IsMet(hub)
                &&(product["Condition"].Type==JTokenType.Null||OriginalCondition(product["Condition"]).IsMet(hub));
            Check(p.MergeSourceAvailable((int)product["ItemId"])==expected,"actual catalog source gate follows furnace and exact owned-type/base-level OR graph");
            Check(!p.MergeSourceAvailable(41400),"no fallback recipe source for an unconfigured material");
        }
    }
    static void CheckMergeTransactions(OfflineMainlineProgressSession profile,OfflineTreasureSlotService service,S.Player graph,int element,int id)
    {
        var selected=raw["LevelCosts"].First(row=>row["Materials"]["41401"]!=null);int target=(int)selected["Level"];
        // Silver has sufficient base capacity for the selected material tier.
        int sub=(int)raw["Limits"].First(row=>levels.CreateTables().Limits.GetTreasureBaseLevelLimit((S.SubRank)(int)row["SubRank"])>=target)["SubRank"];
        Check(profile.ApplyCharacterMutation(p=>{
            p.SubRank=sub;p.HasActiveSeasonRecord=false;p.Experience=0;p.BlessingExperience=0;
            p.CharacterProperties.TreasurePlaneRank=(int)((S.SubRank)sub).ToRank();
            var g=graph.GetTreasureElementSlotData((S.ElementType)element);g.TreasureSlots[0].BaseLevel=target-1;g.UpdateTreasureSlotsShareLevel();
            p.CharacterProperties.TreasureSlots=OfflineTreasureSlotService.Capture(graph);
            p.Inventory.RemoveAll(item=>item.ItemId==41401||item.ItemId==41402||item.ItemId==4015);
            p.Inventory.Single(item=>item.ItemId==41400).Count=10000000;
        }).IsApplied,"seed real bottom-tier automatic-merge materials");
        Check(service.InspectTreasureSlotLevelUp(element,0,true,Now).ErrorCode==1188,"automatic merge refuses locked Bagualu source");
        Check(profile.ApplyCharacterMutation(p=>p.Inventory.Add(new OfflineItemStack{ItemId=4015,Count=1})).IsApplied,"unlock actual recipe source");
        var inspect=service.InspectTreasureSlotLevelUp(element,0,true,Now);
        Check(inspect.Status==ProgressionResultStatus.Ready&&inspect.Costs.ContainsKey(41400)&&!inspect.Costs.ContainsKey(41401),"owned item type opens exact recipe and original recursive fee expands");
        Check(service.InspectTreasureSlotLevelUp(element,0,false,Now).ErrorCode==1188,"disabled automatic merge requires direct material");
        var amounts=inspect.Costs.ToDictionary(row=>row.Key,row=>profile.InventoryCount(row.Key));
        Check(service.UpgradeTreasureSlot(element,0,true,"treasure:paid-merge",Now).IsApplied,"actual automatic merged payment commits");
        foreach(var cost in inspect.Costs)Check(profile.InventoryCount(cost.Key)==amounts[cost.Key]-cost.Value,"expanded fee exact inventory conservation");
        Check(profile.InventoryCount(41401)==0&&profile.InventoryCount(4015)==1&&profile.InventoryCount(id)==1,"automatic fees create no surplus product and preserve gate/equipment");
    }
}
