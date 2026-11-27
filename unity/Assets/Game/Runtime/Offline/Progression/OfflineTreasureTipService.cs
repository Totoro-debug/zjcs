using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.TreasureCultivation;
using S=Zjcs.Official.TreasureSlots;

namespace Zjcs.Offline.Progression
{
    public sealed class OfflineTreasureTipCatalog
    {
        [Serializable] private sealed class Row { public int ClassId,Quality,ItemType,ElementType; }
        [Serializable] private sealed class Payload { public Row[] Items; }
        public sealed class Item
        {
            public int Id { get; }
            public int Quality { get; }
            public int Type { get; }
            public int Element { get; }
            internal Item(int id,int quality,int type,int element) {Id=id;Quality=quality;Type=type;Element=element;}
        }
        private readonly Dictionary<int,Item> items;
        public IReadOnlyCollection<Item> Items { get; }
        private OfflineTreasureTipCatalog(Payload data)
        {
            if(data?.Items==null||data.Items.Length==0||data.Items.Any(row=>row==null)
                ||data.Items.Select(row=>row.ClassId).Distinct().Count()!=data.Items.Length)
                throw new FormatException("Original treasure tip item classification is invalid.");
            items=data.Items.ToDictionary(row=>row.ClassId,row=>new Item(row.ClassId,row.Quality,row.ItemType,row.ElementType));
            Items=Array.AsReadOnly(items.Values.ToArray());
        }
        public static OfflineTreasureTipCatalog LoadDefault()
        {
            var raw=Resources.Load<TextAsset>("Treasure/treasure-tip-items");
            if(raw==null)throw new InvalidOperationException("Original treasure tip item classification is unavailable.");
            return new OfflineTreasureTipCatalog(JsonUtility.FromJson<Payload>(raw.text));
        }
        public Item Get(int id)=>items.TryGetValue(id,out var row)?row:null;
    }

    // One query context uses the shared saved player, original promotion checks and detached slot projection.
    public sealed class OfflineTreasureTipService
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflineTreasureCatalog treasures;
        private readonly OfflineTreasureSlotService slots;
        private readonly Func<long> now;
        public OfflineTreasureTipCatalog Catalog { get; }
        public OfflineTreasureService Cultivation { get; }
        public OfflineTreasureTipService(OfflineMainlineProgressSession session,OfflineTreasureCatalog treasures,
            OfflineTreasureTipCatalog catalog,OfflineTreasureSlotService slots,Func<long> now=null)
        {
            this.session=session??throw new ArgumentNullException(nameof(session));this.treasures=treasures??throw new ArgumentNullException(nameof(treasures));
            Catalog=catalog??throw new ArgumentNullException(nameof(catalog));this.slots=slots??throw new ArgumentNullException(nameof(slots));
            this.now=now??(()=>DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Cultivation=new OfflineTreasureService(session,treasures,id=>{
                var row=Catalog.Get(id);return row==null?null:new ItemInfo{ClassId=id,Quality=row.Quality,ItemType=(ItemType)row.Type};
            });
        }
        public OfficialTreasureTipRules Query()=>new OfficialTreasureTipRules(new Context(this));
        public bool AutoMergeWhenLevelUp=>session.Save.Player.CharacterProperties.TreasureAutoMergeWhenLevelUp;
        public ProgressionResult SetAutoMergeWhenLevelUp(bool value,string receipt)
        {
            if(string.IsNullOrWhiteSpace(receipt)||receipt.Length>160)return new ProgressionResult{Status=ProgressionResultStatus.InvalidState};
            try {
                session.Reload();
                return session.ApplyInventoryTransaction(receipt,next=>next.Player.CharacterProperties.TreasureAutoMergeWhenLevelUp=value);
            }
            catch(IOException error){return new ProgressionResult{Status=ProgressionResultStatus.InvalidState,Message=error.Message};}
            catch(UnauthorizedAccessException error){return new ProgressionResult{Status=ProgressionResultStatus.InvalidState,Message=error.Message};}
        }
        public bool IsPromotionItem(int id)
        {
            var info=Cultivation.GetItemInfo(id);
            return info!=null&&Cultivation.Checks.IsItemEffectTreasurePromote(new TreasureItem{Id=id,Info=info});
        }
        public IEnumerable<int> ObservedItemIds=>Catalog.Items.Where(row=>row.Type==305||IsPromotionItem(row.Id)).Select(row=>row.Id);
        private sealed class Context:ITreasureTipContext
        {
            private readonly OfflineTreasureTipService owner;
            private S.Player levelPlayer;
            private S.Player slotPlayer;
            public Context(OfflineTreasureTipService owner)=>this.owner=owner;
            public IReadOnlyCollection<TreasureItem> FindItemsByType(ItemType type)=>owner.session.Save.Player.Inventory
                .Where(row=>owner.Catalog.Get(row.ItemId)?.Type==(int)type).Select(row=>owner.Cultivation.GetItemById(row.ItemId)).ToArray();
            public S.ElementType GetElement(int id)=>(S.ElementType)(owner.Catalog.Get(id)??throw new FormatException("Unknown original treasure tip item.")).Element;
            public TreasureTipItemInfo GetItemInfo(int id)
            {var item=owner.Catalog.Get(id);return item==null?null:new TreasureTipItemInfo{ElementType=(S.ElementType)item.Element};}
            public int GetSuitClassIdByTreasure(int id)=>owner.treasures.GetSuitByTreasureId(id)?.ClassId??0;
            public int GetTreasureIdByPieceId(int id)=>owner.Cultivation.GetTreasureIdByPieceId(id);
            public bool HasItem(int id,long count)=>owner.Cultivation.HasItem(id,count);
            public bool UpgradeUseCommonMaterailEnable=>owner.session.Save.Player.CharacterProperties.TreasureSlots.UpgradeUseCommonMaterailEnable;
            public int GetTreasureInSlotIndex(int id)=>(slotPlayer??=owner.slots.Snapshot()).GetTreasureInSlotIndex(id);
            public int CanActivateTreasure(int id)=>owner.Cultivation.CanActivateTreasure(id);
            public int CanMergeTreasure(int id)=>owner.Cultivation.CanMergeTreasure(id);
            public int CanTreasureSlotLevelUp(S.ElementType element,int index,bool autoMerge)
                =>S.TreasureHelper.CanTreasureSlotLevelUp(levelPlayer??=owner.slots.CurrentLevelSnapshot(owner.now()),element,index,autoMerge);
            public int GetTreasureNextLevelUpgradeMaterial(int id,bool hasEmptyItem,bool useCommonItem,out Dictionary<int,long> materials)
                =>owner.Cultivation.GetTreasureNextLevelUpgradeMaterial(id,hasEmptyItem,useCommonItem,out materials);
            public int CanTreasureUpgrade(int id,Dictionary<int,long> materials,bool autoMerge,bool canUseCommonMaterial)
                =>owner.Cultivation.CanTreasureUpgrade(id,materials,autoMerge,canUseCommonMaterial);
            public TreasureInfo GetTreasure(int id)=>owner.Cultivation.GetTreasure(id);
        }
    }
}
