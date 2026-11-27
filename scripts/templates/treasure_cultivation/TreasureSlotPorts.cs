// Storage/parser/plan ports for verbatim original treasure slot methods.
using System;
using System.Collections.Generic;
using TreasureSlotItemData = Zjcs.Official.TreasureSlots.PlayerTreasureSlotItemDataWrap;
using TreasureSlotData = Zjcs.Official.TreasureSlots.PlayerTreasureSlotDataWrap;
using PlayerItemDataWrap = Zjcs.Official.TreasureSlots.SlotItem;

namespace Zjcs.Official.TreasureSlots
{
    public enum ElementType { None = 0 }
    public enum BattlePlanType { Treasure = 3 }
    public sealed class TreasureSlotSettings
    {
        public int TreasureMaxSlotCount, TreasurePlanMaxCount;
        public List<ElementType> TreasureSlotElementTypes;
    }
    public sealed class SlotItemInfo { public ElementType ElementType; }
    public sealed class SlotItem { public int Id; public long Count; public SlotItemInfo Info; }

    public class PlayerTreasureSlotItemDataWrap
    {
        public int TreasureClassId, BaseLevel = 1, BlessLevel;
        /*PlayerTreasureSlotItemDataWrap*/
    }

    public sealed class PlayerTreasureElementSlotsDataWrap
    {
        private readonly TreasureSlotSettings settings;
        private readonly Action<string> reportError;
        public ElementType ElementType;
        public int ShareLevel = 1, BaseShareLevel = 1;
        public Dictionary<int, PlayerTreasureSlotItemDataWrap> TreasureSlots = new Dictionary<int, PlayerTreasureSlotItemDataWrap>();
        public PlayerTreasureElementSlotsDataWrap(TreasureSlotSettings settings, Action<string> reportError)
        { this.settings = settings; this.reportError = reportError; }
        public static PlayerTreasureElementSlotsDataWrap CreateNew(TreasureSlotSettings settings, Action<string> reportError)
            => new PlayerTreasureElementSlotsDataWrap(settings, reportError);
        /*PlayerTreasureElementSlotsDataWrap*/
    }

    public sealed class PlayerTreasureSlotDataWrap
    {
        private readonly TreasureSlotSettings settings;
        private readonly Action<string> reportError;
        public int SeasonId;
        public bool UpgradeUseCommonMaterailEnable, AwakeAutoMergeEnable;
        public Dictionary<ElementType, PlayerTreasureElementSlotsDataWrap> TreasureSlotDataDict = new Dictionary<ElementType, PlayerTreasureElementSlotsDataWrap>();
        public PlayerTreasureSlotDataWrap RawData => this;
        public PlayerTreasureSlotDataWrap(TreasureSlotSettings settings, Action<string> reportError = null)
        { this.settings = settings; this.reportError = reportError; }
        /*PlayerTreasureSlotDataWrap*/
    }

    public sealed class TreasurePlanContent
    {
        public Dictionary<ElementType, Dictionary<int, int>> TreasureSlots = new Dictionary<ElementType, Dictionary<int, int>>();
    }
    public sealed class PlayerBattlePlanTreasureSlotsSubValueWrap
    {
        private readonly Dictionary<int, int> slots;
        public PlayerBattlePlanTreasureSlotsSubValueWrap(Dictionary<int, int> slots) { this.slots = slots; }
        public int this[int key] { get => slots[key]; set => slots[key] = value; }
        public int Count => slots.Count;
        public void Clear() => slots.Clear();
        public bool Remove(int index) => slots.Remove(index);
    }
    public sealed class PlayerBattlePlanTreasureSlotsWrap
    {
        public Dictionary<ElementType, Dictionary<int, int>> RawData { get; }
        public PlayerBattlePlanTreasureSlotsWrap(Dictionary<ElementType, Dictionary<int, int>> data) { RawData = data; }
        public void Add(ElementType key, Dictionary<int, int> data) => RawData.Add(key, data);
        public bool Remove(ElementType key) => RawData.Remove(key);
        public void Clear() => RawData.Clear();
        public PlayerBattlePlanTreasureSlotsSubValueWrap this[ElementType key] => new PlayerBattlePlanTreasureSlotsSubValueWrap(RawData[key]);
    }
    public abstract class PlayerBattlePlanContentBaseWrap { public abstract void Clear(); }
    public sealed class PlayerBattlePlanTreasurePlanContentWrap : PlayerBattlePlanContentBaseWrap
    {
        public TreasurePlanContent Data = new TreasurePlanContent();
        public PlayerBattlePlanTreasureSlotsWrap TreasureSlots => new PlayerBattlePlanTreasureSlotsWrap(Data.TreasureSlots);
        public override void Clear() { TreasureSlots.Clear(); }
    }

    public sealed partial class Player
    {
        public long Id;
        private readonly TreasureSlotSettings settings;
        private readonly Func<int, SlotItem> getItem;
        public TreasureSlotSettings Settings => settings;
        public readonly Action<string> ReportError;
        public PlayerTreasureSlotDataWrap TreasureSlotData;
        public int ActivePlanIndex;
        public Dictionary<int, PlayerBattlePlanTreasurePlanContentWrap> Plans = new Dictionary<int, PlayerBattlePlanTreasurePlanContentWrap>();
        public event Action OnSetupTreasureSlot;
        public Player(TreasureSlotSettings settings, Func<int, SlotItem> getItem, Action<string> reportError = null)
        {
            this.settings = settings; this.getItem = getItem; ReportError = reportError;
            TreasureSlotData = new PlayerTreasureSlotDataWrap(settings, reportError);
        }
        public SlotItem GetItemById(int id) => getItem(id);
        public int GetActivePlanIndex(BattlePlanType type) => ActivePlanIndex;
        public object GetOrCreatePlanContent(BattlePlanType type, int index)
        {
            if (index < 0 || index >= settings.TreasurePlanMaxCount) return null;
            if (!Plans.TryGetValue(index, out var value)) Plans.Add(index, value = new PlayerBattlePlanTreasurePlanContentWrap());
            return value;
        }
        /*Player*/
    }
    public static partial class PlayerTreasurePlanExt
    {
        /*PlayerTreasurePlanExt*/
    }
    public static partial class TreasureHelper
    {
        /*TreasureHelper*/
    }
}
