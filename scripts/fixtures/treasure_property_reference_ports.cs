public static class Diagnostics
{
    public static readonly List<string> Errors=new List<string>(),Warnings=new List<string>();
    public static void ReportError(string method)=>Errors.Add(method);
    public static void ReportWarning(string method)=>Warnings.Add(method);
}
public sealed class TreasureInfo
{
    public int ClassId,StarMax,StarScalePropId;
    public List<PropType> StarPropTypes,LevelPropTypes,EffectPropTypes;
    public Dictionary<int,int> LevelPropId;
    public ITreasureEffect Effect;
    public TreasureDataBase TreasureData;
    /*INFO*/
}
public abstract class ITreasureEffect{/*EFFECT*/}
public sealed class TreasureEffectNone:ITreasureEffect{}
public sealed class TreasureEffectTask:ITreasureEffect
{
    public int PerCount,MaxHitCount;
    public List<int> LevelHitCount;
    /*TASK*/
}
public sealed class TreasureSuitInfo
{
    public List<int> Treasures;
    public Dictionary<TreasureSuitPropCondition,(int PropId,List<PropType> PropTypes)> PropData;
    /*SUIT*/
}
public sealed class PlayerSubRankInfo{public int TreasurePropBless;}
public sealed class TreasureState
{
    public bool Active;public int Star,HitCount;
    public object Wrap()=>this;
}
public sealed class ItemInfo{public int ClassId;public object PartInfo;}
public sealed class PlayerItemDataWrap
{
    public int Id;public ItemInfo Info;public TreasureState State;
    public TreasureState GetItemParamTreasure()=>State;
}
public sealed class Player
{
    public SubRank SubRank;
    public readonly Dictionary<int,PlayerItemDataWrap> Items=new Dictionary<int,PlayerItemDataWrap>();
    public Func<int,int> FinalLevel;
    public Func<int,bool> InSlot;
    public PlayerItemDataWrap GetItemById(int id)=>Items.TryGetValue(id,out var item)?item:null;
    public int GetTreasureFinalLevel(int id)=>FinalLevel(id);
    public bool IsTreasureInSlot(int id)=>InSlot(id);
}
public sealed class TreasureDataBase{public Func<Player,long> Read;public long GetCurrentCount(Player player)=>Read(player);}
public sealed class SnapshotTreasure
{
    public int ItemId,BaseLevel,BlessLevel;public SubRank SubRank;public bool InSlot;public TreasureState TreasureParam;
}
public sealed class Lookup<TKey,TValue>
{
    public Dictionary<TKey,TValue> Values=new Dictionary<TKey,TValue>();
    public TValue Get(TKey key)=>Values.TryGetValue(key,out var value)?value:default;
}
public sealed class TreasureInfoLookup
{
    public readonly Dictionary<int,TreasureInfo> Values=new Dictionary<int,TreasureInfo>();
    public TreasureInfo Get(int id)=>Values.TryGetValue(id,out var value)?value:null;
    /*ELEMENT*/
}
public sealed class LimitLookup
{
    public Func<SubRank,int> Read;
    public int GetTreasureBaseLevelLimit(SubRank subRank)=>Read(subRank);
}
public sealed class LevelPropParser
{
    public delegate bool RowReader(int classId,int level,out IReadOnlyDictionary<PropType,long> row);
    public delegate bool RangeReader(int classId,out (int,int) range);
    public RowReader ReadRow;public RangeReader ReadRange;
    private bool TryGetPropDict(int classId,int level,out IReadOnlyDictionary<PropType,long> row)=>ReadRow(classId,level,out row);
    private bool TryGetLevelRange(int classId,out (int,int) range)=>ReadRange(classId,out range);
    /*PARSER*/
}
public sealed class Sources
{
    public LevelPropParser LevelPropParser;
    public TreasureInfoLookup TreasureInfoParser=new TreasureInfoLookup();
    public Lookup<SubRank,PlayerSubRankInfo> PlayerSubRankInfoParser=new Lookup<SubRank,PlayerSubRankInfo>();
    public Lookup<int,ItemInfo> ItemInfoParser=new Lookup<int,ItemInfo>();
    public LimitLookup SystemLevelLimitInfoParser;
}
public sealed class Settings{public float TreasureSlotPropAddRate;}
public static class Parsers{public static Sources Instance;public static Settings GameSettings;}
public static class PlanesInfoUtils{public static int SeasonId;}
public static class MeManager{public static Player MainPlayer;}
public static class TreasureInfoParser
{public static List<PropType> sElementProps=>TreasureInfoLookup.sElementProps;}
public static class Formula{/*NUMERIC*/}
public static class DictionaryPool<TKey,TValue>
{public static Dictionary<TKey,TValue> Rent()=>new Dictionary<TKey,TValue>();public static void Return(Dictionary<TKey,TValue> value){}}
public sealed class BattleFormulaHandler{/*FORMULA*/}
public static class BattleFormula{public static readonly BattleFormulaHandler Instance=new BattleFormulaHandler();}
