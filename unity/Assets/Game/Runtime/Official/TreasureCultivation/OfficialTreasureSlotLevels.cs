// Original slot cultivation methods with explicit table, player and record ports.
using System;
using System.Collections.Generic;
using System.Linq;
using PlayerItemDataWrap = Zjcs.Official.TreasureSlots.SlotItem;
using PlayerTreasureBlessRecordData = Zjcs.Official.TreasureSlots.PlayerSeasonRecordPlayerTreasureBlessRecordDataWrap;
using ItemType = System.Int32;

namespace Zjcs.Official.TreasureSlots
{
    public static class TreasureLevelDiagnostics { public static Action<string> ReportError; }


public enum Rank
{
	None,
	Norank,
	Blackiron,
	Bronze,
	Silver,
	Gold,
	Saint,
	Legend,
	Angel,
	Godtouched1,
	Godtouched2,
	Godtouched3,
	Demigod1,
	Demigod2,
	Demigod3,
	Stargod1,
	Stargod2,
	Stargod3,
	Lordgod1,
	Lordgod2,
	Lordgod3,
	Eternal1,
	Eternal2,
	Eternal3,
	Chaos1,
	Chaos2,
	Chaos3,
	Nihility1,
	Nihility2,
	Nihility3
}



public enum SubRank
{
	None,
	Norank,
	Blackiron1,
	Blackiron2,
	Blackiron3,
	Bronze1,
	Bronze2,
	Bronze3,
	Silver1,
	Silver2,
	Silver3,
	Gold1,
	Gold2,
	Gold3,
	Saint1,
	Saint2,
	Saint3,
	Legend1,
	Legend2,
	Legend3,
	Angel1,
	Angel2,
	Angel3,
	Godtouched1_1,
	Godtouched1_2,
	Godtouched1_3,
	Godtouched2_1,
	Godtouched2_2,
	Godtouched2_3,
	Godtouched3_1,
	Godtouched3_2,
	Godtouched3_3,
	Demigod1_1,
	Demigod1_2,
	Demigod1_3,
	Demigod2_1,
	Demigod2_2,
	Demigod2_3,
	Demigod3_1,
	Demigod3_2,
	Demigod3_3,
	Stargod1_1,
	Stargod1_2,
	Stargod1_3,
	Stargod2_1,
	Stargod2_2,
	Stargod2_3,
	Stargod3_1,
	Stargod3_2,
	Stargod3_3,
	Lordgod1_1,
	Lordgod1_2,
	Lordgod1_3,
	Lordgod2_1,
	Lordgod2_2,
	Lordgod2_3,
	Lordgod3_1,
	Lordgod3_2,
	Lordgod3_3,
	Eternal1_1,
	Eternal1_2,
	Eternal1_3,
	Eternal2_1,
	Eternal2_2,
	Eternal2_3,
	Eternal3_1,
	Eternal3_2,
	Eternal3_3,
	Chaos1_1,
	Chaos1_2,
	Chaos1_3,
	Chaos2_1,
	Chaos2_2,
	Chaos2_3,
	Chaos3_1,
	Chaos3_2,
	Chaos3_3,
	Nihility1_1,
	Nihility1_2,
	Nihility1_3,
	Nihility2_1,
	Nihility2_2,
	Nihility2_3,
	Nihility3_1,
	Nihility3_2,
	Nihility3_3
}



public enum DevelopType
{
	Equip,
	Skill,
	Treasure,
	Pet
}


    public sealed class SystemLevelLimitInfo
    {
        public SubRank SubRank;
        public int PlayerLevel, PlayerBlessLevel, TreasureLevel, TreasureBlessLevel;
    }
    public sealed class SystemLevelLimitInfoParser
    {
        private readonly SystemLevelLimitInfo[] configs;
        private readonly Dictionary<SubRank, Dictionary<int, Dictionary<int, SystemLevelLimitInfo>>> mLevelLimitCaches = new Dictionary<SubRank, Dictionary<int, Dictionary<int, SystemLevelLimitInfo>>>();
        private readonly Dictionary<SubRank,int> mTreasureBaseLevelLimitByRank = new Dictionary<SubRank,int>();
        private readonly Dictionary<SubRank,int> mTreasureBlessLevelLimitByRank = new Dictionary<SubRank,int>();
        public SystemLevelLimitInfoParser(IEnumerable<SystemLevelLimitInfo> rows)
        { configs=rows.ToArray();foreach(var row in configs) { SetInfo(row); SetTreasureMaxLevelLimit(row); } }
        private void SetInfo(SystemLevelLimitInfo info)
	{
		SubRank subRank = info.SubRank;
		int playerLevel = info.PlayerLevel;
		int playerBlessLevel = info.PlayerBlessLevel;
		if (!mLevelLimitCaches.TryGetValue(subRank, out var value))
		{
			value = (mLevelLimitCaches[subRank] = new Dictionary<int, Dictionary<int, SystemLevelLimitInfo>>());
		}
		if (!value.TryGetValue(playerLevel, out var value2))
		{
			value2 = (mLevelLimitCaches[subRank][playerLevel] = new Dictionary<int, SystemLevelLimitInfo>());
		}
		mLevelLimitCaches[subRank][playerLevel][playerBlessLevel] = info;
	}

private void SetTreasureMaxLevelLimit(SystemLevelLimitInfo info)
	{
		SubRank subRank = info.SubRank;
		int value2;
		if (info.TreasureBlessLevel == 0)
		{
			if (!mTreasureBaseLevelLimitByRank.TryGetValue(subRank, out var value))
			{
				mTreasureBaseLevelLimitByRank[subRank] = info.TreasureLevel;
			}
			else
			{
				mTreasureBaseLevelLimitByRank[subRank] = Math.Max(value, info.TreasureLevel);
			}
		}
		else if (!mTreasureBlessLevelLimitByRank.TryGetValue(subRank, out value2))
		{
			mTreasureBlessLevelLimitByRank[subRank] = info.TreasureBlessLevel;
		}
		else
		{
			mTreasureBlessLevelLimitByRank[subRank] = Math.Max(value2, info.TreasureBlessLevel);
		}
	}

public SystemLevelLimitInfo GetLevelLimitInfo(SubRank rank, int playerBaseLevel, int playerBlessLevel)
	{
		if (!mLevelLimitCaches.TryGetValue(rank, out var value))
		{
			return null;
		}
		if (!value.TryGetValue(playerBaseLevel, out var value2))
		{
			return null;
		}
		value2.TryGetValue(playerBlessLevel, out var value3);
		return value3;
	}

public int GetTreasureBaseLevelLimit(SubRank subRank)
	{
		if (mTreasureBaseLevelLimitByRank.TryGetValue(subRank, out var value))
		{
			return value;
		}
		return 0;
	}

public int GetTreasureBlessLevelLimit(SubRank subRank)
	{
		if (mTreasureBlessLevelLimitByRank.TryGetValue(subRank, out var value))
		{
			return value;
		}
		return 0;
	}

public int GetMinSystemLimitInfo(int totalLevel, SubRank playerSubRank, int playerLevel, Rank serverRank, Func<SystemLevelLimitInfo, int> getTotalSystemLevel, out SystemLevelLimitInfo selectedInfo)
	{
		selectedInfo = null;
		SystemLevelLimitInfo[] array = configs;
		foreach (SystemLevelLimitInfo systemLevelLimitInfo in array)
		{
			if (systemLevelLimitInfo.SubRank >= playerSubRank && systemLevelLimitInfo.PlayerLevel + systemLevelLimitInfo.PlayerBlessLevel >= playerLevel && getTotalSystemLevel(systemLevelLimitInfo) >= totalLevel)
			{
				selectedInfo = systemLevelLimitInfo;
				Rank rank = systemLevelLimitInfo.SubRank.ToRank();
				if (rank > serverRank)
				{
					return 61804;
				}
				if (rank > playerSubRank.ToRank())
				{
					return 61802;
				}
				return 61801;
			}
		}
		return 61804;
	}
    }
    public sealed class SystemLevelGapLimitInfo
    {
        public DevelopType DevelopType;
        public int SeasonId, Level, LimitLevel;
        public bool IsOpen;
    }
    public class ParserBase { public virtual void PostParse() {} }
    public sealed class SystemLevelGapLimitInfoParser : ParserBase
    {
        private readonly SystemLevelGapLimitInfo[] configs;
        private readonly Dictionary<DevelopType,Dictionary<int,List<SystemLevelGapLimitInfo>>> mDictionary = new Dictionary<DevelopType,Dictionary<int,List<SystemLevelGapLimitInfo>>>();
        public SystemLevelGapLimitInfoParser(IEnumerable<SystemLevelGapLimitInfo> rows) { configs=rows.ToArray(); PostParse(); }
        public void PostParse()
	{
		SystemLevelGapLimitInfo[] array = configs;
		foreach (SystemLevelGapLimitInfo systemLevelGapLimitInfo in array)
		{
			if (!mDictionary.TryGetValue(systemLevelGapLimitInfo.DevelopType, out var value))
			{
				value = (mDictionary[systemLevelGapLimitInfo.DevelopType] = new Dictionary<int, List<SystemLevelGapLimitInfo>>());
			}
			if (!value.TryGetValue(systemLevelGapLimitInfo.SeasonId, out var value2))
			{
				value2 = (value[systemLevelGapLimitInfo.SeasonId] = new List<SystemLevelGapLimitInfo>());
			}
			value2.Add(systemLevelGapLimitInfo);
		}
	}

public bool TryGetSystemLevelGapInfo(DevelopType developType, int nextLevel, int seasonId, out int levelLimit)
	{
		levelLimit = 0;
		if (!mDictionary.TryGetValue(developType, out var value))
		{
			return false;
		}
		if (!value.TryGetValue(seasonId, out var value2))
		{
			return false;
		}
		for (int i = 0; i < value2.Count; i++)
		{
			SystemLevelGapLimitInfo systemLevelGapLimitInfo = value2[i];
			int level = systemLevelGapLimitInfo.Level;
			if (i == value2.Count - 1 && nextLevel > level)
			{
				levelLimit = systemLevelGapLimitInfo.LimitLevel;
				return systemLevelGapLimitInfo.IsOpen;
			}
			if (nextLevel > level && nextLevel <= value2[i + 1].Level)
			{
				levelLimit = systemLevelGapLimitInfo.LimitLevel;
				return systemLevelGapLimitInfo.IsOpen;
			}
			if (nextLevel <= level)
			{
				return false;
			}
		}
		return false;
	}

public bool IsBlessLevelGapLimit(DevelopType developType, int nextLevel, int minLevel, out int minLevelLimit, int seasonId)
	{
		minLevelLimit = 0;
		if (!TryGetSystemLevelGapInfo(developType, nextLevel, seasonId, out minLevelLimit))
		{
			return false;
		}
		return minLevel < minLevelLimit;
	}
    }
    public sealed class TreasureLevelInfo { public int Level; public Dictionary<int,long> Materials; }
    public sealed class TreasureBlessInfo { public Rank Rank; public int BlessLevel; public Dictionary<int,long> LevelCostItems; }
    public sealed class TreasureLevelSeason { public Rank Rank; public int Season; }
    public sealed class FormulaHandler
    {
        private readonly SystemLevelLimitInfoParser limits;
        public FormulaHandler(SystemLevelLimitInfoParser limits) { this.limits=limits; }
        public int GetMaxTreasureBlessLevel(SubRank subRank, Rank serverRank)
	{
		if (subRank.ToRank() != serverRank)
		{
			return 0;
		}
		return limits.GetTreasureBlessLevelLimit(subRank);
	}
    }
    public static class EnumExt { public static Rank ToRank(this SubRank subRank)
	{
		switch (subRank)
		{
		case SubRank.None:
			return Rank.None;
		case SubRank.Norank:
			return Rank.Norank;
		case SubRank.Blackiron1:
		case SubRank.Blackiron2:
		case SubRank.Blackiron3:
			return Rank.Blackiron;
		case SubRank.Bronze1:
		case SubRank.Bronze2:
		case SubRank.Bronze3:
			return Rank.Bronze;
		case SubRank.Silver1:
		case SubRank.Silver2:
		case SubRank.Silver3:
			return Rank.Silver;
		case SubRank.Gold1:
		case SubRank.Gold2:
		case SubRank.Gold3:
			return Rank.Gold;
		case SubRank.Saint1:
		case SubRank.Saint2:
		case SubRank.Saint3:
			return Rank.Saint;
		case SubRank.Legend1:
		case SubRank.Legend2:
		case SubRank.Legend3:
			return Rank.Legend;
		case SubRank.Angel1:
		case SubRank.Angel2:
		case SubRank.Angel3:
			return Rank.Angel;
		case SubRank.Godtouched1_1:
		case SubRank.Godtouched1_2:
		case SubRank.Godtouched1_3:
			return Rank.Godtouched1;
		case SubRank.Godtouched2_1:
		case SubRank.Godtouched2_2:
		case SubRank.Godtouched2_3:
			return Rank.Godtouched2;
		case SubRank.Godtouched3_1:
		case SubRank.Godtouched3_2:
		case SubRank.Godtouched3_3:
			return Rank.Godtouched3;
		case SubRank.Demigod1_1:
		case SubRank.Demigod1_2:
		case SubRank.Demigod1_3:
			return Rank.Demigod1;
		case SubRank.Demigod2_1:
		case SubRank.Demigod2_2:
		case SubRank.Demigod2_3:
			return Rank.Demigod2;
		case SubRank.Demigod3_1:
		case SubRank.Demigod3_2:
		case SubRank.Demigod3_3:
			return Rank.Demigod3;
		case SubRank.Stargod1_1:
		case SubRank.Stargod1_2:
		case SubRank.Stargod1_3:
			return Rank.Stargod1;
		case SubRank.Stargod2_1:
		case SubRank.Stargod2_2:
		case SubRank.Stargod2_3:
			return Rank.Stargod2;
		case SubRank.Stargod3_1:
		case SubRank.Stargod3_2:
		case SubRank.Stargod3_3:
			return Rank.Stargod3;
		case SubRank.Lordgod1_1:
		case SubRank.Lordgod1_2:
		case SubRank.Lordgod1_3:
			return Rank.Lordgod1;
		case SubRank.Lordgod2_1:
		case SubRank.Lordgod2_2:
		case SubRank.Lordgod2_3:
			return Rank.Lordgod2;
		case SubRank.Lordgod3_1:
		case SubRank.Lordgod3_2:
		case SubRank.Lordgod3_3:
			return Rank.Lordgod3;
		case SubRank.Eternal1_1:
		case SubRank.Eternal1_2:
		case SubRank.Eternal1_3:
			return Rank.Eternal1;
		case SubRank.Eternal2_1:
		case SubRank.Eternal2_2:
		case SubRank.Eternal2_3:
			return Rank.Eternal2;
		case SubRank.Eternal3_1:
		case SubRank.Eternal3_2:
		case SubRank.Eternal3_3:
			return Rank.Eternal3;
		case SubRank.Nihility1_1:
		case SubRank.Nihility1_2:
		case SubRank.Nihility1_3:
			return Rank.Nihility1;
		case SubRank.Nihility2_1:
		case SubRank.Nihility2_2:
		case SubRank.Nihility2_3:
			return Rank.Nihility2;
		case SubRank.Nihility3_1:
		case SubRank.Nihility3_2:
		case SubRank.Nihility3_3:
			return Rank.Nihility3;
		case SubRank.Chaos1_1:
		case SubRank.Chaos1_2:
		case SubRank.Chaos1_3:
			return Rank.Chaos1;
		case SubRank.Chaos2_1:
		case SubRank.Chaos2_2:
		case SubRank.Chaos2_3:
			return Rank.Chaos2;
		case SubRank.Chaos3_1:
		case SubRank.Chaos3_2:
		case SubRank.Chaos3_3:
			return Rank.Chaos3;
		default:
		{
			TreasureLevelDiagnostics.ReportError?.Invoke("ToRank Unsupported SubRank:");
			return Rank.None;
		}
		}
	} }
    public static partial class Formula { public static bool Compare<T>(T lhs, string op, T rhs) where T : IComparable<T>, IEquatable<T>
	{
		return op switch
		{
			">" => lhs.CompareTo(rhs) > 0,
			"<" => lhs.CompareTo(rhs) < 0,
			"=" => lhs.Equals(rhs),
			">=" => lhs.CompareTo(rhs) >= 0,
			"<=" => lhs.CompareTo(rhs) <= 0,
			"!=" => !lhs.Equals(rhs),
			_ => lhs.CompareTo(rhs) >= 0,
		};
	} }
    public enum ConditionDataType { Player, PlayerBaseLevel, ItemData }
    public abstract class ICondition
    {
        protected Action<string> reportError => TreasureLevelDiagnostics.ReportError;
        public abstract bool IsMet(DataHub hub);
        public virtual (long current,long target) GetProgress(DataHub hub) => throw new NotSupportedException();
        public sealed class DataHub
        {
            private readonly Player player;
            public DataHub(Player player) { this.player=player; }
            public bool TryGetPlayer(out Player value) { value=player; return player!=null; }
            public bool TryGetData(ConditionDataType type,out object value) { value=type==ConditionDataType.Player?player:null;return value!=null; }
            public bool TryGetData(ConditionDataType type,out long value) { value=0;return false; }
        }
    }
    public sealed class HasItemCondition : ICondition { public int ClassId,Count=1;public string Op=">=";public override bool IsMet(DataHub dataHub)
	{
		long lhs = 0L;
		if (dataHub.TryGetPlayer(out var player))
		{
			lhs = player.GetItemAmount(ClassId);
		}
		else
		{
			if (!dataHub.TryGetData(ConditionDataType.ItemData, out object value) || !(value is PlayerItemDataWrap playerItemDataWrap))
			{
				reportError?.Invoke(" 没有找到合适的数据源！DataHub: ");
				return false;
			}
			if (playerItemDataWrap.Id == ClassId)
			{
				lhs = playerItemDataWrap.Count;
			}
		}
		return Formula.Compare(lhs, Op, Count);
	} }
    public sealed class HasItemTypeCondition : ICondition { public ItemType ItemType;public int Count;public override (long current, long target) GetProgress(DataHub dataHub)
	{
		return (current: GetValue(dataHub), target: Count);
	}

public override bool IsMet(DataHub dataHub)
	{
		(long, long) progress = GetProgress(dataHub);
		return progress.Item1 >= progress.Item2;
	}

protected long GetValue(DataHub dataHub)
	{
		if (dataHub.TryGetPlayer(out var player))
		{
			if (!player.ItemManager.ItemTypeDict.TryGetValue(ItemType, out var value))
			{
				return 0L;
			}
			long num = 0L;
			{
				foreach (PlayerItemDataWrap item in value)
				{
					num += item.Count;
				}
				return num;
			}
		}
		reportError?.Invoke(" 没有找到合适的数据源！DataHub: ");
		return 0L;
	} }
    public sealed class LevelCondition : ICondition { public int Level;public string Op=">=";public override bool IsMet(DataHub dataHub)
	{
		return Formula.Compare(GetLevel(dataHub), Op, Level);
	}

private int GetLevel(DataHub dataHub)
	{
		if (dataHub.TryGetData(ConditionDataType.PlayerBaseLevel, out long value))
		{
			return (int)value;
		}
		if (dataHub.TryGetData(ConditionDataType.Player, out object value2))
		{
			return (value2 as Player).BaseLevel;
		}
		reportError?.Invoke(" Unknow Data! DataHub: ");
		return 0;
	} }
    public sealed class CompositeOrCondition : ICondition
    {
        public List<ICondition> Children;
        private List<ICondition> GetConditions() => Children;
        public override bool IsMet(DataHub dataHub)
	{
		foreach (ICondition condition in GetConditions())
		{
			if (condition.IsMet(dataHub))
			{
				return true;
			}
		}
		return false;
	}
    }
    public sealed class TreasureSlotItemManager { public readonly Dictionary<ItemType,List<SlotItem>> ItemTypeDict=new Dictionary<ItemType,List<SlotItem>>(); }

    public sealed class TreasureSlotLevelTables
    {
        public SystemLevelLimitInfoParser Limits { get; }
        public SystemLevelGapLimitInfoParser Gaps { get; }
        public FormulaHandler Formula { get; }
        private readonly Dictionary<int,TreasureLevelInfo> levels;
        private readonly Dictionary<(Rank,int),TreasureBlessInfo> blessings;
        private readonly Dictionary<Rank,int> seasons;
        public Func<Player,int,bool,long> CalculateMergeable;
        public TreasureSlotLevelTables(IEnumerable<SystemLevelLimitInfo> limits, IEnumerable<SystemLevelGapLimitInfo> gaps,
            IEnumerable<TreasureLevelInfo> levels,
            IEnumerable<TreasureBlessInfo> blessings, IEnumerable<TreasureLevelSeason> seasons)
        {
            Limits=new SystemLevelLimitInfoParser(limits); Gaps=new SystemLevelGapLimitInfoParser(gaps);
            Formula=new FormulaHandler(Limits);
            this.levels=levels.ToDictionary(row=>row.Level); this.blessings=blessings.ToDictionary(row=>(row.Rank,row.BlessLevel));
            this.seasons=seasons.ToDictionary(row=>row.Rank,row=>row.Season);
        }
        public TreasureLevelInfo GetLevelCost(int level) => levels.TryGetValue(level,out var row)?row:null;
        public TreasureBlessInfo GetBlessCost(Rank rank,int level) => blessings.TryGetValue((rank,level),out var row)?row:null;
        public int GetSeasonByRank(Rank rank) => seasons.TryGetValue(rank,out int season)?season:-1;
        public long CalcCanMergeAmount(Player player,int id,bool recursion) => CalculateMergeable(player,id,recursion);
    }

    public class BlessRecordBase { public virtual void Init(Player player,Rank rank) {} }
    public sealed class PlayerSeasonRecordPlayerTreasureBlessRecordDataWrap
    {
        public Dictionary<int,int> TreasureSlotBlessLevelDict = new Dictionary<int,int>();
    }
    public sealed class PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap : BlessRecordBase
    {
        private readonly TreasureSlotSettings settings;
        private readonly Action<string> reportError;
        public Dictionary<ElementType,PlayerSeasonRecordPlayerTreasureBlessRecordDataWrap> ElementTreasureSlotBlessDataDict = new Dictionary<ElementType,PlayerSeasonRecordPlayerTreasureBlessRecordDataWrap>();
        public PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap(TreasureSlotSettings settings,Action<string> reportError=null)
        { this.settings=settings; this.reportError=reportError; }
        public void Init(Player player, Rank rank)
	{

		ElementTreasureSlotBlessDataDict.Clear();
		List<ElementType> treasureSlotElementTypes = settings.TreasureSlotElementTypes;
		int treasureMaxSlotCount = settings.TreasureMaxSlotCount;
		foreach (ElementType item in treasureSlotElementTypes)
		{
			PlayerTreasureBlessRecordData playerTreasureBlessRecordData = new PlayerTreasureBlessRecordData();
			playerTreasureBlessRecordData.TreasureSlotBlessLevelDict = new Dictionary<int, int>();
			for (int i = 0; i < treasureMaxSlotCount; i++)
			{
				playerTreasureBlessRecordData.TreasureSlotBlessLevelDict.Add(i, 0);
			}
			ElementTreasureSlotBlessDataDict.Add(item, playerTreasureBlessRecordData);
		}
	}

public bool AddBlessLevel(ElementType type, int slotIndex, int addLevel)
	{
		int treasureMaxSlotCount = settings.TreasureMaxSlotCount;

		if (slotIndex >= treasureMaxSlotCount)
		{
			reportError?.Invoke(": 超出最大槽位下标！ slotIndex = ");
			return false;
		}
		if (!ElementTreasureSlotBlessDataDict.TryGetValue(type, out var value))
		{
			reportError?.Invoke(": 不支持的元素类型 ElementType = ");
			return false;
		}
		value.TreasureSlotBlessLevelDict[slotIndex] += addLevel;
		return true;
	}
    }

    public sealed partial class Player
    {
        public TreasureSlotLevelTables Tables;
        public SubRank SubRank;
        public Rank Rank => SubRank.ToRank();
        public Rank MyPlanesRank;
        public int BaseLevel, Level, ServerRecordSeason, PlayerBlessLevel;
        public int BlessRecordFormat;
        public readonly TreasureSlotItemManager ItemManager = new TreasureSlotItemManager();
        public Func<int,long> ReadItemAmount;
        public Func<int,bool> MergeSourceAvailable;
        public readonly Dictionary<int,PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap> BlessRecords = new Dictionary<int,PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap>();
        public int GetCurBlessPlayerLevel() => PlayerBlessLevel;
        public long GetItemAmount(int id) => ReadItemAmount(id);
        public bool HasItems(Dictionary<int,long> requested)
        { foreach(var row in requested) if(GetItemAmount(row.Key)<row.Value) return false; return true; }
        public bool IsItemSourceFromMerge(int id) => MergeSourceAvailable(id);
        private PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap GetSeasonTreasureRecordData(int season)
            => BlessRecords.TryGetValue(season,out var row)?row:null;
        public event Action<SlotItem,int,int> OnTreasureLevelUp;
        public event Action<SlotItem,bool> OnTreasureRefresh;
        public event Action<ElementType> OnTreasureSlotShareLevelUpdate;
        public event Action OnAllTreasureBlessUpdate;
        public event Action<int> OnSeasonBlessDataUpdate;
        public void FireOnSeasonBlessDataUpdate(int season) => OnSeasonBlessDataUpdate?.Invoke(season);
        public bool CanTresureSlotBlessLevelUp()
	{
		if (Rank != MyPlanesRank)
		{
			return false;
		}
		List<ElementType> treasureSlotElementTypes = settings.TreasureSlotElementTypes;
		if (TreasureSlotData.TreasureSlotDataDict.Count < treasureSlotElementTypes.Count)
		{
			return false;
		}
		int maxTreasureBaseLevel = Tables.Limits.GetTreasureBaseLevelLimit(SubRank);
		int treasureMaxSlotCount = settings.TreasureMaxSlotCount;
		foreach (ElementType item in treasureSlotElementTypes)
		{
			PlayerTreasureElementSlotsDataWrap elementSlotData = TreasureSlotData.GetElementSlotData(item);
			if (elementSlotData == null || elementSlotData.TreasureSlots == null || elementSlotData.TreasureSlots.Count < treasureMaxSlotCount)
			{
				return false;
			}
			foreach (PlayerTreasureSlotItemDataWrap value in elementSlotData.TreasureSlots.Values)
			{
				if (value.BaseLevel < maxTreasureBaseLevel)
				{
					return false;
				}
			}
		}
		return true;
	}

public void TreasureSlotLevelUp(ElementType elementType, int slotIndex, int addLevel)
	{
		PlayerTreasureSlotItemDataWrap treasureSlotData = GetTreasureSlotData(elementType, slotIndex);
		int finalLevel = treasureSlotData.GetFinalLevel();
		if (CanTresureSlotBlessLevelUp())
		{
			TreasureSlotBlessLevelUp(this, elementType, slotIndex, addLevel);
		}
		else
		{
			TreasureSlotBaseLevelUp(elementType, slotIndex, addLevel);
		}
		int treasureSlotFinalLevel = GetTreasureSlotFinalLevel(elementType, slotIndex);
		PlayerItemDataWrap itemById = this.GetItemById(treasureSlotData.TreasureClassId);
		if (itemById != null)
		{
			OnTreasureLevelUp?.Invoke(itemById, finalLevel, treasureSlotFinalLevel);
			FireOnTreasureRefresh(itemById, isNew: false);
			FireOnTreasureSlotShareLevelUpdate(elementType);
		}
	}

public bool TreasureSlotBlessLevelUp(Player player, ElementType elementType, int slotIndex, int addLevel)
	{
		PlayerTreasureSlotItemDataWrap treasureSlotData = GetTreasureSlotData(elementType, slotIndex);
		if (treasureSlotData == null)
		{
			return false;
		}
		int serverRecordSeason = player.ServerRecordSeason;
		PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap seasonTreasureRecordData = GetSeasonTreasureRecordData(serverRecordSeason);
		if (seasonTreasureRecordData == null)
		{
			return false;
		}
		if (!seasonTreasureRecordData.AddBlessLevel(elementType, slotIndex, addLevel))
		{
			return false;
		}
		treasureSlotData.AddBlessLevel(addLevel);
		UpdateTreasureSlotShareLevel(elementType);
		FireOnSeasonBlessDataUpdate(serverRecordSeason);
		return true;
	}

public void ClearTreasureBlessLevel()
	{
		TreasureSlotData.ClearAllBlessLevel();
		FireOnAllTreasureBlessUpdate();
	}

public void TryUpdatePlayerTreasureSlotData(Rank rank)
	{
		if (rank > MyPlanesRank)
		{
			ReportError?.Invoke(" 传入的境界不能大于服务器境界!");
			return;
		}
		int seasonByRank = Tables.GetSeasonByRank(rank);
		if (seasonByRank == -1 || TreasureSlotData.SeasonId == seasonByRank)
		{
			return;
		}
		PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap seasonTreasureRecordData = GetSeasonTreasureRecordData(seasonByRank);
		if (seasonTreasureRecordData == null)
		{
			ClearTreasureBlessLevel();
		}
		else
		{
			foreach (KeyValuePair<ElementType, PlayerSeasonRecordPlayerTreasureBlessRecordDataWrap> item in seasonTreasureRecordData.ElementTreasureSlotBlessDataDict)
			{
				ElementType key = item.Key;
				foreach (KeyValuePair<int, int> item2 in item.Value.TreasureSlotBlessLevelDict)
				{
					int key2 = item2.Key;
					int value = item2.Value;
					PlayerTreasureSlotItemDataWrap treasureSlotData = GetTreasureSlotData(key, key2);
					if (treasureSlotData != null)
					{
						treasureSlotData.BlessLevel = value;
					}
				}
				UpdateTreasureSlotShareLevel(key);
			}
		}
		TreasureSlotData.SeasonId = seasonByRank;
		FireOnAllTreasureBlessUpdate();
		FireOnSeasonBlessDataUpdate(seasonByRank);
	}

public void FireOnTreasureSlotShareLevelUpdate(ElementType type)
	{
		OnTreasureSlotShareLevelUpdate?.Invoke(type);
	}

public void FireOnAllTreasureBlessUpdate()
	{
		OnAllTreasureBlessUpdate?.Invoke();
	}

public void FireOnTreasureRefresh(PlayerItemDataWrap data, bool isNew)
	{
		OnTreasureRefresh?.Invoke(data, isNew);
	}
    }
    public static partial class TreasureHelper { public static int CanTreasureSlotLevelUp(Player player, ElementType type, int slotIndex, bool autoMergeMaterial)
	{
		PlayerTreasureSlotItemDataWrap treasureSlotData = player.GetTreasureSlotData(type, slotIndex);
		if (treasureSlotData == null)
		{
			return 53307;
		}
		if (player.GetItemById(treasureSlotData.TreasureClassId) == null)
		{
			return 1189;
		}
		int num = 1;
		int finalLevel = treasureSlotData.GetFinalLevel();
		int nextSlotLevel = finalLevel + num;
		int num2 = CheckLevelLimits(player, treasureSlotData, finalLevel, nextSlotLevel);
		if (num2 != 0)
		{
			return num2;
		}
		int num3 = CheckLevelMaterials(player, treasureSlotData, nextSlotLevel, autoMergeMaterial);
		if (num3 != 0)
		{
			return num3;
		}
		return 0;
	}

private static int CheckLevelLimits(Player player, PlayerTreasureSlotItemDataWrap slotData, int curSlotLevel, int nextSlotLevel)
	{
		int maxTreasureBaseLevel = player.Tables.Limits.GetTreasureBaseLevelLimit(player.SubRank);
		int maxTreasureBlessLevel = player.Tables.Formula.GetMaxTreasureBlessLevel(player.SubRank, player.MyPlanesRank);
		bool flag = maxTreasureBlessLevel > 0 && curSlotLevel > maxTreasureBaseLevel;
		bool flag2 = maxTreasureBlessLevel > 0 && nextSlotLevel > maxTreasureBaseLevel;
		int num = int.MaxValue;
		foreach (ElementType treasureSlotElementType in player.Settings.TreasureSlotElementTypes)
		{
			num = Math.Min(num, player.GetTreasureSlotShareLevel(treasureSlotElementType));
		}
		int minLevelLimit;
		if (!flag && !flag2)
		{
			if (slotData.BaseLevel >= maxTreasureBaseLevel)
			{
				return 53302;
			}
		}
		else if (!flag & flag2)
		{
			if (!CanTresureSlotBlessLevelUp(player))
			{
				return 53312;
			}
			int num2 = CheckTreasureLevelLimitByPlayerLevel(player, slotData);
			if (num2 != 0)
			{
				return num2;
			}
			if (player.Tables.Gaps.IsBlessLevelGapLimit(DevelopType.Treasure, nextSlotLevel, num, out minLevelLimit, player.ServerRecordSeason))
			{
				return 53318;
			}
		}
		else if (flag & flag2)
		{
			int num3 = CheckTreasureLevelLimitByPlayerLevel(player, slotData);
			if (num3 != 0)
			{
				return num3;
			}
			if (player.Tables.Gaps.IsBlessLevelGapLimit(DevelopType.Treasure, nextSlotLevel, num, out minLevelLimit, player.ServerRecordSeason))
			{
				return 53318;
			}
		}
		return 0;
	}

private static int CheckTreasureLevelLimitByPlayerLevel(Player player, PlayerTreasureSlotItemDataWrap slotData)
	{
		SystemLevelLimitInfo levelLimitInfo = player.Tables.Limits.GetLevelLimitInfo(player.SubRank, player.BaseLevel, player.GetCurBlessPlayerLevel());
		if (levelLimitInfo == null)
		{
			return 53313;
		}
		int num = levelLimitInfo.TreasureLevel + levelLimitInfo.TreasureBlessLevel;
		if (slotData.BaseLevel + slotData.BlessLevel + 1 > num)
		{
			return 53314;
		}
		return 0;
	}

public static Dictionary<int, long> GetTreasureLevelUpCostMaterials(Player player, PlayerTreasureSlotItemDataWrap slotData, int nextSlotLevel)
	{
		int maxTreasureBaseLevel = player.Tables.Limits.GetTreasureBaseLevelLimit(player.SubRank);
		if (nextSlotLevel > maxTreasureBaseLevel)
		{
			TreasureBlessInfo treasureBlessInfo = player.Tables.GetBlessCost(player.Rank, nextSlotLevel - maxTreasureBaseLevel);
			if (treasureBlessInfo != null)
			{
				return treasureBlessInfo.LevelCostItems;
			}
		}
		else
		{
			TreasureLevelInfo treasureLevelInfo = player.Tables.GetLevelCost(nextSlotLevel);
			if (treasureLevelInfo != null)
			{
				return treasureLevelInfo.Materials;
			}
		}
		return null;
	}

private static int CheckLevelMaterials(Player player, PlayerTreasureSlotItemDataWrap slotData, int nextSlotLevel, bool autoMergeMaterial)
	{
		Dictionary<int, long> treasureLevelUpCostMaterials = GetTreasureLevelUpCostMaterials(player, slotData, nextSlotLevel);
		if (treasureLevelUpCostMaterials == null)
		{
			return 53315;
		}
		if (player.HasItems(treasureLevelUpCostMaterials))
		{
			return 0;
		}
		if (!autoMergeMaterial)
		{
			return 1188;
		}
		int num = default;
		long num2 = default;
		foreach (KeyValuePair<int, long> item in treasureLevelUpCostMaterials)
		{
			num = item.Key; num2 = item.Value;
			int itemId = num;
			long num3 = num2;
			long itemAmount = player.GetItemAmount(itemId);
			if (player.IsItemSourceFromMerge(itemId))
			{
				long num4 = player.Tables.CalcCanMergeAmount(player, itemId, recursion: true);
				if (num4 <= 0 || player.GetItemAmount(itemId) + num4 < num3)
				{
					return 1188;
				}
			}
			else if (itemAmount < num3)
			{
				return 1188;
			}
		}
		return 0;
	}

public static bool CanTresureSlotBlessLevelUp(Player player)
	{
		if (player.Rank != player.MyPlanesRank)
		{
			return false;
		}
		List<ElementType> treasureSlotElementTypes = player.Settings.TreasureSlotElementTypes;
		if (player.TreasureSlotData.TreasureSlotDataDict.Count < treasureSlotElementTypes.Count)
		{
			return false;
		}
		int maxTreasureBaseLevel = player.Tables.Limits.GetTreasureBaseLevelLimit(player.SubRank);
		int treasureMaxSlotCount = player.Settings.TreasureMaxSlotCount;
		foreach (ElementType item in treasureSlotElementTypes)
		{
			PlayerTreasureElementSlotsDataWrap elementSlotData = player.TreasureSlotData.GetElementSlotData(item);
			if (elementSlotData == null || elementSlotData.TreasureSlots == null || elementSlotData.TreasureSlots.Count < treasureMaxSlotCount)
			{
				return false;
			}
			foreach (PlayerTreasureSlotItemDataWrap value in elementSlotData.TreasureSlots.Values)
			{
				if (value.BaseLevel < maxTreasureBaseLevel)
				{
					return false;
				}
			}
		}
		return true;
	} }
    /*ORACLE_PORTS*/
}
