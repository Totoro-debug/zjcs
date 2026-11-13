// Selected official Console.dll; parser/player boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeIncomeRules
{
public sealed class BattleFormulaHandler
{
    private readonly Func<PropType, PropCfgInfo> configuration;
    public BattleFormulaHandler(Func<PropType, PropCfgInfo> configuration)
    { this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration)); }
	public void GetHangUpProp(HangUpInfo hangUpInfo, IReadOnlyDictionary<int, long> props, out double addPercent, out float addFixed)
	{
		GetExtraHangUpProp(hangUpInfo, props, out addPercent, out addFixed);
		addPercent++;
		if (hangUpInfo.FinallyEffectPercentPropType != PropType.None)
		{
			addPercent *= 1.0 + GetProp(props, hangUpInfo.FinallyEffectPercentPropType);
		}
	}

	public void GetExtraHangUpProp(HangUpInfo hangUpInfo, IReadOnlyDictionary<int, long> props, out double addPercent, out float addFixed)
	{
		addFixed = 0f;
		addPercent = 0.0;
		if (hangUpInfo.EffectPercentPropType != PropType.None)
		{
			addPercent += GetProp(props, hangUpInfo.EffectPercentPropType);
		}
		if (hangUpInfo.BindFurniture == EFurniture.Cart)
		{
			addPercent += GetProp(props, PropType.HangUpCartAllPercent);
		}
		if (hangUpInfo.EffectFixedPropType != PropType.None)
		{
			addFixed = (float)GetProp(props, hangUpInfo.EffectFixedPropType);
		}
	}

	public double GetProp(IReadOnlyDictionary<int, long> props, PropType type)
	{
		PropCfgInfo propCfgInfo = configuration(type);
		if (propCfgInfo == null)
		{
			return 0.0;
		}
		if (props == null || !props.TryGetValue((int)type, out var value))
		{
			value = 0L;
		}
		if (!propCfgInfo.Float)
		{
			return value;
		}
		return Long2Double(value);
	}

	public static double Long2Double(long val)
	{
		return (double)val / 10000.0;
	}

}
}
