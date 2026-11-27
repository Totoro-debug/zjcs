// Original treasure plan state transitions with explicit player/request and logging ports.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using TreasureSlotItemData = Zjcs.Official.TreasureSlots.PlayerTreasureSlotItemDataWrap;
using TreasureSlotData = Zjcs.Official.TreasureSlots.PlayerTreasureSlotDataWrap;

namespace Zjcs.Official.TreasureSlots
{
    public static class TreasurePlanDiagnostics { public static Action<Exception> ReportObserverError; }
    public sealed class GAction<T1, T2>
{
	private sealed class ObserverNode : IDisposable
	{
		public readonly Action<T1, T2> Observer;

		private GAction<T1, T2>? mParent;

		public ulong Version;

		public ObserverNode? Previous { get; set; }

		public ObserverNode? Next { get; set; }

		public bool IsDisposed => mParent == null;

		public ObserverNode(GAction<T1, T2> parent, Action<T1, T2> observer, ulong version)
		{
			mParent = parent;
			Observer = observer;
			Version = version;
			lock (mParent)
			{
				if (mParent.mRoot == null)
				{
					Volatile.Write(ref mParent.mRoot, this);
					return;
				}
				ObserverNode observerNode = mParent.mRoot.Previous ?? mParent.mRoot;
				observerNode.Next = this;
				Previous = observerNode;
				mParent.mRoot.Previous = this;
			}
		}

		public void Dispose()
		{
			GAction<T1, T2> gAction = Interlocked.Exchange(ref mParent, null);
			if (gAction == null)
			{
				return;
			}
			lock (gAction)
			{
				if (this == gAction.mRoot)
				{
					if (Previous == null || Next == null)
					{
						gAction.mRoot = null;
						return;
					}
					ObserverNode next = Next;
					if (next.Next == null)
					{
						next.Previous = null;
					}
					else
					{
						next.Previous = Previous;
					}
					gAction.mRoot = next;
				}
				else
				{
					Previous.Next = Next;
					if (Next != null)
					{
						Next.Previous = Previous;
					}
					else
					{
						gAction.mRoot.Previous = Previous;
					}
				}
			}
		}
	}

	private ObserverNode? mRoot;

	private uint mVersion;

	public static GAction<T1, T2> operator +(GAction<T1, T2> gAction, Action<T1, T2> action)
	{
		gAction.Subscribe(action);
		return gAction;
	}

	public static GAction<T1, T2> operator -(GAction<T1, T2> gAction, Action<T1, T2> action)
	{
		for (ObserverNode observerNode = gAction.mRoot; observerNode != null; observerNode = observerNode?.Next)
		{
			if ((Delegate)(observerNode?.Observer) == (Delegate)action)
			{
				observerNode.Dispose();
				break;
			}
		}
		return gAction;
	}

	public static implicit operator GAction<T1, T2>(Action<T1, T2> action)
	{
		GAction<T1, T2> gAction = new GAction<T1, T2>();
		if (action != null)
		{
			gAction.Subscribe(action);
		}
		return gAction;
	}

	public void Invoke(T1 param1, T2 param2)
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				next.Observer(param1, param2);
			}
			next = next.Next;
		}
	}

	public void SafeInvoke(T1 param1, T2 param2)
	{
		uint version = GetVersion();
		ObserverNode next = mRoot;
		while (next != null && next.Version <= version)
		{
			if (!next.IsDisposed)
			{
				try
				{
					next.Observer(param1, param2);
				}
				catch (Exception exception)
				{
					TreasurePlanDiagnostics.ReportObserverError?.Invoke(exception);
				}
			}
			next = next.Next;
		}
	}

	public IDisposable Subscribe(Action<T1, T2> handler)
	{
		return new ObserverNode(this, handler, mVersion);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint GetVersion()
	{
		if (mVersion == uint.MaxValue)
		{
			ResetAllObserverVersion();
			return 0u;
		}
		return mVersion++;
		void ResetAllObserverVersion()
		{
			lock (this)
			{
				for (ObserverNode next = mRoot; next != null; next = next.Next)
				{
					next.Version = 0uL;
				}
				mVersion = 1u;
			}
		}
	}
}
    public sealed class PlayerApplyBattlePlanRequest { public BattlePlanType PlanType; public int PlanIndex; }
    public sealed class PlayerClearBattlePlanRequest { public BattlePlanType PlanType; public int PlanIndex; }
    public sealed partial class Player
    {
        public GAction<BattlePlanType,int> OnBattlePlanContentCleared = new GAction<BattlePlanType,int>();
        public bool TryGetPlanContent(BattlePlanType type,int index,out PlayerBattlePlanContentBaseWrap content)
        {
            content=null;
            if(type!=BattlePlanType.Treasure || index<0 || index>=settings.TreasurePlanMaxCount || !Plans.TryGetValue(index,out var row)) return false;
            content=row;return row!=null;
        }
        // The local model owns the Treasure facet of BattlePlanAccessor's active-index dictionary.
        public bool SetActivePlanIndex(BattlePlanType type,int index)
        {
            if(type!=BattlePlanType.Treasure || index<0 || index>=settings.TreasurePlanMaxCount) return false;
            ActivePlanIndex=index;return true;
        }
        public void FireOnBattlePlanContentCleared(BattlePlanType planType, int planIndex)
	{
		OnBattlePlanContentCleared.SafeInvoke(planType, planIndex);
	}
    }
    public static partial class PlayerTreasurePlanExt { public static bool TryGetTreasurePlanSlots(this Player player, int planIndex, out Dictionary<ElementType, Dictionary<int, int>> slots)
	{
		slots = new Dictionary<ElementType, Dictionary<int, int>>();
		if (!player.TryGetPlanContent(BattlePlanType.Treasure, planIndex, out var content))
		{
			return false;
		}
		if (!(content is PlayerBattlePlanTreasurePlanContentWrap content2))
		{
			player.ReportError?.Invoke("[BattlePlan] 古遗物方案内容类型不符 playerId:");
			return false;
		}
		ParseTreasurePlanSlots(content2, out slots);
		return true;
	}

public static void ParseTreasurePlanSlots(PlayerBattlePlanTreasurePlanContentWrap content, out Dictionary<ElementType, Dictionary<int, int>> slots)
	{
		slots = new Dictionary<ElementType, Dictionary<int, int>>();
		if (content == null)
		{
			return;
		}
		Dictionary<ElementType, Dictionary<int, int>> treasureSlots = content.Data.TreasureSlots;
		if (treasureSlots == null)
		{
			return;
		}
		foreach (KeyValuePair<ElementType, Dictionary<int, int>> item in treasureSlots)
		{
			if (item.Value == null)
			{
				continue;
			}
			Dictionary<int, int> dictionary = null;
			foreach (KeyValuePair<int, int> item2 in item.Value)
			{
				if (item2.Value != 0)
				{
					if (dictionary == null)
					{
						dictionary = new Dictionary<int, int>();
					}
					dictionary[item2.Key] = item2.Value;
				}
			}
			if (dictionary != null)
			{
				slots[item.Key] = dictionary;
			}
		}
	}

public static bool HasTreasurePlanSlot(Dictionary<ElementType, Dictionary<int, int>> rawSlots)
	{
		if (rawSlots == null)
		{
			return false;
		}
		foreach (Dictionary<int, int> value in rawSlots.Values)
		{
			if (value == null)
			{
				continue;
			}
			foreach (int value2 in value.Values)
			{
				if (value2 != 0)
				{
					return true;
				}
			}
		}
		return false;
	}

public static Dictionary<int, int> ReadWornTreasureSlots(this Player player, ElementType elementType)
	{
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		if (!player.TryGetWornTreasureSlots(elementType, out var wornSlots) || wornSlots == null)
		{
			return dictionary;
		}
		foreach (KeyValuePair<int, TreasureSlotItemData> item in wornSlots)
		{
			if (item.Value != null && item.Value.TreasureClassId != 0)
			{
				dictionary[item.Key] = item.Value.TreasureClassId;
			}
		}
		return dictionary;
	}

public static void ApplyWornTreasureTargetState(this Player player, Dictionary<ElementType, Dictionary<int, int>> targetSlots)
	{

		if (targetSlots == null)
		{
			player.ReportError?.Invoke("[BattlePlan] 古遗物目标槽位表为空，放弃应用 playerId:");
			return;
		}
		int treasureMaxSlotCount = player.Settings.TreasureMaxSlotCount;
		bool flag = false;
		foreach (ElementType treasureSlotElementType in player.Settings.TreasureSlotElementTypes)
		{
			targetSlots.TryGetValue(treasureSlotElementType, out var value);
			if ((!player.TryGetWornTreasureSlots(treasureSlotElementType, out var wornSlots) || wornSlots.Count <= 0) && (value == null || value.Count == 0))
			{
				continue;
			}
			PlayerTreasureElementSlotsDataWrap elementSlotData = player.TreasureSlotData.GetElementSlotData(treasureSlotElementType);
			if (elementSlotData == null)
			{
				player.ReportError?.Invoke("[BattlePlan] 古遗物方案应用失败：元素槽位数据不可用 playerId:");
				continue;
			}
			for (int i = 0; i < treasureMaxSlotCount; i++)
			{
				int value2 = 0;
				value?.TryGetValue(i, out value2);
				PlayerTreasureSlotItemDataWrap treasureSlotByIndex = elementSlotData.GetTreasureSlotByIndex(i);
				if (treasureSlotByIndex == null)
				{
					if (value2 != 0)
					{
						elementSlotData.InsertTreasureToSlot(value2, i);
						flag = true;
					}
				}
				else if (treasureSlotByIndex.TreasureClassId != value2)
				{
					elementSlotData.InsertTreasureToSlot(value2, i);
					flag = true;
				}
			}
		}
		if (flag)
		{
			player.FireOnSetupTreasureSlot();
		}
	}

public static int RemoveTreasureFromAllPlans(this Player player, int treasureClassId)
	{
		if (treasureClassId == 0)
		{
			return 0;
		}
		int num = 0;
		List<int> list = new List<int>();
		List<ElementType> list2 = new List<ElementType>();
		int treasurePlanMaxCount = player.Settings.TreasurePlanMaxCount;
		for (int i = 0; i < treasurePlanMaxCount; i++)
		{
			if (!player.TryGetPlanContent(BattlePlanType.Treasure, i, out var content) || !(content is PlayerBattlePlanTreasurePlanContentWrap playerBattlePlanTreasurePlanContentWrap))
			{
				continue;
			}
			Dictionary<ElementType, Dictionary<int, int>> treasureSlots = playerBattlePlanTreasurePlanContentWrap.Data.TreasureSlots;
			if (treasureSlots == null || treasureSlots.Count == 0)
			{
				continue;
			}
			list2.Clear();
			foreach (KeyValuePair<ElementType, Dictionary<int, int>> item in treasureSlots)
			{
				if (item.Value == null)
				{
					continue;
				}
				list.Clear();
				foreach (KeyValuePair<int, int> item2 in item.Value)
				{
					if (item2.Value == treasureClassId)
					{
						list.Add(item2.Key);
					}
				}
				if (list.Count == 0)
				{
					continue;
				}
				PlayerBattlePlanTreasureSlotsSubValueWrap playerBattlePlanTreasureSlotsSubValueWrap = playerBattlePlanTreasurePlanContentWrap.TreasureSlots[item.Key];
				foreach (int item3 in list)
				{
					playerBattlePlanTreasureSlotsSubValueWrap.Remove(item3);
					num++;
				}
				if (playerBattlePlanTreasureSlotsSubValueWrap.Count == 0)
				{
					list2.Add(item.Key);
				}
			}
			foreach (ElementType item4 in list2)
			{
				playerBattlePlanTreasurePlanContentWrap.TreasureSlots.Remove(item4);
			}
		}
		return num;
	}

public static bool ClearPlanContent(this Player player, BattlePlanType planType, int planIndex)
	{
		if (!player.TryGetPlanContent(planType, planIndex, out var content))
		{
			return false;
		}
		content.Clear();
		player.FireOnBattlePlanContentCleared(planType, planIndex);
		return true;
	} }
    public static class TreasurePlanCallbacks { public static void OperPlayerApplyBattlePlanCallback(Player player, PlayerApplyBattlePlanRequest req, int ec)
	{
		if (ec != 0)
		{
			return;
		}

		switch (req.PlanType)
		{
		case BattlePlanType.Treasure:
		{
			if (!player.TryGetTreasurePlanSlots(req.PlanIndex, out var slots))
			{
				player.ReportError?.Invoke("[BattlePlan] ");
			}
			else
			{
				player.SetActivePlanIndex(req.PlanType, req.PlanIndex);
				player.ApplyWornTreasureTargetState(slots);
			}
			break;
		}
		}
	}

public static void OperPlayerClearBattlePlanCallback(Player player, PlayerClearBattlePlanRequest req, int ec)
	{
		if (ec == 0)
		{
			player.ClearPlanContent(req.PlanType, req.PlanIndex);
			ClearWornWhenActivePlanCleared(player, req.PlanType, req.PlanIndex);
		}
	}

private static void ClearWornWhenActivePlanCleared(Player player, BattlePlanType planType, int planIndex)
	{
		if (planIndex == player.GetActivePlanIndex(planType))
		{
			switch (planType)
			{
			case BattlePlanType.Treasure:
				player.ApplyWornTreasureTargetState(new Dictionary<ElementType, Dictionary<int, int>>());
				break;
			}
		}
	} }
}
