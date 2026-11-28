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
    /*ENUMS*/
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
        /*SystemLevelLimitInfoParser*/
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
        /*SystemLevelGapLimitInfoParser*/
    }
    public sealed class TreasureLevelInfo { public int Level; public Dictionary<int,long> Materials; }
    public sealed class TreasureBlessInfo { public Rank Rank; public int BlessLevel; public Dictionary<int,long> LevelCostItems; }
    public sealed class TreasureLevelSeason { public Rank Rank; public int Season; }
    public sealed class FormulaHandler
    {
        private readonly SystemLevelLimitInfoParser limits;
        public FormulaHandler(SystemLevelLimitInfoParser limits) { this.limits=limits; }
        /*FormulaHandler*/
    }
    public static class EnumExt { /*EnumExt*/ }
    public static partial class Formula { /*Formula*/ }
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
    public sealed class HasItemCondition : ICondition { public int ClassId,Count=1;public string Op=">=";/*HasItemCondition*/ }
    public sealed class HasItemTypeCondition : ICondition { public ItemType ItemType;public int Count;/*HasItemTypeCondition*/ }
    public sealed class LevelCondition : ICondition { public int Level;public string Op=">=";/*LevelCondition*/ }
    public sealed class CompositeOrCondition : ICondition
    {
        public List<ICondition> Children;
        private List<ICondition> GetConditions() => Children;
        /*CompositeOrCondition*/
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
        /*PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap*/
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
        /*Player*/
    }
    public static partial class TreasureHelper { /*TreasureHelper*/ }
    /*ORACLE_PORTS*/
}
