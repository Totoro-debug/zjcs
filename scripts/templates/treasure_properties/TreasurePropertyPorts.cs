public interface ITreasurePropertyTables
{
    float TreasureSlotPropAddRate{get;}
    IReadOnlyList<int> ElementPropTypes{get;}
    TreasureInfo GetTreasure(int id);
    PlayerSubRankInfo GetSubRank(int subRank);
    int GetMaxTreasureBaseLevel(int subRank);
    IReadOnlyDictionary<int,long> GetProps(int classId,int level);
    long GetProp(int classId,int level,int type);
    void ReportError(string method);
    void ReportWarning(string method);
}
public interface ITreasurePropertyPlayer
{
    int SubRank{get;}
    Zjcs.Official.TreasureCultivation.TreasureState GetTreasureState(int id);
    bool IsTreasureInSlot(int id);
    int GetTreasureFinalLevel(int id);
    long? GetCurrentCount(TreasureInfo info);
}
public static class TreasurePropertyNumber
{
    /*NUMBER*/
}
public sealed class PlayerSubRankInfo{public int SubRank,TreasurePropBless;}
public abstract class ITreasureEffect
{
    /*EFFECT_DEFAULTS*/
}
public sealed class TreasureEffectNone:ITreasureEffect{}
public sealed class TreasureEffectTask:ITreasureEffect
{
    public int PerCount,MaxHitCount;
    public List<int> LevelHitCount;
    /*TASK_METHODS*/
}
public sealed class TreasureInfo
{
    public int ClassId,TreasurePieceId,StarMax,StarScalePropId;
    public List<int> StarPropTypes,LevelPropTypes,EffectPropTypes;
    public Dictionary<int,int> LevelPropId;
    public ITreasureEffect Effect;
    private readonly ITreasurePropertyTables Tables;
    public TreasureInfo(ITreasurePropertyTables tables)=>Tables=tables??throw new ArgumentNullException(nameof(tables));
    /*INFO_LEVEL*/
}
/*CONDITIONS*/
public sealed class TreasureSuitInfo
{
    public int ClassId;
    public List<int> Treasures;
    public Dictionary<TreasureSuitPropCondition,(int PropId,List<int> PropTypes)> PropData;
    private readonly ITreasurePropertyTables Tables;
    public TreasureSuitInfo(ITreasurePropertyTables tables)=>Tables=tables??throw new ArgumentNullException(nameof(tables));
    /*SUIT_METHODS*/
}
public sealed class SnapshotTreasure
{
    public int ItemId,BaseLevel,BlessLevel,SubRank;
    public bool InSlot;
    public Zjcs.Official.TreasureCultivation.TreasureState TreasureParam;
    public Dictionary<int,int> ActivedInSuit;
}
