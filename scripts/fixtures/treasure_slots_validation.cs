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
using Zjcs.Offline.HomePlayerHealthUI;
using S = Zjcs.Official.TreasureSlots;
using O = TreasureSlotOracle;

public static class TreasureSlotsValidation
{
    static string Work => Environment.GetEnvironmentVariable("ZJCS_TREASURE_SLOTS_OUTPUT");
    static int checks, scenarios;
    static JObject raw;
    static Dictionary<int,int> elements;
    static OfflineTreasureSlotCatalog catalog;
    static void Check(bool value,string name) { checks++; if(!value) throw new Exception("Treasure slots: " + name); }
    public static void Run()
    {
        try {
            raw=JObject.Parse(File.ReadAllText(Path.Combine(Work,"OriginalTreasureSlots.json")));
            elements=raw["Items"].ToDictionary(row=>(int)row["ClassId"],row=>(int)row["ElementType"]);
            catalog=OfflineTreasureSlotCatalog.LoadDefault();
            foreach(var row in elements) Check(catalog.TryGetElement(row.Key,out int element)&&element==row.Value,"actual selected item element mapping");
            CheckKernel(); CheckSaved();
            File.WriteAllText(Path.Combine(Work,"result.json"),JsonConvert.SerializeObject(new{success=true,checks,scenarios,treasures=elements.Count}));
            Debug.Log("TREASURE_SLOTS_PASSED: " + checks + " checks / " + scenarios + " original-state scenarios"); EditorApplication.Exit(0);
        } catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static S.Player Local() => new S.Player(catalog.CreateSettings(),id=>elements.TryGetValue(id,out int element)
        ?new S.SlotItem{Info=new S.SlotItemInfo{ElementType=(S.ElementType)element}}:null,_=>{});
    static O.Player Original()
    {
        O.Parsers.GameSettings=new O.TreasureSlotSettings{TreasureMaxSlotCount=catalog.MaxSlotCount,TreasurePlanMaxCount=catalog.MaxPlanCount,
            TreasureSlotElementTypes=raw["TreasureSlotElementTypes"].Select(value=>(O.ElementType)(int)value).ToList()};
        return new O.Player(O.Parsers.GameSettings,id=>elements.TryGetValue(id,out int element)
            ?new O.SlotItem{Info=new O.SlotItemInfo{ElementType=(O.ElementType)element}}:null);
    }
    static string State(object value)=>JToken.FromObject(value).ToString(Formatting.None);
    static object LocalState(S.Player p)=>new{p.ActivePlanIndex,p.TreasureSlotData.SeasonId,p.TreasureSlotData.UpgradeUseCommonMaterailEnable,p.TreasureSlotData.AwakeAutoMergeEnable,Groups=p.TreasureSlotData.TreasureSlotDataDict.Select(e=>new{
        Element=(int)e.Key,e.Value.ShareLevel,e.Value.BaseShareLevel,Slots=e.Value.TreasureSlots.Select(s=>new{s.Key,s.Value.TreasureClassId,s.Value.BaseLevel,s.Value.BlessLevel})}),
        Plans=p.Plans.Select(plan=>new{plan.Key,Groups=plan.Value.Data.TreasureSlots.Select(e=>new{Element=(int)e.Key,Slots=e.Value})})};
    static object OriginalState(O.Player p)=>new{p.ActivePlanIndex,p.TreasureSlotData.SeasonId,p.TreasureSlotData.UpgradeUseCommonMaterailEnable,p.TreasureSlotData.AwakeAutoMergeEnable,Groups=p.TreasureSlotData.TreasureSlotDataDict.Select(e=>new{
        Element=(int)e.Key,e.Value.ShareLevel,e.Value.BaseShareLevel,Slots=e.Value.TreasureSlots.Select(s=>new{s.Key,s.Value.TreasureClassId,s.Value.BaseLevel,s.Value.BlessLevel})}),
        Plans=p.Plans.Select(plan=>new{plan.Key,Groups=plan.Value.Data.TreasureSlots.Select(e=>new{Element=(int)e.Key,Slots=e.Value})})};
    static void Compare(S.Player local,O.Player original)
    {
        scenarios++;
        foreach(int id in elements.Keys.Concat(new[]{-1,0})) {
            Check(local.GetTreasureInSlotIndex(id)==original.GetTreasureInSlotIndex(id),"original occupied and empty-slot lookup order");
            Check(local.GetTreasureFinalLevel(id)==original.GetTreasureFinalLevel(id)&&local.GetTreasureBaseLevel(id)==original.GetTreasureBaseLevel(id),"original slot and shared final/base levels");
            Check(local.GetSlotTreasureBaseLevel(id)==original.GetSlotTreasureBaseLevel(id)&&local.GetSlotTreasureLevel(id)==original.GetSlotTreasureLevel(id),"base-only slot queries");
            Check(local.IsTreasureInSlot(id)==original.IsTreasureInSlot(id),"slot presence semantics include original zero-id behavior");
        }
        foreach(int e in raw["TreasureSlotElementTypes"].Select(v=>(int)v)) {
            Check(local.GetCanSetupTreasureSlotIndex((S.ElementType)e)==original.GetCanSetupTreasureSlotIndex((O.ElementType)e),"first absent slot and count-based cap");
            for(int i=-1;i<=catalog.MaxSlotCount;i++) Check(local.GetTreasureSlotFinalLevel((S.ElementType)e,i)==original.GetTreasureSlotFinalLevel((O.ElementType)e,i),"missing-slot level fallback");
        }
        Check(State(LocalState(local))==State(OriginalState(original)),"entire ordered slot/plan graph agrees with untouched original methods");
    }
    static void CheckKernel()
    {
        foreach(int element in raw["TreasureSlotElementTypes"].Select(v=>(int)v))
        for(int count=0;count<=catalog.MaxSlotCount;count++) {
            var local=Local();var original=Original();int le=0,oe=0;
            local.OnSetupTreasureSlot+=()=>le++;original.OnSetupTreasureSlot+=()=>oe++;
            var ids=elements.Where(row=>row.Value==element).Select(row=>row.Key).Take(5).ToArray();
            var lg=local.GetTreasureElementSlotData((S.ElementType)element);var og=original.GetTreasureElementSlotData((O.ElementType)element);
            lg.ShareLevel=og.ShareLevel=13;lg.BaseShareLevel=og.BaseShareLevel=7;
            for(int i=0;i<count;i++) {
                lg.InsertTreasureToSlot(i==count-1?0:ids[i],i);og.InsertTreasureToSlot(i==count-1?0:ids[i],i);
                lg.TreasureSlots[i].BaseLevel=og.TreasureSlots[i].BaseLevel=2+i;
                lg.TreasureSlots[i].BlessLevel=og.TreasureSlots[i].BlessLevel=1+i;
            }
            lg.UpdateTreasureSlotsShareLevel();og.UpdateTreasureSlotsShareLevel();Compare(local,original);
            foreach(var command in new[]{(ids[0],0),(ids[1],1),(ids[0],1),(ids[2],1),(ids[2],2),(ids[2],2)}) {
                int id=command.Item1,index=command.Item2;
                Check(S.TreasureHelper.CanSetupTreasureSlot(local,id,index)==O.TreasureHelper.CanSetupTreasureSlot(original,id,index),"original same-slot cancellation");
                Check(local.SetupTreasureInSlot(id,(S.ElementType)element,local.GetTreasureInSlotIndex(id),index)
                    ==original.SetupTreasureInSlot(id,(O.ElementType)element,original.GetTreasureInSlotIndex(id),index),"original setup return value");
                local.ActivePlanIndex=original.ActivePlanIndex=3;
                S.PlayerTreasurePlanExt.WriteWornTreasureElementToActivePlan(local,(S.ElementType)element);
                O.PlayerTreasurePlanExt.WriteWornTreasureElementToActivePlan(original,(O.ElementType)element);
                Check(le==oe,"original setup notification count");Compare(local,original);
            }
            Check(local.RemoveTreasureInSlot((S.ElementType)element,1)==original.RemoveTreasureInSlot((O.ElementType)element,1),"removal keeps slot instead of deleting its level");
            local.TreasureSlotBaseLevelUp((S.ElementType)element,0,2);original.TreasureSlotBaseLevelUp((O.ElementType)element,0,2);
            Compare(local,original);
            local.TreasureSlotData.ClearAllBlessLevel();original.TreasureSlotData.ClearAllBlessLevel();Compare(local,original);
            Check(local.GetTreasureElementSlotData(S.ElementType.None)==null&&original.GetTreasureElementSlotData(O.ElementType.None)==null,"unsupported element remains absent");
            string before=State(LocalState(local));int errors=O.Log.Errors;
            lg.InsertTreasureToSlot(ids[0],catalog.MaxSlotCount);og.InsertTreasureToSlot(ids[0],catalog.MaxSlotCount);
            Check(O.Log.Errors==errors+1&&State(LocalState(local))==before,"overflow insert logs and preserves original graph");
            // The low-level official method permits negative keys. Public save operations reject them.
            lg.InsertTreasureToSlot(ids[3],-1);og.InsertTreasureToSlot(ids[3],-1);Compare(local,original);
        }
    }
    static void CheckSaved()
    {
        string root=Path.Combine(Work,"isolated-saves");var creation=CharacterCreationOfflineService.OpenDefault(root);
        string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="槽位迁移验证";
        Check(creation.TryCreateCharacter(slot,draft,out var error),"real isolated character: "+error);
        Check(creation.TrySetInitialWeapon(slot,CharacterCreationOfflineService.SwordInitialWeaponType,out error),"real weapon selection");
        var slots=new ProfileSlotCatalog(root);var profile=OfflineMainlineProgressSession.OpenCharacter(slots,slot);
        var service=new OfflineTreasureSlotService(profile,catalog);string file=Path.Combine(slots.GetSlotDirectory(slot),"city-save.json");
        var oldPayload=JObject.Parse(File.ReadAllText(file));((JObject)oldPayload["Player"]["CharacterProperties"]).Remove("TreasureSlots");
        File.WriteAllText(file,oldPayload.ToString());
        var oldProfile=OfflineMainlineProgressSession.OpenCharacter(slots,slot);
        Check(oldProfile.ReadStatus==SaveReadStatus.Primary&&oldProfile.Save.Player.CharacterProperties.TreasureSlots.Format==0,
            "actual old JSON without the new field opens as primary save without backup recovery");
        profile.Reload();
        int element=elements.First().Value;var ids=elements.Where(row=>row.Value==element).Select(row=>row.Key).Take(5).ToArray();
        var graph=Local();var group=graph.GetTreasureElementSlotData((S.ElementType)element);
        for(int i=0;i<catalog.MaxSlotCount;i++) {group.InsertTreasureToSlot(i<2?ids[i]:0,i);group.TreasureSlots[i].BaseLevel=3+i;group.TreasureSlots[i].BlessLevel=i;}
        group.UpdateTreasureSlotsShareLevel();graph.ActivePlanIndex=3;
        graph.TreasureSlotData.SeasonId=3;graph.TreasureSlotData.UpgradeUseCommonMaterailEnable=true;graph.TreasureSlotData.AwakeAutoMergeEnable=true;
        ((S.PlayerBattlePlanTreasurePlanContentWrap)graph.GetOrCreatePlanContent(S.BattlePlanType.Treasure,0)).Data.TreasureSlots.Add((S.ElementType)element,new Dictionary<int,int>{{0,ids[0]}});
        Check(profile.ApplyCharacterMutation(p=>{
            p.Inventory=ids.Select(id=>new OfflineItemStack{ItemId=id,Count=1}).ToList();
            p.CharacterProperties.Treasures=ids.Select(id=>new OfflineCharacterTreasure{ClassId=id,Active=true,HitCount=1}).ToList();
            p.CharacterProperties.TreasureSlots=OfflineTreasureSlotService.Capture(graph);
        }).IsApplied,"persist real trained slots including empty ones and inactive plan");
        int notified=0;service.OnSetupTreasureSlot+=()=>{notified++;Check(File.ReadAllText(file).Contains("\"treasure:swap\""),"notification follows durable receipt");};
        string inventory=JsonConvert.SerializeObject(profile.Save.Player.Inventory);
        string inactivePlan=State(profile.Save.Player.CharacterProperties.TreasureSlots.Plans.Single(p=>p.Index==0));
        Check(service.SetupTreasureSlot(ids[0],1,"treasure:swap").IsApplied,"actual swap commits");
        Check(notified==1,"single committed setup event");
        var saved=service.Snapshot();var sg=saved.GetTreasureElementSlotData((S.ElementType)element);
        Check(sg.TreasureSlots[0].TreasureClassId==ids[1]&&sg.TreasureSlots[1].TreasureClassId==ids[0]
            &&sg.TreasureSlots[0].BaseLevel==3&&sg.TreasureSlots[1].BaseLevel==4&&sg.TreasureSlots[1].BlessLevel==1,"swap retains each physical slot's own training");
        Check(saved.Plans[3].Data.TreasureSlots[(S.ElementType)element][1]==ids[0]
            &&State(profile.Save.Player.CharacterProperties.TreasureSlots.Plans.Single(p=>p.Index==0))==inactivePlan,"current plan written without overwriting other plans");
        Check(inventory==JsonConvert.SerializeObject(profile.Save.Player.Inventory),"replacement has no inventory fee or grants");
        CheckProperties(profile.Save.Player);
        byte[] unchanged=File.ReadAllBytes(file);
        Check(service.SetupTreasureSlot(ids[0],1,"treasure:swap").Status==ProgressionResultStatus.Unchanged
            &&service.SetupTreasureSlot(ids[0],1,"treasure:same-slot").Status==ProgressionResultStatus.Unchanged
            &&File.ReadAllBytes(file).SequenceEqual(unchanged)&&notified==1,"replay and same-slot cancellation do not save or notify");
        var cold=OfflineMainlineProgressSession.OpenCharacter(slots,slot);var coldService=new OfflineTreasureSlotService(cold,catalog);
        Check(State(LocalState(coldService.Snapshot()))==State(LocalState(saved)),"real cold profile restores full slots, preferences and plans");
        var detached=service.Snapshot();detached.GetTreasureElementSlotData((S.ElementType)element).TreasureSlots[0].BaseLevel=999;
        Check(service.Snapshot().GetTreasureElementSlotData((S.ElementType)element).TreasureSlots[0].BaseLevel==3,"detached official projection cannot mutate the profile");
        Check(service.SetupTreasureSlot(ids[0],-1,"treasure:bad-index").Status==ProgressionResultStatus.InvalidState
            &&service.SetupTreasureSlot(0,0,"treasure:bad-id").Status==ProgressionResultStatus.InvalidState,"invalid envelopes reject without new official error codes");
        Directory.CreateDirectory(file+".tmp");
        try {Check(service.SetupTreasureSlot(ids[2],2,"treasure:blocked-write").Status==ProgressionResultStatus.InvalidState
            &&File.ReadAllBytes(file).SequenceEqual(unchanged)&&notified==1,"real save write failure rolls back slot, plan, receipt and notification");}
        finally {Directory.Delete(file+".tmp");}
        Check(service.SetupTreasureSlot(ids[2],2,"treasure:blocked-write").IsApplied&&notified==2,"same failed request can retry");
        Check(service.Snapshot().GetTreasureElementSlotData((S.ElementType)element).TreasureSlots[2].BaseLevel==5,"newly equipped item inherits trained empty-slot level");
        Check(profile.ApplyCharacterMutation(p=>{
            p.CharacterProperties.TreasureSlots=new OfflineTreasureSlotProgress();p.CharacterProperties.TreasureElementLevels=new List<OfflineTreasureElementLevel>{new OfflineTreasureElementLevel{ElementType=element,BaseLevel=2,Level=3}};
            foreach(var t in p.CharacterProperties.Treasures){t.SlotIndex=-1;t.BaseLevel=t.Level=1;}
            var a=p.CharacterProperties.Treasures[0];a.SlotIndex=0;a.BaseLevel=5;a.Level=9;
        }).IsApplied,"persist genuine old save shape");
        Check(service.SetupTreasureSlot(ids[1],1,"treasure:legacy-import").IsApplied,"legacy slot and share records import in the same transaction");
        profile.Reload();var imported=service.Snapshot();
        Check(imported.GetTreasureBaseLevel(ids[0])==5&&imported.GetTreasureFinalLevel(ids[0])==9
            &&imported.GetTreasureFinalLevel(ids[4])==3&&imported.GetTreasureBaseLevel(ids[4])==2,"legacy explicit slot and shared levels survive cold restore");
        Check(profile.Save.Player.CharacterProperties.TreasureElementLevels.Count==0&&profile.Save.Player.CharacterProperties.Treasures.All(t=>t.SlotIndex==-1&&t.BaseLevel==1&&t.Level==1),"old fields stop owning live slot state after import");
        CheckProperties(profile.Save.Player);
        Check(profile.ApplyCharacterMutation(p=>{p.CharacterProperties.TreasureSlots=new OfflineTreasureSlotProgress();p.CharacterProperties.Treasures[0].SlotIndex=0;p.CharacterProperties.Treasures[1].SlotIndex=0;}).IsApplied,"persist conflicting legacy records for rejection test");
        unchanged=File.ReadAllBytes(file);
        Check(service.SetupTreasureSlot(ids[2],2,"treasure:conflict").Status==ProgressionResultStatus.InvalidState&&File.ReadAllBytes(file).SequenceEqual(unchanged),"ambiguous legacy occupancy is never guessed or overwritten");
    }
    static void CheckProperties(OfflinePlayerProgress player)
    {
        var properties=OfflineHomeNoBlessConfiguration.LoadDefault().CreatePropertyCatalog(OfficialCharacterAssetCatalog.LoadDefault().Load());
        var source=new OfflineCharacterConfiguredPropSource(properties,CharacterPropHandleType.Treasure);
        var legacy=JsonUtility.FromJson<OfflinePlayerProgress>(JsonUtility.ToJson(player));var data=legacy.CharacterProperties.TreasureSlots;
        legacy.CharacterProperties.TreasureElementLevels=data.Elements.Select(e=>new OfflineTreasureElementLevel{ElementType=e.ElementType,BaseLevel=e.BaseShareLevel,Level=e.ShareLevel}).ToList();
        foreach(var element in data.Elements) foreach(var slot in element.Slots) {
            var item=legacy.CharacterProperties.Treasures.FirstOrDefault(t=>t.ClassId==slot.TreasureClassId);
            if(item!=null){item.SlotIndex=slot.Index;item.BaseLevel=slot.BaseLevel;item.Level=slot.BaseLevel+slot.BlessLevel;}
        }
        legacy.CharacterProperties.TreasureSlots=new OfflineTreasureSlotProgress();
        string unchanged=JsonUtility.ToJson(player);
        foreach(int rank in new[]{properties.RankOfSubRank(player.SubRank),999}) {
            var context=new CharacterPropScoreContext{Inputs=new CharacterPropCalculationInputs(rank,0,new Dictionary<int,long>())};
            var actual=source.CalculateProps(player,null,0,context);var expected=source.CalculateProps(legacy,null,0,context);
            Check(actual.Count>0&&actual.Count==expected.Count&&actual.All(row=>expected.TryGetValue(row.Key,out long value)&&value==row.Value),"actual character properties use canonical slot levels, sharing and world blessing gate");
        }
        Check(JsonUtility.ToJson(player)==unchanged,"full property query never writes slot state");
    }
}
