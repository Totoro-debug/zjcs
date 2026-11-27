// Original 911 per-item properties; explicit table, player, season and diagnostic boundaries.
using System;
using System.Linq;
using System.Collections.Generic;
using PropType=System.Int32;
using SubRank=System.Int32;
using Player=Zjcs.Official.TreasureProperties.ITreasurePropertyPlayer;
using PlayerItemParamTreasureWrap=Zjcs.Official.TreasureCultivation.TreasureState;
namespace Zjcs.Official.TreasureProperties {
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
    public static double Long2Double(long val)
	{
		return (double)val / 10000.0;
	}
}
public sealed class PlayerSubRankInfo{public int SubRank,TreasurePropBless;}
public abstract class ITreasureEffect
{
    public virtual int GetLevelMaxHitCount(int currentLevel)
	{
		return 1;
	}
public virtual int GetMaxHitCount()
	{
		return 1;
	}
}
public sealed class TreasureEffectNone:ITreasureEffect{}
public sealed class TreasureEffectTask:ITreasureEffect
{
    public int PerCount,MaxHitCount;
    public List<int> LevelHitCount;
    public override int GetLevelMaxHitCount(int currentLevel)
	{
		List<int> levelHitCount = LevelHitCount;
		if (levelHitCount == null || levelHitCount.Count == 0)
		{
			return MaxHitCount;
		}
		if (currentLevel < LevelHitCount.Count)
		{
			return LevelHitCount[currentLevel - 1];
		}
		return LevelHitCount[LevelHitCount.Count - 1];
	}
public override int GetMaxHitCount()
	{
		List<int> levelHitCount = LevelHitCount;
		if (levelHitCount == null || levelHitCount.Count == 0)
		{
			return MaxHitCount;
		}
		return LevelHitCount[LevelHitCount.Count - 1];
	}
}
public sealed class TreasureInfo
{
    public int ClassId,TreasurePieceId,StarMax,StarScalePropId;
    public List<int> StarPropTypes,LevelPropTypes,EffectPropTypes;
    public Dictionary<int,int> LevelPropId;
    public ITreasureEffect Effect;
    private readonly ITreasurePropertyTables Tables;
    public TreasureInfo(ITreasurePropertyTables tables)=>Tables=tables??throw new ArgumentNullException(nameof(tables));
    public int GetLevelPropId(int seasonId)
	{
		if (seasonId < 0)
		{
			seasonId = 0;
		}
		if (LevelPropId.TryGetValue(seasonId, out var value))
		{
			return value;
		}

		Tables.ReportError("GetLevelPropId");
		if (LevelPropId.Count == 0)
		{
			return 0;
		}
		return LevelPropId.First().Value;
	}

}

public enum TreasureSuitPropCondition
{
	FullCollected,
	FullThreeStar,
	FullMaxStar
}

public sealed class TreasureSuitInfo
{
    public int ClassId;
    public List<int> Treasures;
    public Dictionary<TreasureSuitPropCondition,(int PropId,List<int> PropTypes)> PropData;
    private readonly ITreasurePropertyTables Tables;
    public TreasureSuitInfo(ITreasurePropertyTables tables)=>Tables=tables??throw new ArgumentNullException(nameof(tables));
    public IEnumerable<TreasureSuitPropCondition> GetActivedCondition(Dictionary<int, int> activedStars)
	{
		IEnumerable<KeyValuePair<int, int>> activedStars2 = activedStars.Where((KeyValuePair<int, int> it) => Treasures.Contains(it.Key));
		return GetActivedConditionBySuitActivedStars(activedStars2);
	}
public IEnumerable<TreasureSuitPropCondition> GetActivedConditionBySuitActivedStars(IEnumerable<KeyValuePair<int, int>> activedStars)
	{
		int suitItemCount = Treasures.Count;
		int num = 0;
		int threeStarCount = 0;
		int maxStarCount = 0;
		foreach (KeyValuePair<int, int> activedStar in activedStars)
		{
			num++;
			if (activedStar.Value >= 3)
			{
				int num2 = threeStarCount + 1;
				threeStarCount = num2;
			}
			if (Tables.GetTreasure(activedStar.Key) is TreasureInfo treasureInfo && activedStar.Value >= treasureInfo.StarMax)
			{
				int num2 = maxStarCount + 1;
				maxStarCount = num2;
			}
		}
		if (num == suitItemCount)
		{
			yield return TreasureSuitPropCondition.FullCollected;
		}
		if (threeStarCount == suitItemCount)
		{
			yield return TreasureSuitPropCondition.FullThreeStar;
		}
		if (maxStarCount == suitItemCount)
		{
			yield return TreasureSuitPropCondition.FullMaxStar;
		}
	}
public IEnumerable<(int PropId, List<PropType> PropTypes)> GetActivatedProps(Dictionary<int,int> activedStars)
	{
		foreach (TreasureSuitPropCondition item in GetActivedCondition(activedStars))
		{
			if (PropData.TryGetValue(item, out (int, List<PropType>) value))
			{
				yield return value;
			}
		}
	}
}
public sealed class SnapshotTreasure
{
    public int ItemId,BaseLevel,BlessLevel,SubRank;
    public bool InSlot;
    public Zjcs.Official.TreasureCultivation.TreasureState TreasureParam;
    public Dictionary<int,int> ActivedInSuit;
}

public abstract class CalcPropDataBase
{
    protected readonly ITreasurePropertyTables Tables;
    protected CalcPropDataBase(ITreasurePropertyTables tables){Tables=tables??throw new ArgumentNullException(nameof(tables));}
	public int FinalLevel;

	public int BaseLevel;

	protected IReadOnlyDictionary<PropType, long> PropDict;

	protected abstract int GetPropClassId(PlayerSubRankInfo info);

	private int GetBlessLevel()
	{
		return FinalLevel - BaseLevel;
	}

	public bool HasBlessLevelPropData()
	{
		if (GetBlessLevel() <= 0 || PropDict == null)
		{
			return false;
		}
		return true;
	}

	public void Clear()
	{
		FinalLevel = 0;
		BaseLevel = 0;
		PropDict = null;
	}

	public void ResetCalcData(int finalLevel, int baseLevel, SubRank subRank)
	{
		Clear();
		FinalLevel = finalLevel;
		BaseLevel = baseLevel;
		AssignBlessLevelPropDict(subRank);
	}

	public void AssignBlessLevelPropDict(SubRank subRank)
	{
		if (GetBlessLevel() <= 0)
		{
			return;
		}
		PlayerSubRankInfo playerSubRankInfo = Tables.GetSubRank(subRank);
		if (playerSubRankInfo != null)
		{
			IReadOnlyDictionary<PropType, long> levelpropDictByFixBlessLevel = GetLevelpropDictByFixBlessLevel(GetPropClassId(playerSubRankInfo), GetBlessLevel());
			if (levelpropDictByFixBlessLevel != null)
			{
				PropDict = levelpropDictByFixBlessLevel;
			}
		}
	}

	private IReadOnlyDictionary<PropType, long> GetLevelpropDictByFixBlessLevel(int blessPropClassId, int blessLevel)
	{
		if (blessPropClassId <= 0 || blessLevel <= 0)
		{
			return null;
		}
		IReadOnlyDictionary<PropType, long> props = Tables.GetProps(blessPropClassId, blessLevel);
		if (props != null)
		{
			return props;
		}

		Tables.ReportError("GetLevelpropDictByFixBlessLevel");
		for (int num = blessLevel - 1; num > 0; num--)
		{
			props = Tables.GetProps(blessPropClassId, num);
			if (props != null)
			{
				return props;
			}

			Tables.ReportError("GetLevelpropDictByFixBlessLevel");
		}
		return null;
	}

	public virtual double GetBlessScaleFactor(PropType propType)
	{
		if (!HasBlessLevelPropData())
		{
			return 1.0;
		}
		if (PropDict.TryGetValue(propType, out var value))
		{
			return 1.0 + TreasurePropertyNumber.Long2Double(value);
		}
		return 1.0;
	}
}

public class TreasureCalcPropData : CalcPropDataBase
{
	protected TreasureCalcPropData(ITreasurePropertyTables tables) : base(tables)
	{
	}

	protected TreasureCalcPropData(ITreasurePropertyTables tables, int finalLevel, int baseLevel) : base(tables)
	{
		BaseLevel = baseLevel;
		FinalLevel = finalLevel;
	}

	public static TreasureCalcPropData CreateCalcPropData(ITreasurePropertyTables tables)
	{
		return new TreasureCalcPropData(tables);
	}

	public static TreasureCalcPropData CreateCalcPropDataByBaseLevel(ITreasurePropertyTables tables, int baseLevel)
	{
		return new TreasureCalcPropData(tables, baseLevel, baseLevel);
	}

	public static TreasureCalcPropData CreateCalcPropDataByFinalLevelAndBaseLevel(ITreasurePropertyTables tables, int finalLevel, int baseLevel)
	{
		return new TreasureCalcPropData(tables, finalLevel, baseLevel);
	}

	protected override int GetPropClassId(PlayerSubRankInfo info)
	{
		return info.TreasurePropBless;
	}
}
public sealed class TreasurePropertyCalculator {
private readonly ITreasurePropertyTables Tables;
public TreasurePropertyCalculator(ITreasurePropertyTables tables){Tables=tables??throw new ArgumentNullException(nameof(tables));}
public void CalcTreasureProps(TreasureInfo treasureInfo, int star, TreasureCalcPropData calcPropData, int hitCount, Dictionary<PropType, long> resultProps, bool inSlot, int seasonId)
	{
		if (resultProps == null)
		{
			resultProps = new Dictionary<PropType, long>();

			Tables.ReportWarning("CalcTreasureProps");
		}
		Dictionary<PropType, long> dictionary = new Dictionary<PropType, long>();
		Dictionary<PropType, long> dictionary2 = new Dictionary<PropType, long>();
		CalcTreasureStarPartProps(treasureInfo, star, calcPropData, hitCount, inSlot, seasonId, dictionary);
		CalcTreasureLevelPartProps(treasureInfo, star, calcPropData, hitCount, inSlot, seasonId, dictionary2);
		MergeProps(dictionary, dictionary2, resultProps);


	}
public Dictionary<PropType, long> CalcTreasureLevelPartProps(TreasureInfo treasureInfo, int star, TreasureCalcPropData calcPropData, int hitCount, bool inSlot, int seasonId)
	{
		Dictionary<PropType, long> dictionary = new Dictionary<PropType, long>();
		CalcTreasureLevelPartProps(treasureInfo, star, calcPropData, hitCount, inSlot, seasonId, dictionary);
		return dictionary;
	}
private void CalcTreasureLevelPartProps(TreasureInfo treasureInfo, int star, TreasureCalcPropData calcPropData, int hitCount, bool inSlot, int seasonId, Dictionary<PropType, long> levelProps)
	{
		levelProps.Clear();
		int levelMaxHitCount = treasureInfo.Effect.GetLevelMaxHitCount(calcPropData.FinalLevel);
		int levelPropId = treasureInfo.GetLevelPropId(seasonId);
		if (levelPropId == 0)
		{
			return;
		}
		foreach (PropType levelPropType in treasureInfo.LevelPropTypes)
		{
			long prop = Tables.GetProp(levelPropId, calcPropData.BaseLevel, levelPropType);
			prop = (long)((double)prop * calcPropData.GetBlessScaleFactor(levelPropType));
			long prop2 = Tables.GetProp(treasureInfo.StarScalePropId, star, levelPropType);
			prop = (long)((double)prop * TreasurePropertyNumber.Long2Double(prop2));
			if (treasureInfo.EffectPropTypes.Contains(levelPropType))
			{
				long num = prop / levelMaxHitCount;
				num = ((num == 0L) ? 1 : num);
				prop = num * hitCount;
			}
			if (inSlot)
			{
				long num2 = (long)((float)prop * Tables.TreasureSlotPropAddRate);
				prop += num2;
			}
			levelProps.Add(levelPropType, prop);
		}
	}
public Dictionary<PropType, long> CalcTreasureStarPartProps(TreasureInfo treasureInfo, int star, TreasureCalcPropData calcPropData, int hitCount, bool inSlot, int seasonId)
	{
		Dictionary<PropType, long> dictionary = new Dictionary<PropType, long>();
		CalcTreasureStarPartProps(treasureInfo, star, calcPropData, hitCount, inSlot, seasonId, dictionary);
		return dictionary;
	}
private void CalcTreasureStarPartProps(TreasureInfo treasureInfo, int star, TreasureCalcPropData calcPropData, int hitCount, bool inSlot, int seasonId, Dictionary<PropType, long> starProps)
	{
		starProps.Clear();
		int levelMaxHitCount = treasureInfo.Effect.GetLevelMaxHitCount(calcPropData.FinalLevel);
		int levelPropId = treasureInfo.GetLevelPropId(seasonId);
		if (levelPropId == 0)
		{
			return;
		}
		foreach (PropType starPropType in treasureInfo.StarPropTypes)
		{
			long prop = Tables.GetProp(levelPropId, calcPropData.BaseLevel, starPropType);
			prop = (long)((double)prop * calcPropData.GetBlessScaleFactor(starPropType));
			long prop2 = Tables.GetProp(treasureInfo.StarScalePropId, star, starPropType);
			prop = (long)((double)prop * TreasurePropertyNumber.Long2Double(prop2));
			if (treasureInfo.EffectPropTypes.Contains(starPropType))
			{
				long num = prop / levelMaxHitCount;
				num = ((num == 0L) ? 1 : num);
				prop = num * hitCount;
			}
			if (inSlot)
			{
				long num2 = (long)((float)prop * Tables.TreasureSlotPropAddRate);
				prop += num2;
			}
			starProps.Add(starPropType, prop);
		}
	}
public void MergeProps(IReadOnlyDictionary<PropType, long> props1, IReadOnlyDictionary<PropType, long> props2, Dictionary<PropType, long> mergeResult)
	{
		if (mergeResult == null)
		{
			mergeResult = new Dictionary<PropType, long>();

			Tables.ReportWarning("MergeProps");
		}
		MergePropsInPlace(mergeResult, props1);
		MergePropsInPlace(mergeResult, props2);
	}
public void MergePropsInPlace(Dictionary<PropType, long> dest, (PropType, long) add)
	{
		if (!dest.ContainsKey(add.Item1))
		{
			dest[add.Item1] = 0L;
		}
		dest[add.Item1] += add.Item2;
	}

    private void MergePropsInPlace(Dictionary<PropType,long> dest,IReadOnlyDictionary<PropType,long> add)
    {
        if(add!=null)foreach(var pair in add)MergePropsInPlace(dest,(pair.Key,pair.Value));
    }
public void CalcTreasureSuitProps(TreasureSuitInfo suit, Dictionary<int,int> activedStars, Dictionary<PropType, long> resultProps)
	{
		if (resultProps == null)
		{
			resultProps = new Dictionary<PropType, long>();

			Tables.ReportWarning("CalcTreasureSuitProps");
		}
		foreach (var activatedProp in suit.GetActivatedProps(activedStars))
		{
			foreach (PropType item in activatedProp.PropTypes)
			{
				if (!resultProps.ContainsKey(item))
				{
					resultProps.Add(item, 0L);
				}
				long prop = Tables.GetProp(activatedProp.PropId, 0, item);
				resultProps[item] += prop;
			}
		}
	}
}
public sealed class TreasurePropertyQueries
{
    private readonly ITreasurePropertyTables Tables;
    private readonly TreasurePropertyCalculator Calculator;
    private readonly Func<int> SeasonId;
    public TreasurePropertyQueries(ITreasurePropertyTables tables,Func<int> seasonId)
    {Tables=tables??throw new ArgumentNullException(nameof(tables));Calculator=new TreasurePropertyCalculator(tables);SeasonId=seasonId??throw new ArgumentNullException(nameof(seasonId));}

	private readonly Dictionary<PropType, long> sTmpPropsDict = new Dictionary<PropType, long>();

	public Dictionary<PropType, long> GetProDict()
	{
		sTmpPropsDict.Clear();
		return sTmpPropsDict;
	}

	public (PropType PropType, long PropValue) GetElementPropInfo(TreasureInfo self, Player player, int star, TreasureCalcPropData calcPropData, int hitCount, bool? inSlot = null)
	{
		Dictionary<PropType, long> proDict = GetProDict();
		inSlot = inSlot ?? player.IsTreasureInSlot(self.ClassId);
		Calculator.CalcTreasureProps(self, star, calcPropData, hitCount, proDict, inSlot.Value, SeasonId());
		foreach (PropType sElementProp in Tables.ElementPropTypes)
		{
			if (proDict.TryGetValue(sElementProp, out var value))
			{
				return (PropType: sElementProp, PropValue: value);
			}
		}

		Tables.ReportError("GetElementPropInfo");
		return (PropType: 0, PropValue: 0L);
	}

	public (PropType PropType, long PropValue) GetEffectPropInfo(TreasureInfo self, Player player, int star, TreasureCalcPropData calcPropData, int hitCount, bool? inSlot = null)
	{
		Dictionary<PropType, long> proDict = GetProDict();
		inSlot = inSlot ?? player.IsTreasureInSlot(self.ClassId);
		Calculator.CalcTreasureProps(self, star, calcPropData, hitCount, proDict, inSlot.Value, SeasonId());
		foreach (PropType effectPropType in self.EffectPropTypes)
		{
			if (!Tables.ElementPropTypes.Contains(effectPropType) && proDict.TryGetValue(effectPropType, out var value))
			{
				return (PropType: effectPropType, PropValue: value);
			}
		}

		Tables.ReportError("GetEffectPropInfo");
		return (PropType: 0, PropValue: 0L);
	}

	public void GetStarProps(TreasureInfo self, Player player, int star, TreasureCalcPropData calcPropData, int hitCount, Dictionary<PropType, long> resultProps)
	{
		if (resultProps == null)
		{
			resultProps = new Dictionary<PropType, long>();

			Tables.ReportError("GetStarProps");
		}
		Dictionary<PropType, long> proDict = GetProDict();
		bool inSlot = player.IsTreasureInSlot(self.ClassId);
		Calculator.CalcTreasureProps(self, star, calcPropData, hitCount, proDict, inSlot, SeasonId());
		foreach (PropType starPropType in self.StarPropTypes)
		{
			if (proDict.TryGetValue(starPropType, out var value))
			{
				resultProps.Add(starPropType, value);
			}
		}
		foreach (PropType levelPropType in self.LevelPropTypes)
		{
			if (proDict.TryGetValue(levelPropType, out var value2))
			{
				resultProps.Add(levelPropType, value2);
			}
		}
	}
}

public interface ITreasurePropProvider
{
	int Star();

	int Level();

	int BlessLevel();

	int BaseLevel();

	SubRank SubRank();

	bool InSlot();

	TreasureInfo Info();

	int GetHitCount();

	bool Active();
}



public class TreasureDummyPropProvider : ITreasurePropProvider
{
	private readonly int mClassId;
    private readonly ITreasurePropertyTables Tables;
    private readonly Player Player;

	private readonly TreasureInfo mTreasureInfo;

	private PlayerItemParamTreasureWrap ItemParam => Player.GetTreasureState(mClassId);

	public TreasureDummyPropProvider(ITreasurePropertyTables tables, Player player, int classId)
	{
		Tables=tables??throw new ArgumentNullException(nameof(tables));Player=player??throw new ArgumentNullException(nameof(player));
        mClassId = classId;
		mTreasureInfo = Tables.GetTreasure(mClassId);
	}

	public bool Active()
	{
		return ItemParam?.Active ?? false;
	}

	public int BlessLevel()
	{
		int num = Level();
		int maxTreasureBaseLevel = Tables.GetMaxTreasureBaseLevel(Player.SubRank);
		if (num <= maxTreasureBaseLevel)
		{
			return 0;
		}
		return num - maxTreasureBaseLevel;
	}

	public bool InSlot()
	{
		return Player.IsTreasureInSlot(mTreasureInfo.ClassId);
	}

	public int GetHitCount()
	{
		return ItemParam?.HitCount ?? ((int)(Player.GetCurrentCount(mTreasureInfo) ?? 0));
	}

	public TreasureInfo Info()
	{
		return mTreasureInfo;
	}

	public int Level()
	{
		return Player.GetTreasureFinalLevel(mTreasureInfo.ClassId);
	}

	public int Star()
	{
		return ItemParam?.Star ?? 0;
	}

	public SubRank SubRank()
	{
		return Player.SubRank;
	}

	public int BaseLevel()
	{
		int val = Level();
		int maxTreasureBaseLevel = Tables.GetMaxTreasureBaseLevel(Player.SubRank);
		return Math.Min(val, maxTreasureBaseLevel);
	}
}


public class TreasureSnapshotPropProvider : ITreasurePropProvider
{
	private SnapshotTreasure mSnapshot;

	private TreasureInfo mTreasureInfo;

	public TreasureSnapshotPropProvider(ITreasurePropertyTables tables, SnapshotTreasure snapshot)
	{
		mSnapshot = snapshot;
		mTreasureInfo = tables.GetTreasure(mSnapshot.ItemId);
	}

	public bool Active()
	{
		return false;
	}

	public int BlessLevel()
	{
		return mSnapshot.BlessLevel;
	}

	public bool InSlot()
	{
		return mSnapshot.InSlot;
	}

	public int GetHitCount()
	{
		return mSnapshot.TreasureParam.HitCount;
	}

	public TreasureInfo Info()
	{
		return mTreasureInfo;
	}

	public int Level()
	{
		return mSnapshot.BlessLevel + mSnapshot.BaseLevel;
	}

	public int Star()
	{
		return mSnapshot.TreasureParam.Star;
	}

	public SubRank SubRank()
	{
		return mSnapshot.SubRank;
	}

	public int BaseLevel()
	{
		return mSnapshot.BaseLevel;
	}
}


public class TreasureInSlotPropProvider : ITreasurePropProvider
{
	private readonly ITreasurePropProvider mOriginProvider;

	public TreasureInSlotPropProvider(ITreasurePropProvider provider)
	{
		mOriginProvider = provider;
	}

	public bool Active()
	{
		return mOriginProvider.Active();
	}

	public int BaseLevel()
	{
		return mOriginProvider.BaseLevel();
	}

	public int BlessLevel()
	{
		return mOriginProvider.BlessLevel();
	}

	public int GetHitCount()
	{
		return mOriginProvider.GetHitCount();
	}

	public TreasureInfo Info()
	{
		return mOriginProvider.Info();
	}

	public bool InSlot()
	{
		return true;
	}

	public int Level()
	{
		return mOriginProvider.Level();
	}

	public int Star()
	{
		return mOriginProvider.Star();
	}

	public SubRank SubRank()
	{
		return mOriginProvider.SubRank();
	}
}


public class TreasureNoSlotPropProvider : ITreasurePropProvider
{
	private ITreasurePropProvider mOriginProvider;

	public TreasureNoSlotPropProvider(ITreasurePropProvider provider)
	{
		mOriginProvider = provider;
	}

	public bool Active()
	{
		return mOriginProvider.Active();
	}

	public int BaseLevel()
	{
		return mOriginProvider.BaseLevel();
	}

	public int BlessLevel()
	{
		return mOriginProvider.BlessLevel();
	}

	public int GetHitCount()
	{
		return mOriginProvider.GetHitCount();
	}

	public TreasureInfo Info()
	{
		return mOriginProvider.Info();
	}

	public bool InSlot()
	{
		return false;
	}

	public int Level()
	{
		return mOriginProvider.Level();
	}

	public int Star()
	{
		return mOriginProvider.Star();
	}

	public SubRank SubRank()
	{
		return mOriginProvider.SubRank();
	}
}

}
