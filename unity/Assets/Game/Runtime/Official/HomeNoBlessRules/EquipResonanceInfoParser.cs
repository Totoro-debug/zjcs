// Selected original 884 no-blessing implementation; typed data/configuration boundaries only.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zjcs.Official.HomeExperienceRules;
namespace Zjcs.Official.HomeNoBlessRules
{
public sealed class EquipResonanceInfoParser
{
    private List<EquipResonanceInfo> mResonances;
    public uint MinLevel { get; private set; }
    public uint MaxLevel { get; private set; }
    public EquipResonanceInfoParser(IEnumerable<EquipResonanceInfo> rows) { PostParse(rows.ToDictionary(row=>row.Level)); }
	private void PostParse(Dictionary<int, EquipResonanceInfo> configs)
	{
		mResonances = new List<EquipResonanceInfo>(configs.Count);
		MinLevel = (uint)configs.Keys.Min();
		MaxLevel = (uint)configs.Keys.Max();
		Dictionary<int, long> dictionary = new Dictionary<int, long>();
		for (uint num = MinLevel; num <= MaxLevel; num++)
		{
			EquipResonanceInfo equipResonanceInfo = configs[(int)num];
			mResonances.Insert(Index(num), equipResonanceInfo);
			equipResonanceInfo.Condition = new EquipLevelCountCondition
			{
				Level = equipResonanceInfo.EquipLevel,
				Count = equipResonanceInfo.EquipCount
			};
			foreach (KeyValuePair<int, long> item in equipResonanceInfo.Effect)
			{
				if (dictionary.ContainsKey(item.Key))
				{
					dictionary[item.Key] += item.Value;
				}
				else
				{
					dictionary[item.Key] = item.Value;
				}
			}
			equipResonanceInfo.TotalEffect = new ReadOnlyDictionary<int, long>(new Dictionary<int, long>(dictionary));
		}
	}

	private int Index(uint level)
	{
		return (int)(level - MinLevel);
	}

	public EquipResonanceInfo Get(uint level)
	{
		int num = Index(level);
		if (num < 0 || num >= mResonances.Count)
		{
			return null;
		}
		return mResonances[num];
	}

	public void CalcEquipResonanceProps(uint activedEquipResonanceLevel, Dictionary<int, long> props)
	{
		props.Clear();
		EquipResonanceInfo equipResonanceInfo = Get(activedEquipResonanceLevel);
		if (equipResonanceInfo == null)
		{
			return;
		}
		foreach (KeyValuePair<int, long> item in equipResonanceInfo.TotalEffect)
		{
			props.Add(item.Key, item.Value);
		}
	}

	public uint SearchCurrentLevel(Func<EquipResonanceInfo, bool> checkReachLevelFunc)
	{
		int result = 0;
		int num = 0;
		int num2 = mResonances.Count - 1;
		while (num <= num2)
		{
			int num3 = (num2 + num) / 2;
			EquipResonanceInfo equipResonanceInfo = mResonances[num3];
			if (checkReachLevelFunc(equipResonanceInfo))
			{
				result = equipResonanceInfo.Level;
				num = num3 + 1;
			}
			else
			{
				num2 = num3 - 1;
			}
		}
		return (uint)result;
	}

}

}
