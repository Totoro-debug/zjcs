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
        public int GetFinalLevel()
	{
		return BaseLevel + BlessLevel;
	}

public int AddBaseLevel(int addLevel)
	{
		BaseLevel += addLevel;
		return BaseLevel;
	}

public int AddBlessLevel(int addLevel)
	{
		BlessLevel += addLevel;
		return BlessLevel;
	}
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
        public int GetShareLevel()
	{
		return Math.Max(1, ShareLevel);
	}

public int GetBaseShareLevel()
	{
		return Math.Max(1, BaseShareLevel);
	}

public void SetShareLevel(int level)
	{
		ShareLevel = Math.Max(1, level);
	}

public void SetBaseShareLevel(int level)
	{
		BaseShareLevel = Math.Max(1, level);
	}

public void InsertTreasureToSlot(int treasureClassId, int index)
	{
		int treasureMaxSlotCount = settings.TreasureMaxSlotCount;
		if (index >= treasureMaxSlotCount)
		{
			reportError?.Invoke("古遗物装备到槽位时出现错误，下标超出最大可设置范围！treasureClassId = ");
		}
		else
		{
			if (!TreasureSlots.TryGetValue(index, out var value))
			{
				value = new TreasureSlotItemData();
				TreasureSlots.Add(index, value);
			}
			value.TreasureClassId = treasureClassId;
		}
	}

public PlayerTreasureSlotItemDataWrap GetTreasureSlotByIndex(int index)
	{
		TreasureSlots.TryGetValue(index, out var value);
		return value;
	}

public void UpdateTreasureSlotsShareLevel()
	{
		int treasureMaxSlotCount = settings.TreasureMaxSlotCount;
		if (TreasureSlots.Count < treasureMaxSlotCount)
		{
			return;
		}
		int? num = null;
		int? num2 = null;
		foreach (PlayerTreasureSlotItemDataWrap value in TreasureSlots.Values)
		{
			if (value != null)
			{
				int finalLevel = value.GetFinalLevel();
				if (!num.HasValue || finalLevel < num)
				{
					num = finalLevel;
				}
				int baseLevel = value.BaseLevel;
				if (!num2.HasValue || baseLevel < num2)
				{
					num2 = baseLevel;
				}
			}
		}
		if (num.HasValue)
		{
			SetShareLevel(num.Value);
		}
		if (num2.HasValue)
		{
			SetBaseShareLevel(num2.Value);
		}
	}
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
        public PlayerTreasureSlotItemDataWrap GetTreasureInSlots(int treasureClassId, out int index)
	{
		index = -1;
		foreach (PlayerTreasureElementSlotsDataWrap value in TreasureSlotDataDict.Values)
		{
			if (value == null || value.TreasureSlots.Count <= 0)
			{
				continue;
			}
			foreach (KeyValuePair<int, PlayerTreasureSlotItemDataWrap> treasureSlot in value.TreasureSlots)
			{
				if (treasureSlot.Value != null && treasureSlot.Value.TreasureClassId == treasureClassId)
				{
					index = treasureSlot.Key;
					return treasureSlot.Value;
				}
			}
		}
		return null;
	}

public int GetElementTreasureShareLevel(ElementType elementType)
	{
		return GetElementSlotData(elementType)?.GetShareLevel() ?? 1;
	}

public int GetElementTreasureBaseShareLevel(ElementType elementType)
	{
		return GetElementSlotData(elementType)?.GetBaseShareLevel() ?? 1;
	}

public PlayerTreasureElementSlotsDataWrap GetElementSlotData(ElementType type)
	{
		if (!settings.TreasureSlotElementTypes.Contains(type))
		{
			return null;
		}
		if (!TreasureSlotDataDict.TryGetValue(type, out var value))
		{
			value = PlayerTreasureElementSlotsDataWrap.CreateNew(settings, reportError);
			value.ElementType = type;
			TreasureSlotDataDict.Add(type, value);
		}
		return value;
	}

public void ClearAllBlessLevel()
	{
		foreach (PlayerTreasureElementSlotsDataWrap value in TreasureSlotDataDict.Values)
		{
			foreach (PlayerTreasureSlotItemDataWrap value2 in value.TreasureSlots.Values)
			{
				value2.BlessLevel = 0;
			}
			value.UpdateTreasureSlotsShareLevel();
		}
	}

public bool SetupTreasureInSlot(int treasureClassId, ElementType elementType, int originIndex, int targetIndex)
	{
		PlayerTreasureElementSlotsDataWrap elementSlotData = GetElementSlotData(elementType);
		elementSlotData.GetTreasureSlotByIndex(originIndex);
		PlayerTreasureSlotItemDataWrap treasureSlotByIndex = elementSlotData.GetTreasureSlotByIndex(targetIndex);
		if (originIndex >= 0)
		{
			if (treasureSlotByIndex != null)
			{
				elementSlotData.InsertTreasureToSlot(treasureSlotByIndex.TreasureClassId, originIndex);
			}
			else
			{
				elementSlotData.InsertTreasureToSlot(0, originIndex);
			}
		}
		elementSlotData.InsertTreasureToSlot(treasureClassId, targetIndex);
		return true;
	}

public bool RemoveTreasureInSlot(ElementType type, int index)
	{
		PlayerTreasureElementSlotsDataWrap elementSlotData = GetElementSlotData(type);
		if (elementSlotData == null)
		{
			return false;
		}
		PlayerTreasureSlotItemDataWrap treasureSlotByIndex = elementSlotData.GetTreasureSlotByIndex(index);
		if (treasureSlotByIndex == null)
		{
			return false;
		}
		treasureSlotByIndex.TreasureClassId = 0;
		return true;
	}
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
        public int GetTreasureFinalLevel(int treasureClassId)
	{
		PlayerTreasureSlotItemDataWrap treasureInSlots = TreasureSlotData.GetTreasureInSlots(treasureClassId, out var _);
		if (treasureInSlots != null)
		{
			return treasureInSlots.GetFinalLevel();
		}
		PlayerItemDataWrap itemById = this.GetItemById(treasureClassId);
		if (itemById == null)
		{
			return 1;
		}
		ElementType elementType = itemById.Info.ElementType;
		return TreasureSlotData.GetElementTreasureShareLevel(elementType);
	}

public int GetTreasureBaseLevel(int treasureClassId)
	{
		PlayerTreasureSlotItemDataWrap treasureInSlots = TreasureSlotData.GetTreasureInSlots(treasureClassId, out var _);
		if (treasureInSlots != null)
		{
			return treasureInSlots.BaseLevel;
		}
		PlayerItemDataWrap itemById = this.GetItemById(treasureClassId);
		if (itemById == null)
		{
			return 1;
		}
		ElementType elementType = itemById.Info.ElementType;
		return TreasureSlotData.GetElementTreasureBaseShareLevel(elementType);
	}

public int GetTreasureSlotFinalLevel(ElementType elementType, int slotIndex)
	{
		return GetTreasureSlotData(elementType, slotIndex)?.GetFinalLevel() ?? 1;
	}

public int GetSlotTreasureBaseLevel(int treasureClassId)
	{
		int index;
		return TreasureSlotData.GetTreasureInSlots(treasureClassId, out index)?.BaseLevel ?? 1;
	}

public PlayerTreasureElementSlotsDataWrap GetTreasureElementSlotData(ElementType type)
	{
		return TreasureSlotData.GetElementSlotData(type);
	}

public int GetTreasureInSlotIndex(int treasureClassId)
	{
		TreasureSlotData.GetTreasureInSlots(treasureClassId, out var index);
		return index;
	}

public PlayerTreasureSlotItemDataWrap GetTreasureSlotDataAndIndex(int treasureClassId, out int index)
	{
		return TreasureSlotData.GetTreasureInSlots(treasureClassId, out index);
	}

public PlayerTreasureSlotItemDataWrap GetTreasureSlotData(ElementType elementType, int slotIndex)
	{
		return TreasureSlotData.GetElementSlotData(elementType)?.GetTreasureSlotByIndex(slotIndex);
	}

public PlayerTreasureSlotItemDataWrap GetTreasureSlotData(int treasureClassId)
	{
		int index;
		return TreasureSlotData.GetTreasureInSlots(treasureClassId, out index);
	}

public bool IsTreasureInSlot(int treasureClassId)
	{
		return GetTreasureSlotData(treasureClassId) != null;
	}

public bool SetupTreasureInSlot(int treasureClassId, ElementType elementType, int originIndex, int targetIndex)
	{
		bool flag = TreasureSlotData.SetupTreasureInSlot(treasureClassId, elementType, originIndex, targetIndex);
		if (flag)
		{
			FireOnSetupTreasureSlot();
		}
		return flag;
	}

public bool RemoveTreasureInSlot(ElementType type, int index)
	{
		return TreasureSlotData.RemoveTreasureInSlot(type, index);
	}

public int TreasureSlotBaseLevelUp(ElementType elementType, int slotIndex, int addLevel)
	{
		PlayerTreasureSlotItemDataWrap treasureSlotData = GetTreasureSlotData(elementType, slotIndex);
		if (treasureSlotData == null)
		{
			return 1;
		}
		int result = treasureSlotData.AddBaseLevel(addLevel);
		UpdateTreasureSlotShareLevel(elementType);
		return result;
	}

private void UpdateTreasureSlotShareLevel(ElementType elementType)
	{
		GetTreasureElementSlotData(elementType)?.UpdateTreasureSlotsShareLevel();
	}

public int GetTreasureSlotShareLevel(ElementType type)
	{
		return TreasureSlotData.GetElementTreasureShareLevel(type);
	}

public int GetSlotTreasureLevel(int treasureClassId)
	{
		foreach (PlayerTreasureElementSlotsDataWrap value in TreasureSlotData.TreasureSlotDataDict.Values)
		{
			if (value == null)
			{
				continue;
			}
			foreach (PlayerTreasureSlotItemDataWrap value2 in value.TreasureSlots.Values)
			{
				if (value2 != null && value2.TreasureClassId == treasureClassId)
				{
					return value2.BaseLevel;
				}
			}
		}
		return 1;
	}

public int GetCanSetupTreasureSlotIndex(ElementType elementType)
	{
		PlayerTreasureElementSlotsDataWrap treasureElementSlotData = GetTreasureElementSlotData(elementType);
		if (treasureElementSlotData.TreasureSlots.Count >= settings.TreasureMaxSlotCount)
		{
			return -1;
		}
		for (int i = 0; i < settings.TreasureMaxSlotCount; i++)
		{
			if (!treasureElementSlotData.TreasureSlots.TryGetValue(i, out var _))
			{
				return i;
			}
		}
		return -1;
	}

public void FireOnSetupTreasureSlot()
	{
		OnSetupTreasureSlot?.Invoke();
	}
    }
    public static partial class PlayerTreasurePlanExt
    {
        public static void WriteWornTreasureElementToActivePlan(this Player player, ElementType elementType)
	{
		int activePlanIndex = player.GetActivePlanIndex(BattlePlanType.Treasure);

		if (!(player.GetOrCreatePlanContent(BattlePlanType.Treasure, activePlanIndex) is PlayerBattlePlanTreasurePlanContentWrap { TreasureSlots: var treasureSlots }))
		{
			player.ReportError?.Invoke("[BattlePlan] 手动装配写回失败：取不到激活方案 playerId:");
			return;
		}
		if (treasureSlots == null)
		{
			player.ReportError?.Invoke("[BattlePlan] 手动装配写回失败：方案内容槽位表为空 playerId:");
			return;
		}
		if (treasureSlots.RawData == null || !treasureSlots.RawData.ContainsKey(elementType))
		{
			treasureSlots.Add(elementType, new Dictionary<int, int>());
		}
		PlayerBattlePlanTreasureSlotsSubValueWrap playerBattlePlanTreasureSlotsSubValueWrap = treasureSlots[elementType];
		playerBattlePlanTreasureSlotsSubValueWrap.Clear();
		int treasureMaxSlotCount = player.Settings.TreasureMaxSlotCount;
		for (int i = 0; i < treasureMaxSlotCount; i++)
		{
			int wornTreasureClassId = player.GetWornTreasureClassId(elementType, i);
			if (wornTreasureClassId != 0)
			{
				playerBattlePlanTreasureSlotsSubValueWrap[i] = wornTreasureClassId;
			}
		}
		if (playerBattlePlanTreasureSlotsSubValueWrap.Count == 0)
		{
			treasureSlots.Remove(elementType);
		}
	}

private static bool TryGetWornTreasureSlots(this Player player, ElementType elementType, out Dictionary<int, TreasureSlotItemData> wornSlots)
	{
		wornSlots = null;
		TreasureSlotData rawData = player.TreasureSlotData.RawData;
		if (rawData == null || rawData.TreasureSlotDataDict == null)
		{
			return false;
		}
		if (!rawData.TreasureSlotDataDict.TryGetValue(elementType, out var value) || value == null)
		{
			return false;
		}
		wornSlots = value.TreasureSlots;
		return wornSlots != null;
	}

private static int GetWornTreasureClassId(this Player player, ElementType elementType, int slotIndex)
	{
		if (!player.TryGetWornTreasureSlots(elementType, out var wornSlots))
		{
			return 0;
		}
		if (!wornSlots.TryGetValue(slotIndex, out var value) || value == null)
		{
			return 0;
		}
		return value.TreasureClassId;
	}
    }
    public static partial class TreasureHelper
    {
        public static bool CanSetupTreasureSlot(Player player, int treasureClassId, int slotIndex)
	{
		int treasureInSlotIndex = player.GetTreasureInSlotIndex(treasureClassId);
		if (treasureInSlotIndex != -1 && treasureInSlotIndex == slotIndex)
		{
			return false;
		}
		return true;
	}
    }
}
