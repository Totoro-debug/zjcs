using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using C=Zjcs.Official.TreasureCultivation;
using S=Zjcs.Official.TreasureSlots;
using O=TreasureTipOracle;
using H=Zjcs.Official.HomeTipDots;

public sealed class TreasureTipProbe:C.ITreasureTipContext
{
    public readonly List<C.TreasureItem> Items=new List<C.TreasureItem>();
    public readonly Dictionary<int,int> Elements=new Dictionary<int,int>();
    public readonly Dictionary<int,C.TreasureInfo> Infos=new Dictionary<int,C.TreasureInfo>();
    public readonly Dictionary<int,int> PieceIds=new Dictionary<int,int>();
    public readonly HashSet<int> Suits=new HashSet<int>();
    public readonly List<string> Trace=new List<string>();
    public int ActiveCode,SlotCode,UpgradeCode,MergeCode,Index=-1,NextCode;
    public bool Policy;
    public IReadOnlyCollection<C.TreasureItem> FindItemsByType(C.ItemType type){Trace.Add("find:"+(int)type);return Items.Where(row=>row.Info.ItemType==type).ToArray();}
    public S.ElementType GetElement(int id)=>(S.ElementType)Elements[id];
    public C.TreasureTipItemInfo GetItemInfo(int id){Trace.Add("info:"+id);return Elements.TryGetValue(id,out int e)?new C.TreasureTipItemInfo{ElementType=(S.ElementType)e}:null;}
    public int GetSuitClassIdByTreasure(int id){Trace.Add("suit:"+id);return Suits.Contains(id)?1:0;}
    public int GetTreasureIdByPieceId(int id){Trace.Add("piece:"+id);return PieceIds[id];}
    public bool HasItem(int id,long count){Trace.Add("has:"+id+":"+count);return (Items.FirstOrDefault(row=>row.Id==id)?.Count??0)>=count;}
    public bool UpgradeUseCommonMaterailEnable {get {Trace.Add("policy");return Policy;}}
    public int GetTreasureInSlotIndex(int id){Trace.Add("index:"+id);return Index;}
    public int CanActivateTreasure(int id){Trace.Add("active:"+id);return ActiveCode;}
    public int CanMergeTreasure(int id){Trace.Add("merge:"+id);return MergeCode;}
    public int CanTreasureSlotLevelUp(S.ElementType element,int index,bool autoMerge){Trace.Add($"slot:{(int)element}:{index}:{autoMerge}");return SlotCode;}
    public int GetTreasureNextLevelUpgradeMaterial(int id,bool hasEmptyItem,bool useCommonItem,out Dictionary<int,long> materials)
    {Trace.Add($"next:{id}:{hasEmptyItem}:{useCommonItem}");materials=new Dictionary<int,long>{{1,7}};return NextCode;}
    public int CanTreasureUpgrade(int id,Dictionary<int,long> materials,bool autoMerge,bool canUseCommonMaterial)
    {Trace.Add($"upgrade:{id}:{materials[1]}:{autoMerge}:{canUseCommonMaterial}");return UpgradeCode;}
    public C.TreasureInfo GetTreasure(int id){Trace.Add("treasure:"+id);return Infos[id];}
    public void Add(int id,int piece,int element,int maximum,bool limited,bool suit)
    {
        var info=new C.TreasureInfo{StarMax=maximum,LimitUseCommonMaterial=limited};Infos[id]=info;Elements[id]=element;PieceIds[piece]=id;
        if(suit)Suits.Add(id);
        Items.Add(new C.TreasureItem{Id=id,Count=1,Info=new C.ItemInfo{ClassId=id,ItemType=(C.ItemType)305,PartInfo=info},State=new C.TreasureState{Active=true,Star=1}});
        Items.Add(new C.TreasureItem{Id=piece,Count=999,Info=new C.ItemInfo{ClassId=piece,ItemType=C.ItemType.TreasurePiece}});
    }
}

public static partial class HomeDeskRuntimeValidation
{
    private static void TipCheck(bool value,string message)
    {checksCount++;if(!value)throw new Exception(message);}
    private static void CheckTreasureTipRules()
    {
        var catalog=OfflineTreasureCatalog.Load();var metadata=OfflineTreasureTipCatalog.LoadDefault();int cases=0;
        Action<TreasureTipProbe,List<C.TreasureRedDotEnum>,int,int> compare=(probe,tips,element,mode)=>{
            var rules=new C.OfficialTreasureTipRules(probe);var original=new O.Player{Probe=probe};O.MeManager.MainPlayer=original;
            var otips=tips.Select(tip=>(O.TreasureRedDotEnum)(int)tip).ToList();var item=probe.Items.First(row=>(int)row.Info.ItemType==305);
            bool auto=(cases&1)!=0,awake=(cases&2)!=0,common=(cases&4)!=0;
            C.TreasureRedDotEnum actualTip=C.TreasureRedDotEnum.None;O.TreasureRedDotEnum expectedTip=O.TreasureRedDotEnum.None;
            probe.Trace.Clear();bool actual;
            switch(mode){
                case 0:actual=rules.TreasureRedDot(item,tips,out actualTip,auto,awake,common);break;
                case 1:actual=rules.TreasureRedDot((S.ElementType)element,tips,auto,awake,common);break;
                case 2:actual=rules.AnyTreasureRedDot(tips,auto,awake);break;
                case 3:actual=rules.TreasureSummaryRedDot(tips,auto,awake);break;
                case 4:actual=rules.TreasurePieceRedDot((S.ElementType)element);break;
                case 5:actual=rules.TreasureCanUpgrade(item,auto);break;
                case 6:actual=rules.TreasureCanUpgrade(item,auto,common);break;
                default:actual=rules.TreasureCanWakeup(item,awake);break;
            }
            var trace=probe.Trace.ToArray();probe.Trace.Clear();bool expected;
            switch(mode){
                case 0:expected=original.TreasureRedDot(O.Player.Wrap(item,probe),otips,out expectedTip,auto,awake,common);break;
                case 1:expected=original.TreasureRedDot((O.ElementType)element,otips,auto,awake,common);break;
                case 2:expected=original.AnyTreasureRedDot(otips,auto,awake);break;
                case 3:expected=original.TreasureSummaryRedDot(otips,auto,awake);break;
                case 4:expected=original.TreasurePieceRedDot((O.ElementType)element);break;
                case 5:expected=original.TreasureCanUpgrade(O.Player.Wrap(item,probe),auto);break;
                case 6:expected=original.TreasureCanUpgrade(O.Player.Wrap(item,probe),auto,common);break;
                default:expected=original.TreasureCanWakeup(O.Player.Wrap(item,probe),awake);break;
            }
            TipCheck(actual==expected&&(int)actualTip==(int)expectedTip,"untouched original tip result and fixed priority, mode "+mode);
            TipCheck(trace.SequenceEqual(probe.Trace),"untouched original tip query order and exact merge/common flags: "+string.Join("/",trace)+" <> "+string.Join("/",probe.Trace));cases++;
        };
        var all=new List<C.TreasureRedDotEnum>{C.TreasureRedDotEnum.Upgrade,C.TreasureRedDotEnum.Wakeup,C.TreasureRedDotEnum.LevelUp,C.TreasureRedDotEnum.Merge,C.TreasureRedDotEnum.Active};
        foreach(var row in catalog.Treasures){
            int element=metadata.Get(row.ClassId).Element;
            foreach(int star in new[]{0,Math.Max(0,row.StarMax-2),row.StarMax-1,row.StarMax}.Distinct())foreach(bool active in new[]{false,true})foreach(int mask in Enumerable.Range(0,16)){
                var probe=new TreasureTipProbe{ActiveCode=(mask&1)==0?0:1188,SlotCode=(mask&2)==0?0:53307,UpgradeCode=(mask&4)==0?0:1188,MergeCode=(mask&8)==0?0:1188,
                    Index=(mask&1)==0?0:-1,Policy=(mask&2)!=0,NextCode=(mask&4)==0?0:1324};
                probe.Add(row.ClassId,row.TreasurePieceId,element,row.StarMax,row.LimitUseCommonMaterial,catalog.GetSuitByTreasureId(row.ClassId)!=null);
                probe.Items[0].State.Active=active;probe.Items[0].State.Star=star;
                compare(probe,all,element,0);
                if(mask==3||mask==12)for(int mode=5;mode<8;mode++)compare(probe,all,element,mode);
                if(mask==0||mask==15)for(int mode=1;mode<5;mode++){compare(probe,all,0,mode);compare(probe,all,element+100,mode);}
            }
        }
        // All subsets, including empty lists, and both owned/unowned fragment branches.
        foreach(var row in catalog.Treasures.GroupBy(r=>r.LimitUseCommonMaterial).Select(group=>group.First()))foreach(int bits in Enumerable.Range(0,32))foreach(int mode in Enumerable.Range(0,5)){
            var probe=new TreasureTipProbe{Policy=(bits&1)!=0,ActiveCode=bits%3,SlotCode=bits%4,UpgradeCode=bits%5,MergeCode=bits%2};
            probe.Add(row.ClassId,row.TreasurePieceId,metadata.Get(row.ClassId).Element,row.StarMax,row.LimitUseCommonMaterial,true);
            probe.Items[0].State.Active=(bits&2)!=0;probe.Items[0].State.Star=(bits&4)==0?1:row.StarMax-1;probe.Items[0].Count=(bits&8)==0?0:1;
            compare(probe,Enumerable.Range(0,5).Where(i=>(bits&(1<<i))!=0).Select(i=>(C.TreasureRedDotEnum)i).ToList(),0,mode);
        }
        UnityEngine.Debug.Log("TREASURE_TIPS_ORIGINAL_PASSED: "+cases+" decision/query-order cases");
    }

    private static IEnumerator CheckTreasureTipScene(OfflineMainlineProgressSession profile,string slot)
    {
        var model=home.TreasureTips;var rules=OfflineTreasureCatalog.Load();var tipTypes=new List<H.TreasureRedDotEnum>{H.TreasureRedDotEnum.Active,H.TreasureRedDotEnum.Merge,H.TreasureRedDotEnum.LevelUp,H.TreasureRedDotEnum.Wakeup,H.TreasureRedDotEnum.Upgrade};
        Require(model!=null&&home.HomeTipServices.AnyTreasureRedDotEvaluator!=null,"real Home owns the shared original treasure evaluator");
        int id=rules.Treasures.First(row=>!row.LimitUseCommonMaterial).ClassId;var treasure=rules.GetTreasure(id);int element=model.Catalog.Get(id).Element;
        Func<bool> visible=()=>home.HomeTipServices.AnyTreasureRedDot(tipTypes,home.HomeTipServices.AutoMergeWhenLevelUp,home.HomeTipServices.AwakeAutoMergeEnable);
        var linker=Desk.GetComponentsInChildren<Zjcs.Official.ProfessionUI.TipDotNodeLinker>(true)
            .Single(node=>Field<Zjcs.Official.ProfessionUI.TipDotGraphNodeSelector>(node,"mGraphNodeSelector").GraphNode.NodeName=="UnlockedTreasureDeskHasTip");
        Func<bool> rendered=()=>linker.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true)
            .Any(sprite=>sprite.sprite!=null&&sprite.enabled&&sprite.gameObject.activeInHierarchy);
        string file=System.IO.Path.Combine(new Zjcs.Offline.State.ProfileSlotCatalog(System.IO.Path.Combine(Work,"isolated-saves")).GetSlotDirectory(slot),"city-save.json");
        Require(model.AutoMergeWhenLevelUp&&home.HomeTipServices.AutoMergeWhenLevelUp,"missing client settings use the official enabled default");
        Require(model.SetAutoMergeWhenLevelUp(false,"desk-tip-client-setting").IsApplied,"save the original per-character client preference");
        yield return WaitFrames(8);
        Require(!home.HomeTipServices.AutoMergeWhenLevelUp&&model.SetAutoMergeWhenLevelUp(false,"desk-tip-client-setting").Status==ProgressionResultStatus.Unchanged,"committed client preference refreshes the graph and supports request replay");
        var preferenceBytes=System.IO.File.ReadAllBytes(file);System.IO.Directory.CreateDirectory(file+".tmp");
        try {Require(model.SetAutoMergeWhenLevelUp(true,"desk-tip-client-setting-retry").Status==ProgressionResultStatus.InvalidState
            &&!model.AutoMergeWhenLevelUp&&preferenceBytes.SequenceEqual(System.IO.File.ReadAllBytes(file)),"real client-setting write failure preserves the preference and save");}
        finally {System.IO.Directory.Delete(file+".tmp");}
        profile.Reload();
        // Profile is a separate owner: exercise the same durable reopen used when another window changes treasure state.
        Require(profile.ApplyCharacterMutation(p=>{
            p.Inventory.RemoveAll(row=>model.Catalog.Get(row.ItemId)?.Type==305||model.IsPromotionItem(row.ItemId));
            p.CharacterProperties.Treasures.Clear();p.CharacterProperties.TreasureSlots=new OfflineTreasureSlotProgress();
        }).IsApplied,"clear isolated treasure state without touching user saves");
        home.Profile.Reload();yield return WaitFrames(8);
        Require(!visible()&&!linker.IsShowTip&&!rendered(),"empty real inventory clears the actual native desk graph and rendered red dot");yield return Capture("09-desk-tip-empty.png");
        Require(profile.ApplyCharacterMutation(p=>{
            p.Inventory.Add(new OfflineItemStack{ItemId=treasure.TreasurePieceId,Count=100000});
        }).IsApplied,"commit real merge fragments through another profile owner");
        home.Profile.Reload();yield return WaitFrames(8);
        bool query=visible(),piece=home.HomeTipServices.TreasurePieceRedDot((Zjcs.Official.BattleSkillPlayables.ElementType)element),graph=linker.IsShowTip,render=rendered();
        Require(query&&piece&&graph&&render,$"actual fragment inventory reaches the real desk graph and original rendered red dot; query={query}, piece={piece}, graph={graph}, render={render}, level={linker.StateInfo.TipLevel}");
        yield return Capture("10-desk-tip-fragments.png");
        var before=System.IO.File.ReadAllBytes(file);for(int i=0;i<8;i++)visible();
        Require(before.SequenceEqual(System.IO.File.ReadAllBytes(file)),"graph queries never mutate inventory, slots, names or receipts");
        var merged=model.Cultivation.MergeTreasure(id,"desk-tip-merge",DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Require(merged.Status==ProgressionResultStatus.Applied,"actual fragment tip can perform the real atomic merge");
        home.Profile.Reload();profile.Reload();yield return WaitFrames(8);
        Require(profile.Save.Player.CharacterProperties.Treasures.Single(row=>row.ClassId==id).Active,"tip transition observes a committed activated treasure");
        Require(profile.ApplyCharacterMutation(p=>{
            p.Inventory.RemoveAll(row=>model.IsPromotionItem(row.ItemId));
            p.CharacterProperties.Treasures.Single(row=>row.ClassId==id).Star=treasure.StarMax;
        }).IsApplied,"commit maximum star and remove upgrade resources");
        home.Profile.Reload();yield return WaitFrames(8);
        Require(!visible()&&!linker.IsShowTip&&!rendered(),"maximum-star treasure without funds clears actual graph and rendered cultivation tip");yield return Capture("11-desk-tip-cleared.png");
        var prior=home.HomeTipServices;home.Close();home=null;yield return WaitFrames(3);
        Require(prior.AnyTreasureRedDotEvaluator==null&&prior.TreasureRedDotEvaluator==null,"scene teardown releases evaluator ownership");
        home=OpenHome(slot,System.IO.Path.Combine(Work,"isolated-saves"),new NativePointer());yield return WaitFrames(8);FocusDesk();
        Require(!home.TreasureTips.AutoMergeWhenLevelUp&&!home.HomeTipServices.AutoMergeWhenLevelUp
            &&!home.HomeTipServices.AnyTreasureRedDot(tipTypes,false,home.HomeTipServices.AwakeAutoMergeEnable),"actual cold Home reopen restores client preference and cleared cultivation state");
        yield return Capture("12-desk-tip-cold-reopen.png");home.Close();home=null;yield return WaitFrames(3);
        UnityEngine.Debug.Log("TREASURE_TIPS_SCENE_PASSED");
    }
}

namespace TreasureTipOracle
{
    public enum ElementType {None=0}
    public enum ItemType {Treasure=305,TreasurePiece=306}
    public sealed class TreasureInfo {public int StarMax;public bool LimitUseCommonMaterial;}
    public sealed class ItemInfo {public int ClassId;public ElementType ElementType;public object PartInfo;}
    public sealed class PlayerItemDataWrap
    {
        public int Id;public ItemInfo Info;public C.TreasureState State;
        public C.TreasureState GetItemParamTreasure()=>State;
    }
    public sealed class SlotData {public TreasureTipProbe Probe;public bool UpgradeUseCommonMaterailEnable=>Probe.UpgradeUseCommonMaterailEnable;}
    public partial class Player
    {
        public TreasureTipProbe Probe;
        public SlotData TreasureSlotData=>new SlotData{Probe=Probe};
        public IReadOnlyCollection<PlayerItemDataWrap> FindItemsByType(ItemType type)=>Probe.FindItemsByType((C.ItemType)(int)type).Select(item=>Wrap(item,Probe)).ToArray();
        public bool HasItem(int id,long count)=>Probe.HasItem(id,count);
        public int GetTreasureInSlotIndex(int id)=>Probe.GetTreasureInSlotIndex(id);
        public static PlayerItemDataWrap Wrap(C.TreasureItem item,TreasureTipProbe probe)=>new PlayerItemDataWrap{Id=item.Id,State=item.State,Info=new ItemInfo{ClassId=item.Info.ClassId,
            ElementType=probe.Elements.TryGetValue(item.Id,out int e)?(ElementType)e:ElementType.None,
            PartInfo=item.Info.PartInfo==null?null:new TreasureInfo{StarMax=item.Info.PartInfo.StarMax,LimitUseCommonMaterial=item.Info.PartInfo.LimitUseCommonMaterial}}};
    }
    public static class MeManager {public static Player MainPlayer;}
    public static class Formula {public static int GetTreasureIdByPieceId(int id)=>MeManager.MainPlayer.Probe.GetTreasureIdByPieceId(id);}
    public sealed class ItemParser {public ItemInfo Get(int id){var info=MeManager.MainPlayer.Probe.GetItemInfo(id);return info==null?null:new ItemInfo{ClassId=id,ElementType=(ElementType)(int)info.ElementType};}}
    public sealed class TreasureParser {public TreasureInfo Get(int id){var info=MeManager.MainPlayer.Probe.GetTreasure(id);return new TreasureInfo{StarMax=info.StarMax,LimitUseCommonMaterial=info.LimitUseCommonMaterial};}}
    public sealed class SuitParser {public int GetSuitClassIdByTreasure(int id)=>MeManager.MainPlayer.Probe.GetSuitClassIdByTreasure(id);}
    public sealed class Parsers {public static Parsers Instance=new Parsers();public ItemParser ItemInfoParser=new ItemParser();public TreasureParser TreasureInfoParser=new TreasureParser();public SuitParser TreasureSuitInfoParser=new SuitParser();}
    public static partial class TreasureHelper
    {
        public static int CanActivateTreasure(int id,Player p)=>p.Probe.CanActivateTreasure(id);
        public static int CanMergeTreasure(int id,Player p)=>p.Probe.CanMergeTreasure(id);
        public static int CanTreasureSlotLevelUp(Player p,ElementType e,int index,bool auto)=>p.Probe.CanTreasureSlotLevelUp((S.ElementType)(int)e,index,auto);
        public static int GetTreasureNextLevelUpgradeMaterial(int id,Player p,bool hasEmptyItem,bool useCommonItem,out Dictionary<int,long> materials)=>p.Probe.GetTreasureNextLevelUpgradeMaterial(id,hasEmptyItem,useCommonItem,out materials);
        public static int CanTreasureUpgrade(int id,Dictionary<int,long> materials,Player p,bool autoMerge,bool canUseCommonMaterial)=>p.Probe.CanTreasureUpgrade(id,materials,autoMerge,canUseCommonMaterial);
    }
}
