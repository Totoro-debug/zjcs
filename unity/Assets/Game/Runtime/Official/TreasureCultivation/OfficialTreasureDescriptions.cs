// Original TreasureConfigUtils branches and argument order; explicit local owners replace globals.
using System;
using System.Collections.Generic;
using PropType = System.Int32;
namespace Zjcs.Official.TreasureProperties
{
    public interface ITreasureDescriptionServices
    {
        ITreasurePropertyTables Tables { get; }
        ITreasurePropertyPlayer Player { get; }
        int SeasonId { get; }
        object Compact(long value);
        object PropertyValue(int type, long value, bool includeUnit, bool includePropDesc,
            long targetValue, bool needSpaceBeforeValue);
    }
public enum TreasurePropDisplayOption
{
	None,
	Level,
	Star
}

    public sealed class TreasureDescriptionQuery
    {
        private readonly ITreasureDescriptionServices Services;
        private readonly TreasurePropertyCalculator calculator;
        private readonly Dictionary<int, long> sTmpPropsDict = new Dictionary<int, long>();
        public TreasureDescriptionQuery(ITreasureDescriptionServices services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            calculator = new TreasurePropertyCalculator(services.Tables);
        }
public (string Key, object[] Params) GenerateDesc(ITreasureEffect self, TreasureInfo info, ITreasurePropProvider propProvider = null, TreasurePropDisplayOption option = TreasurePropDisplayOption.None)
	{
		if (self is TreasureEffectTask self2)
		{
			return (Key: GetTreasureFuncDescKey(info.ClassId), Params: GenerateTaskDesc(self2, info, propProvider, option));
		}
		return (Key: GetTreasureFuncDescKey(info.ClassId), Params: GenerateDefaultDesc(self, info, propProvider, option));
	}
private object[] GenerateTaskDesc(TreasureEffectTask self, TreasureInfo info, ITreasurePropProvider propProvider, TreasurePropDisplayOption option)
	{
		int star = 0;
		int baseLevel = 1;
		int num = 0;
		TreasureInfo treasureInfo = info;
		TreasureCalcPropData treasureCalcPropData = TreasureCalcPropData.CreateCalcPropDataByBaseLevel(Services.Tables, baseLevel);
		if (propProvider != null)
		{
			treasureInfo = propProvider.Info();
			star = propProvider.Star();
			num = propProvider.GetHitCount();
			baseLevel = propProvider.Level();
			treasureCalcPropData.BaseLevel = propProvider.BaseLevel();
			treasureCalcPropData.FinalLevel = baseLevel;
			treasureCalcPropData.AssignBlessLevelPropDict(propProvider.SubRank());
		}
		sTmpPropsDict.Clear();
		calculator.CalcTreasureProps(treasureInfo, star, treasureCalcPropData, 1, sTmpPropsDict, inSlot: false, Services.SeasonId);
		List<object> list = new List<object> { Services.Compact(self.PerCount) };
		foreach (PropType effectPropType in treasureInfo.EffectPropTypes)
		{
			if (sTmpPropsDict.TryGetValue(effectPropType, out var value))
			{
				list.Add(Services.PropertyValue(effectPropType, value, includeUnit: false, includePropDesc: true, targetValue: -1L, needSpaceBeforeValue: true));
			}
		}
		list.Add(Services.Compact(num));
		list.Add(Services.Compact(self.MaxHitCount));
		return list.ToArray();
	}
private object[] GenerateDefaultDesc(ITreasureEffect self, TreasureInfo info, ITreasurePropProvider propProvider, TreasurePropDisplayOption option)
	{
		int star = 0;
		int baseLevel = 1;
		TreasureInfo treasureInfo = info;
		bool inSlot = Services.Player.IsTreasureInSlot(info.ClassId);
		TreasureCalcPropData treasureCalcPropData = TreasureCalcPropData.CreateCalcPropDataByBaseLevel(Services.Tables, baseLevel);
		if (propProvider != null)
		{
			treasureInfo = propProvider.Info();
			star = propProvider.Star();
			baseLevel = propProvider.Level();
			inSlot = propProvider.InSlot();
			treasureCalcPropData.BaseLevel = propProvider.BaseLevel();
			treasureCalcPropData.FinalLevel = baseLevel;
			treasureCalcPropData.AssignBlessLevelPropDict(propProvider.SubRank());
		}
		if (treasureInfo.GetLevelPropId(Services.SeasonId) == 0)
		{
			return null;
		}
		sTmpPropsDict.Clear();
		calculator.CalcTreasureProps(treasureInfo, star, treasureCalcPropData, 1, sTmpPropsDict, inSlot, Services.SeasonId);
		List<object> list = new List<object>();
		foreach (PropType effectPropType in treasureInfo.EffectPropTypes)
		{
			if (sTmpPropsDict.TryGetValue(effectPropType, out var value))
			{
				list.Add(Services.PropertyValue(effectPropType, value, includeUnit: false, includePropDesc: true, targetValue: -1L, needSpaceBeforeValue: true));
			}
		}
		return list.ToArray();
	}
public static string GetTreasureFuncDescKey(int id)
	{
		return $"treasure_{id}_func_desc";
	}
    }
}
