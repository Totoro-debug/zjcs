using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureCultivation;
using Zjcs.Official.TreasureUI;
using S=Zjcs.Official.TreasureSlots;

namespace Zjcs.Offline.TreasureUI
{
    // Window-owned projection of the shared saved player. The enclosing original UI owns presentation services.
    public sealed class OfflineTreasureIconServices : ITreasureIconServices,IProfessionItemPlayer,IDisposable
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflineTreasureTipService tips;
        private readonly OfflineTreasureSlotService slots;
        private readonly Func<int,IProfessionItemInfo> readInfo;
        private readonly Func<int> readBaseCap;
        private readonly Action<GameObject,bool> gray;
        private readonly Action<ProgressionResult> failed;
        private readonly Func<object,bool> canGetFromMap;
        private readonly Dictionary<int,Item> items=new Dictionary<int,Item>();
        private Dictionary<int,long> counts;
        private Dictionary<int,string> states;
        private string slotState;
        private int baseCap;
        private bool autoMerge,awake,useCommon,disposed;
        private event Action SettingsChanged;
        public SafeAction PlayerUpdated { get; }=new SafeAction();
        public IProfessionTipDotSystem TipDotSystem { get; }
        public ITreasureReplaceEventLisener ReplaceListener { get; }=new TreasureReplaceEventLisener();
        public bool PlayerAvailable=>!disposed;
        public bool ManagerAvailable=>!disposed;
        public event Action<IProfessionItemData,bool> OnTreasureRefresh;
        public event Action<int> OnTreasureSlotShareLevelUpdate;
        public event Action OnAllTreasureBlessUpdate;
        public event Action<IProfessionItemData,long,object> OnItemCountUpdate;
        public OfflineTreasureIconServices(OfflineMainlineProgressSession session,OfflineTreasureTipService tips,
            OfflineTreasureSlotService slots,Func<int,IProfessionItemInfo> readInfo,IProfessionTipDotSystem tipDots,
            Func<int> readBaseCap,Action<GameObject,bool> gray,Action<ProgressionResult> failed,Func<object,bool> canGetFromMap)
        {
            this.session=session??throw new ArgumentNullException(nameof(session));this.tips=tips??throw new ArgumentNullException(nameof(tips));
            this.slots=slots??throw new ArgumentNullException(nameof(slots));this.readInfo=readInfo??throw new ArgumentNullException(nameof(readInfo));
            TipDotSystem=tipDots??throw new ArgumentNullException(nameof(tipDots));this.readBaseCap=readBaseCap??throw new ArgumentNullException(nameof(readBaseCap));
            this.gray=gray??throw new ArgumentNullException(nameof(gray));this.failed=failed??throw new ArgumentNullException(nameof(failed));
            this.canGetFromMap=canGetFromMap??throw new ArgumentNullException(nameof(canGetFromMap));
            counts=Counts();states=States();slotState=Slots();baseCap=readBaseCap();autoMerge=AutoMergeWhenLevelUp;awake=AwakeAutoMergeEnable;
            useCommon=session.Save.Player.CharacterProperties.TreasureSlots.UpgradeUseCommonMaterailEnable;
            tips.Cultivation.OnTreasureRefresh+=CultivationRefresh;
            slots.OnTreasureRefresh+=SlotRefresh;slots.OnTreasureSlotShareLevelUpdate+=ShareLevel;
            slots.OnAllTreasureBlessUpdate+=AllBless;
        }
        public bool AutoMergeWhenLevelUp=>tips.AutoMergeWhenLevelUp;
        public bool AwakeAutoMergeEnable=>session.Save.Player.CharacterProperties.TreasureSlots.AwakeAutoMergeEnable;
        public long GetItemAmount(int id)=>session.InventoryCount(id);
        public bool HasItem(int id,long count)=>GetItemAmount(id)>=count;
        public bool CanGetItemFromMapLocation(object location)=>canGetFromMap(location);
        public IProfessionItemData GetItemById(int id)=>session.Save.Player.Inventory.Any(row=>row.ItemId==id)?GetItem(id):null;
        private Item GetItem(int id)
        {
            if(!items.TryGetValue(id,out var result))items.Add(id,result=new Item(this,id,readInfo(id)??throw new FormatException("Unknown original item: "+id)));
            return result;
        }
        public TreasureState GetState(IProfessionItemData data)
        {
            if(data==null)return null;
            var view=GetItem(data.Id);var current=tips.Cultivation.GetItemById(data.Id);
            // Original item wrappers retain their parameter while an icon still references a removed stack.
            if(current!=null)view.State=current.State;
            return view.State;
        }
        public int GetStarMax(IProfessionItemData data)=>tips.Cultivation.GetTreasure(data.Info.ClassId).StarMax;
        public int GetElement(int id)=>(tips.Catalog.Get(id)??throw new FormatException("Unknown original element: "+id)).Element;
        public int GetTreasureFinalLevel(int id)=>slots.Snapshot().GetTreasureFinalLevel(id);
        public int GetMaxTreasureBaseLevel()=>readBaseCap();
        public bool ShowTip(IProfessionItemData item,List<TreasureRedDotEnum> types,bool merge,bool autoAwake)
            =>tips.Query().TreasureRedDot(tips.Cultivation.GetItemById(item.Id),types,out _,merge,autoAwake);
        public bool IsPromotionItem(IProfessionItemData item)=>tips.IsPromotionItem(item.Info.ClassId);
        public bool CanSetupTreasureSlot(int id,int index)=>S.TreasureHelper.CanSetupTreasureSlot(slots.Snapshot(),id,index);
        public void SetupTreasureSlot(PlayerSetupTreasureSlotRequest request)
        {
            if(disposed)throw new ObjectDisposedException(nameof(OfflineTreasureIconServices));
            if(request==null)throw new ArgumentNullException(nameof(request));
            var result=slots.SetupTreasureSlot(request.TreasureClassId,request.TargetIndex,"treasure-icon-replace-"+Guid.NewGuid().ToString("N"));
            if(result.IsApplied||result.Status==ProgressionResultStatus.Unchanged)Synchronize();else failed(result);
        }
        public void SetUIGray(GameObject target,bool value)=>gray(target,value);
        public IDisposable SubscribeSettings(Action changed)
        {
            if(disposed)throw new ObjectDisposedException(nameof(OfflineTreasureIconServices));
            if(changed==null)throw new ArgumentNullException(nameof(changed));
            SettingsChanged+=changed;return new Subscription(()=>SettingsChanged-=changed);
        }
        private Dictionary<int,long> Counts()=>session.Save.Player.Inventory.GroupBy(row=>row.ItemId).ToDictionary(group=>group.Key,group=>group.Sum(row=>row.Count));
        private Dictionary<int,string> States()=>session.Save.Player.CharacterProperties.Treasures.ToDictionary(row=>row.ClassId,row=>row.Active+":"+row.Star+":"+row.HitCount);
        private string Slots()=>JsonUtility.ToJson(session.Save.Player.CharacterProperties.TreasureSlots);
        private void SyncCounts()
        {
            var next=Counts();var previous=counts;counts=next;
            foreach(int id in previous.Keys.Concat(next.Keys).Distinct())
            {
                previous.TryGetValue(id,out long before);next.TryGetValue(id,out long after);
                if(before!=after)OnItemCountUpdate?.Invoke(GetItem(id),before,null);
            }
        }
        private void CultivationRefresh(TreasureItem item,bool isNew)
        {SyncCounts();states=States();OnTreasureRefresh?.Invoke(GetItem(item.Id),isNew);}
        private void SlotRefresh(S.SlotItem item,bool isNew)
        {SyncCounts();states=States();OnTreasureRefresh?.Invoke(GetItem(item.Id),isNew);}
        private void ShareLevel(S.ElementType element)=>OnTreasureSlotShareLevelUpdate?.Invoke((int)element);
        private void AllBless()=>OnAllTreasureBlessUpdate?.Invoke();
        // Call after the window owner's save reload/tick to observe commits from other windows too.
        public void Synchronize()
        {
            if(disposed)return;
            session.Reload();SyncCounts();var next=States();var previous=states;states=next;
            foreach(int id in previous.Keys.Concat(next.Keys).Distinct())
                if(!previous.TryGetValue(id,out var before)||!next.TryGetValue(id,out var after)||before!=after)
                    OnTreasureRefresh?.Invoke(GetItem(id),!previous.ContainsKey(id));
            string current=Slots();int currentCap=readBaseCap();
            if(current!=slotState||currentCap!=baseCap){slotState=current;baseCap=currentCap;OnAllTreasureBlessUpdate?.Invoke();}
            if(autoMerge!=AutoMergeWhenLevelUp||awake!=AwakeAutoMergeEnable||useCommon!=session.Save.Player.CharacterProperties.TreasureSlots.UpgradeUseCommonMaterailEnable)
                PublishSettings();
        }
        // The source publishes its setting notification after the successful local callback, including unchanged settings.
        public void PublishSettings()
        {
            if(disposed)throw new ObjectDisposedException(nameof(OfflineTreasureIconServices));
            autoMerge=AutoMergeWhenLevelUp;awake=AwakeAutoMergeEnable;
            useCommon=session.Save.Player.CharacterProperties.TreasureSlots.UpgradeUseCommonMaterailEnable;
            SettingsChanged?.Invoke();
        }
        public void Dispose()
        {
            if(disposed)return;
            tips.Cultivation.OnTreasureRefresh-=CultivationRefresh;slots.OnTreasureRefresh-=SlotRefresh;
            slots.OnTreasureSlotShareLevelUpdate-=ShareLevel;slots.OnAllTreasureBlessUpdate-=AllBless;
            disposed=true;PlayerUpdated.Invoke();PlayerUpdated.ClearListener();SettingsChanged=null;
        }
        private sealed class Subscription:IDisposable
        {
            private Action release;public Subscription(Action release)=>this.release=release;
            public void Dispose(){var action=release;release=null;action?.Invoke();}
        }
        private sealed class Item:IProfessionItemData
        {
            private readonly OfflineTreasureIconServices owner;
            public TreasureState State;
            public Item(OfflineTreasureIconServices owner,int id,IProfessionItemInfo info)
            {this.owner=owner;Id=id;Info=info;State=owner.tips.Cultivation.GetItemById(id)?.State;}
            public int Id{get;}public IProfessionItemInfo Info{get;}
            public long Count=>owner.GetItemAmount(Id);
            public Quality GetQuality()=>Info.Quality;
            public bool CanExpire()=>false;
        }
    }
}
