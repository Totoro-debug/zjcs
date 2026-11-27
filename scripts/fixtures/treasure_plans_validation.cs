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
using S=Zjcs.Official.TreasureSlots;
using O=TreasureSlotOracle;

public static class TreasurePlansValidation
{
    static string Work=>Environment.GetEnvironmentVariable("ZJCS_TREASURE_PLANS_OUTPUT");
    static int checks,scenarios;
    static OfflineTreasureSlotCatalog catalog;
    static Dictionary<int,int> elements;
    static void Check(bool value,string name) {checks++;if(!value)throw new Exception("Treasure plans: "+name);}
    static string State(object value)=>JsonConvert.SerializeObject(value);
    public static void Run()
    {
        try {
            var raw=JObject.Parse(File.ReadAllText(Path.Combine(Work,"OriginalTreasureSlots.json")));
            elements=raw["Items"].ToDictionary(r=>(int)r["ClassId"],r=>(int)r["ElementType"]);catalog=OfflineTreasureSlotCatalog.LoadDefault();
            Kernel();Signals();Saved();
            File.WriteAllText(Path.Combine(Work,"result.json"),State(new{success=true,checks,scenarios}));
            Debug.Log("TREASURE_PLANS_PASSED: "+checks+" checks / "+scenarios+" original scenarios");EditorApplication.Exit(0);
        } catch(Exception error) {Debug.LogException(error);EditorApplication.Exit(1);}
    }
    static S.Player Local()=>new S.Player(catalog.CreateSettings(),id=>elements.TryGetValue(id,out int e)?new S.SlotItem{Id=id,Count=1,Info=new S.SlotItemInfo{ElementType=(S.ElementType)e}}:null,_=>{});
    static O.Player Original()
    {
        O.Parsers.GameSettings=new O.TreasureSlotSettings{TreasureMaxSlotCount=catalog.MaxSlotCount,TreasurePlanMaxCount=catalog.MaxPlanCount,
            TreasureSlotElementTypes=catalog.CreateSettings().TreasureSlotElementTypes.Select(e=>(O.ElementType)(int)e).ToList()};
        return new O.Player(O.Parsers.GameSettings,id=>elements.TryGetValue(id,out int e)?new O.SlotItem{Id=id,Count=1,Info=new O.SlotItemInfo{ElementType=(O.ElementType)e}}:null);
    }
    static object Graph(S.Player p)=>new{p.ActivePlanIndex,Groups=p.TreasureSlotData.TreasureSlotDataDict.Select(e=>new{Element=(int)e.Key,e.Value.ShareLevel,e.Value.BaseShareLevel,
        Slots=e.Value.TreasureSlots.Select(s=>new{s.Key,s.Value.TreasureClassId,s.Value.BaseLevel,s.Value.BlessLevel})}),Plans=p.Plans.Select(plan=>new{plan.Key,Elements=plan.Value.Data.TreasureSlots.Select(e=>new{Element=(int)e.Key,Slots=e.Value})})};
    static object Graph(O.Player p)=>new{p.ActivePlanIndex,Groups=p.TreasureSlotData.TreasureSlotDataDict.Select(e=>new{Element=(int)e.Key,e.Value.ShareLevel,e.Value.BaseShareLevel,
        Slots=e.Value.TreasureSlots.Select(s=>new{s.Key,s.Value.TreasureClassId,s.Value.BaseLevel,s.Value.BlessLevel})}),Plans=p.Plans.Select(plan=>new{plan.Key,Elements=plan.Value.Data.TreasureSlots.Select(e=>new{Element=(int)e.Key,Slots=e.Value})})};
    static void Kernel()
    {
        for(int scenario=0;scenario<80;scenario++) {
            var p=Local();var q=Original();var pe=new List<string>();var qe=new List<string>();
            p.OnSetupTreasureSlot+=()=>pe.Add("setup");q.OnSetupTreasureSlot+=()=>qe.Add("setup");
            p.OnBattlePlanContentCleared.Subscribe((type,index)=>pe.Add("clear:"+index));q.OnBattlePlanContentCleared.Subscribe((type,index)=>qe.Add("clear:"+index));
            foreach(int e in elements.Values.Distinct()) {
                var ids=elements.Where(r=>r.Value==e).Select(r=>r.Key).ToArray();
                var pg=p.GetTreasureElementSlotData((S.ElementType)e);var qg=q.GetTreasureElementSlotData((O.ElementType)e);
                for(int slot=0;slot<(scenario%5);slot++) {
                    int id=slot==scenario%4?0:ids[(scenario+slot)%ids.Length];pg.InsertTreasureToSlot(id,slot);qg.InsertTreasureToSlot(id,slot);
                    pg.TreasureSlots[slot].BaseLevel=qg.TreasureSlots[slot].BaseLevel=slot+2;pg.TreasureSlots[slot].BlessLevel=qg.TreasureSlots[slot].BlessLevel=slot;
                }
                pg.UpdateTreasureSlotsShareLevel();qg.UpdateTreasureSlotsShareLevel();
                for(int index=0;index<catalog.MaxPlanCount;index++) {
                    var pc=(S.PlayerBattlePlanTreasurePlanContentWrap)p.GetOrCreatePlanContent(S.BattlePlanType.Treasure,index);
                    var qc=(O.PlayerBattlePlanTreasurePlanContentWrap)q.GetOrCreatePlanContent(O.BattlePlanType.Treasure,index);
                    if((scenario+e+index)%3==0)continue;
                    var target=new Dictionary<int,int>();for(int slot=0;slot<index%5;slot++)target.Add(slot,slot==scenario%4?0:ids[(scenario+index+slot)%ids.Length]);
                    pc.Data.TreasureSlots.Add((S.ElementType)e,new Dictionary<int,int>(target));qc.Data.TreasureSlots.Add((O.ElementType)e,new Dictionary<int,int>(target));
                }
                Check(State(S.PlayerTreasurePlanExt.ReadWornTreasureSlots(p,(S.ElementType)e))==State(O.PlayerTreasurePlanExt.ReadWornTreasureSlots(q,(O.ElementType)e)),"read worn filters zero IDs in source order");
            }
            for(int index=-1;index<=catalog.MaxPlanCount;index++) {
                bool a=S.PlayerTreasurePlanExt.TryGetTreasurePlanSlots(p,index,out var ps),b=O.PlayerTreasurePlanExt.TryGetTreasurePlanSlots(q,index,out var qs);
                Check(a==b&&State(ps)==State(qs),"plan lookup and parsing match source");
                Check(S.PlayerTreasurePlanExt.HasTreasurePlanSlot(ps)==O.PlayerTreasurePlanExt.HasTreasurePlanSlot(qs),"nonempty plan predicate");
                if(a&&ps.Count>0) {
                    string before=State(p.Plans[index].Data);ps.Clear();
                    Check(State(p.Plans[index].Data)==before,"parsed plan result is detached");
                }
                foreach(int ec in new[]{1,0}) {
                    S.TreasurePlanCallbacks.OperPlayerApplyBattlePlanCallback(p,new S.PlayerApplyBattlePlanRequest{PlanType=S.BattlePlanType.Treasure,PlanIndex=index},ec);
                    O.TreasurePlanCallbacks.OperPlayerApplyBattlePlanCallback(q,new O.PlayerApplyBattlePlanRequest{PlanType=O.BattlePlanType.Treasure,PlanIndex=index},ec);
                    Check(State(Graph(p))==State(Graph(q))&&State(pe)==State(qe),"original success/error plan replay, slot training and event order");scenarios++;
                }
            }
            int removed=p.Plans.Values.SelectMany(plan=>plan.Data.TreasureSlots.Values).SelectMany(e=>e.Values).First(id=>id!=0);
            int removalCount=S.PlayerTreasurePlanExt.RemoveTreasureFromAllPlans(p,removed);
            Check(removalCount>0&&removalCount==O.PlayerTreasurePlanExt.RemoveTreasureFromAllPlans(q,removed),"remove lost treasure from every plan");
            Check(State(Graph(p))==State(Graph(q)),"plan cleanup drops emptied groups while preserving worn state");
            for(int index=0;index<catalog.MaxPlanCount;index++) {
                S.TreasurePlanCallbacks.OperPlayerClearBattlePlanCallback(p,new S.PlayerClearBattlePlanRequest{PlanType=S.BattlePlanType.Treasure,PlanIndex=index},0);
                O.TreasurePlanCallbacks.OperPlayerClearBattlePlanCallback(q,new O.PlayerClearBattlePlanRequest{PlanType=O.BattlePlanType.Treasure,PlanIndex=index},0);
                Check(State(Graph(p))==State(Graph(q))&&State(pe)==State(qe),"clear inactive/current plan preserves training with source clear-before-setup events");scenarios++;
            }
            foreach(int e in elements.Values.Distinct())Check(p.GetCanSetupTreasureSlotIndex((S.ElementType)e)==q.GetCanSetupTreasureSlotIndex((O.ElementType)e),"automatic setup only sees missing keys after clear");
            S.PlayerTreasurePlanExt.ApplyWornTreasureTargetState(p,null);O.PlayerTreasurePlanExt.ApplyWornTreasureTargetState(q,null);
            Check(State(Graph(p))==State(Graph(q)),"null target logs and preserves source state");
        }
    }
    static void Signals()
    {
        var p=new S.GAction<int,int>();var q=new O.GAction<int,int>();var a=new List<string>();var b=new List<string>();int pe=0,qe=0;
        S.TreasurePlanDiagnostics.ReportObserverError=error=>pe++;O.TreasurePlanDiagnostics.ReportObserverError=error=>qe++;
        IDisposable pa=null,qa=null;
        p.Subscribe((x,y)=>{a.Add("first:"+x);pa?.Dispose();p.Subscribe((v,w)=>a.Add("late:"+w));throw new Exception("expected observer");});
        q.Subscribe((x,y)=>{b.Add("first:"+x);qa?.Dispose();q.Subscribe((v,w)=>b.Add("late:"+w));throw new Exception("expected observer");});
        pa=p.Subscribe((x,y)=>a.Add("removed"));qa=q.Subscribe((x,y)=>b.Add("removed"));
        for(int i=0;i<3;i++){p.SafeInvoke(i,i+1);q.SafeInvoke(i,i+1);Check(State(a)==State(b)&&pe==qe,"original signal catches per-observer exceptions and defers appended subscriptions");}
        Check(pe==3&&!a.Contains("removed"),"signal disposal and observer failure do not interrupt subsequent state changes");
        S.TreasurePlanDiagnostics.ReportObserverError=null;O.TreasurePlanDiagnostics.ReportObserverError=null;
    }
    static void Saved()
    {
        var creation=CharacterCreationOfflineService.OpenDefault(Path.Combine(Work,"isolated-saves"));string slot=creation.CreateSlot();var draft=creation.OpenDraft(slot);draft.name="方案验证";
        Check(creation.TryCreateCharacter(slot,draft,out var error)&&creation.TrySetInitialWeapon(slot,CharacterCreationOfflineService.SwordInitialWeaponType,out error),"real isolated character: "+error);
        var saves=new ProfileSlotCatalog(Path.Combine(Work,"isolated-saves"));var profile=OfflineMainlineProgressSession.OpenCharacter(saves,slot);
        var service=new OfflineTreasureSlotService(profile,catalog);var treasureCatalog=OfflineTreasureCatalog.Load();
        var itemInfo=JArray.Parse(File.ReadAllText(Path.Combine(Work,"TreasureCultivationItems.json"))).ToDictionary(r=>(int)r["ClassId"],r=>new Zjcs.Official.TreasureCultivation.ItemInfo {
            ClassId=(int)r["ClassId"],Quality=(int)r["Quality"],ItemType=(Zjcs.Official.TreasureCultivation.ItemType)(int)r["ItemType"]});
        foreach(var row in treasureCatalog.Treasures)itemInfo[row.ClassId].PartInfo=new Zjcs.Official.TreasureCultivation.TreasureInfo{StarMax=row.StarMax,LimitUseCommonMaterial=row.LimitUseCommonMaterial};
        var cultivation=new OfflineTreasureService(profile,treasureCatalog,id=>itemInfo.TryGetValue(id,out var info)?info:null);
        int element=elements.First().Value;var ids=elements.Where(r=>r.Value==element).Select(r=>r.Key).Take(8).ToArray();
        string file=Path.Combine(saves.GetSlotDirectory(slot),"city-save.json");var frames=new Queue<Action>();var trace=new List<string>();
        Check(profile.ApplyCharacterMutation(p=>{
            foreach(int id in ids.Take(5)) {p.Inventory.Add(new OfflineItemStack{ItemId=id,Count=1});p.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=id,Active=id==ids[2],HitCount=treasureCatalog.GetTreasure(id).InitialHitCount});}
        }).IsApplied,"seed actual owned inactive treasures and an existing active item");
        using(var automatic=new OfflineTreasureAutoSetup(cultivation,service,a=>frames.Enqueue(a))) {
            var completions=new List<ProgressionResult>();
            automatic.OnCompleted+=(id,index,result)=>completions.Add(result);
            service.OnSetupTreasureSlot+=()=>{trace.Add("setup");Check(File.ReadAllText(file).Contains("treasure:auto-slot:"),"auto setup event follows durable second transaction");};
            cultivation.OnTreasureActive+=item=>trace.Add("active");cultivation.OnTreasureRefresh+=(item,isNew)=>trace.Add("refresh");
            Check(frames.Count==0,"binding does not equip or replay already-active inventory");
            Check(cultivation.ActivateTreasure(ids[0],"treasure:activate-first",1795767000).IsApplied,"real activation commits");
            Check(frames.Count==1&&!service.Snapshot().IsTreasureInSlot(ids[0])&&State(trace)==State(new[]{"active","refresh"}),"activation and refresh finish before the next-frame request");
            Action first=frames.Dequeue();first();first();
            Check(service.Snapshot().GetTreasureInSlotIndex(ids[0])==0&&trace.Count(s=>s=="setup")==1,"automatic request commits once and replays idempotently");
            Check(cultivation.ActivateTreasure(ids[1],"treasure:activate-second",1795767000).IsApplied&&frames.Count==1,"second activation captures missing index one");
            Check(service.SetupTreasureSlot(ids[2],1,"treasure:manual-race").IsApplied,"intervening manual placement");
            frames.Dequeue()();
            Check(service.Snapshot().GetTreasureInSlotIndex(ids[1])==1&&!service.Snapshot().IsTreasureInSlot(ids[2]),"next frame uses captured index and ordinary replacement instead of reselecting another key");
            Check(cultivation.ActivateTreasure(ids[3],"treasure:activate-overlap-a",1795767000).IsApplied&&cultivation.ActivateTreasure(ids[4],"treasure:activate-overlap-b",1795767000).IsApplied,"overlapping single activation requests");
            Check(frames.Count==2,"independent original next-frame tasks are both retained");while(frames.Count>0)frames.Dequeue()();
            Check(service.Snapshot().GetTreasureInSlotIndex(ids[4])==2&&!service.Snapshot().IsTreasureInSlot(ids[3]),"overlapping single activations preserve source captured-index order");
            var merge=cultivation.GetMergeMaterials(ids[5]);
            Check(profile.ApplyCharacterMutation(p=>{foreach(var cost in merge){var stack=p.Inventory.FirstOrDefault(r=>r.ItemId==cost.Key);if(stack==null)p.Inventory.Add(new OfflineItemStack{ItemId=cost.Key,Count=cost.Value});else stack.Count+=cost.Value;}}).IsApplied,"seed exact original fragment merge fee");
            Check(cultivation.MergeTreasure(ids[5],"treasure:merge-auto",1795767000).IsApplied&&frames.Count==1,"real fragment merge emits durable activation into same auto chain");frames.Dequeue()();
            Check(service.Snapshot().GetTreasureInSlotIndex(ids[5])==3,"merged treasure auto-equips into final missing key");
            Check(service.ClearTreasurePlan(0,"treasure:clear-auto-full").IsApplied,"clear current auto-populated plan");
            Check(profile.ApplyCharacterMutation(p=>{p.Inventory.Add(new OfflineItemStack{ItemId=ids[6],Count=1});p.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=ids[6],HitCount=treasureCatalog.GetTreasure(ids[6]).InitialHitCount});}).IsApplied,"seed another real inactive treasure");
            Check(cultivation.ActivateTreasure(ids[6],"treasure:activate-full-zero-keys",1795767000).IsApplied&&frames.Count==0&&!service.Snapshot().IsTreasureInSlot(ids[6]),"four created zero-ID slots remain full for original automatic lookup");
            Check(completions.Count==6&&completions.Count(r=>r.IsApplied)==5&&completions.Count(r=>r.Status==ProgressionResultStatus.Unchanged)==1,"all automatic completion results are verified outside the async observer boundary");
        }
        var graph=service.Snapshot();var group=graph.GetTreasureElementSlotData((S.ElementType)element);group.TreasureSlots[0].BaseLevel=8;group.TreasureSlots[0].BlessLevel=2;group.UpdateTreasureSlotsShareLevel();
        graph.TreasureSlotData.SeasonId=1;graph.BlessRecordFormat=1;
        var history=new S.PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap(graph.Settings);history.Init(graph,graph.Rank);history.AddBlessLevel((S.ElementType)element,0,2);graph.BlessRecords.Add(1,history);
        ((S.PlayerBattlePlanTreasurePlanContentWrap)graph.GetOrCreatePlanContent(S.BattlePlanType.Treasure,3)).Data.TreasureSlots.Add((S.ElementType)element,new Dictionary<int,int>{{0,ids[0]},{2,ids[5]}});
        ((S.PlayerBattlePlanTreasurePlanContentWrap)graph.GetOrCreatePlanContent(S.BattlePlanType.Treasure,5)).Data.TreasureSlots.Add((S.ElementType)element,new Dictionary<int,int>{{1,ids[1]}});
        ((S.PlayerBattlePlanTreasurePlanContentWrap)graph.GetOrCreatePlanContent(S.BattlePlanType.Treasure,6)).Data.TreasureSlots.Add((S.ElementType)element,new Dictionary<int,int>{{1,ids[7]}});
        Check(profile.ApplyCharacterMutation(p=>p.CharacterProperties.TreasureSlots=OfflineTreasureSlotService.Capture(graph)).IsApplied,"persist trained empty slots, seasonal history and inactive plans");
        string inventory=State(profile.Save.Player.Inventory),record=State(profile.Save.Player.CharacterProperties.TreasureSlots.BlessRecords);var events=new List<string>();
        service.OnBattlePlanContentCleared.Subscribe((type,index)=>events.Add("clear:"+index));service.OnSetupTreasureSlot+=()=>events.Add("setup");
        byte[] before=File.ReadAllBytes(file);Directory.CreateDirectory(file+".tmp");
        try {Check(service.ApplyTreasurePlan(3,"treasure:apply-retry").Status==ProgressionResultStatus.InvalidState&&events.Count==0&&File.ReadAllBytes(file).SequenceEqual(before),"failed plan save rolls back active index, worn items, receipt and events");}
        finally {Directory.Delete(file+".tmp");}
        Check(service.ApplyTreasurePlan(3,"treasure:apply-retry").IsApplied,"plan application retries after actual storage recovery");
        Check(service.Snapshot().ActivePlanIndex==3&&service.Snapshot().GetTreasureInSlotIndex(ids[5])==2,"actual callback selects and applies recorded target");
        Check(group.TreasureSlots[0].BaseLevel==service.Snapshot().GetTreasureSlotData((S.ElementType)element,0).BaseLevel&&service.Snapshot().GetTreasureSlotData((S.ElementType)element,0).BlessLevel==2,"plan switch retains physical slot training");
        Check(inventory==State(profile.Save.Player.Inventory)&&record==State(profile.Save.Player.CharacterProperties.TreasureSlots.BlessRecords),"plan actions preserve exact inventory and season histories");
        before=File.ReadAllBytes(file);int count=events.Count;
        Check(service.ApplyTreasurePlan(3,"treasure:apply-retry").Status==ProgressionResultStatus.Unchanged&&events.Count==count&&File.ReadAllBytes(file).SequenceEqual(before),"receipt replay has no second mutation or notification");
        var cold=OfflineMainlineProgressSession.OpenCharacter(saves,slot);Check(State(cold.Save.Player.CharacterProperties.TreasureSlots)==State(profile.Save.Player.CharacterProperties.TreasureSlots),"cold-open restores complete active/worn/other plans and history");
        events.Clear();Check(service.ClearTreasurePlan(5,"treasure:clear-inactive").IsApplied&&State(events)==State(new[]{"clear:5"})&&service.Snapshot().IsTreasureInSlot(ids[0]),"inactive plan clear only emits content event and preserves current equipment");
        events.Clear();Check(service.ClearTreasurePlan(3,"treasure:clear-active").IsApplied&&State(events)==State(new[]{"clear:3","setup"}),"active clear emits original content-before-equipment order");
        Check(service.Snapshot().GetTreasureSlotData((S.ElementType)element,0).BaseLevel==8&&service.Snapshot().GetTreasureSlotData((S.ElementType)element,0).BlessLevel==2&&service.GetAutomaticSetupTargetIndex(ids[0])==-1,"clear preserves created slots, cultivation and source auto-full behavior");
        Check(service.ApplyTreasurePlan(7,"treasure:apply-empty").IsApplied&&service.Snapshot().ActivePlanIndex==7,"configured empty plan can become active");
        before=File.ReadAllBytes(file);Check(service.ApplyTreasurePlan(catalog.MaxPlanCount,"treasure:invalid-plan").Status==ProgressionResultStatus.InvalidState&&File.ReadAllBytes(file).SequenceEqual(before),"invalid envelope cannot alter source state");
        Check(service.ApplyTreasurePlan(6,"treasure:unowned-plan").Status==ProgressionResultStatus.InvalidState&&File.ReadAllBytes(file).SequenceEqual(before),"stale unowned plan does not manufacture equipment or lose saved references");
        Check(profile.ApplyCharacterMutation(p=>{p.Inventory.Add(new OfflineItemStack{ItemId=ids[7],Count=1});p.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=ids[7],HitCount=treasureCatalog.GetTreasure(ids[7]).InitialHitCount});}).IsApplied,"seed owned inactive plan reference");
        before=File.ReadAllBytes(file);Check(service.ApplyTreasurePlan(6,"treasure:inactive-plan").Status==ProgressionResultStatus.InvalidState&&File.ReadAllBytes(file).SequenceEqual(before),"inactive plan reference rejects atomically");
        // A queued callback belongs to its player/scene lifetime, never a replacement profile.
        int other=elements.First(r=>r.Value!=element).Key;
        Check(profile.ApplyCharacterMutation(p=>{p.Inventory.Add(new OfflineItemStack{ItemId=other,Count=1});p.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=other,HitCount=treasureCatalog.GetTreasure(other).InitialHitCount});}).IsApplied,"seed another element");
        var disposed=new OfflineTreasureAutoSetup(cultivation,service,a=>frames.Enqueue(a));
        Check(cultivation.ActivateTreasure(other,"treasure:activate-before-dispose",1795767000).IsApplied&&frames.Count==1,"pending automatic action is scheduled");disposed.Dispose();frames.Dequeue()();
        Check(!service.Snapshot().IsTreasureInSlot(other),"disposed owner cancels pending profile action and releases activation subscription");
        int failedId=elements.First(r=>r.Value!=element&&r.Key!=other).Key;
        Check(profile.ApplyCharacterMutation(p=>{p.Inventory.Add(new OfflineItemStack{ItemId=failedId,Count=1});p.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=failedId,HitCount=treasureCatalog.GetTreasure(failedId).InitialHitCount});}).IsApplied,"seed automatic write failure case");
        using(var failingWrite=new OfflineTreasureAutoSetup(cultivation,service,a=>frames.Enqueue(a))) {
            ProgressionResult completion=null;failingWrite.OnCompleted+=(id,index,result)=>completion=result;
            Check(cultivation.ActivateTreasure(failedId,"treasure:auto-before-write-failure",1795767000).IsApplied&&frames.Count==1,"activation persists before the second automatic transaction");
            before=File.ReadAllBytes(file);Directory.CreateDirectory(file+".tmp");
            try {frames.Dequeue()();} finally {Directory.Delete(file+".tmp");}
            Check(completion!=null&&completion.Status==ProgressionResultStatus.InvalidState&&File.ReadAllBytes(file).SequenceEqual(before)
                &&profile.Save.Player.CharacterProperties.Treasures.Single(t=>t.ClassId==failedId).Active&&!service.Snapshot().IsTreasureInSlot(failedId),"failed automatic equipment write reports failure while preserving committed activation and trained slots");
            Check(service.SetupTreasureSlot(failedId,0,"treasure:auto-manual-retry").IsApplied,"ordinary manual request can recover after automatic storage failure");
        }
        int schedulerErrors=0,refreshes=0;
        cultivation.OnTreasureRefresh+=(item,isNew)=>refreshes++;
        // Match the async state machine: an immediate dispatcher failure cannot escape into the committed activation callback.
        using(var failing=new OfflineTreasureAutoSetup(cultivation,service,a=>throw new IOException("isolated scheduler failure"),error=>schedulerErrors++)) {
            int another=elements.First(r=>r.Value!=element&&r.Key!=other&&r.Key!=failedId).Key;
            Check(profile.ApplyCharacterMutation(p=>{p.Inventory.Add(new OfflineItemStack{ItemId=another,Count=1});p.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=another,HitCount=treasureCatalog.GetTreasure(another).InitialHitCount});}).IsApplied,"seed dispatcher failure case");
            Check(cultivation.ActivateTreasure(another,"treasure:activate-dispatch-error",1795767000).IsApplied&&schedulerErrors==1&&refreshes==1&&!service.Snapshot().IsTreasureInSlot(another),"async infrastructure error is reported while activation and following refresh remain durable");
        }
    }
}
