// Selected Console.dll; explicit data, clock, config and disabled-function ports.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeOutputAddition
{
public static class OutputAdditionUtils
{
	public static List<(List<OutputAdditionInfo> propList, DateTime endTime)> GetCurrOutputGroupPropList(HomeOutputAdditionPlayer player, OutputGroupType outputGroupType)
	{
		List<(List<OutputAdditionInfo>, DateTime)> list = new List<(List<OutputAdditionInfo>, DateTime)>();
		DateTime dateTime = player.GetDateTime();
		if (player.CheckFunctionDisabled(null))
		{
			return list;
		}
		OutputAdditionInfoParser outputAdditionInfoParser = player.Configuration;
		foreach (PlayerOutputAdditionDataWrap value in player.mAllOutputAdditionDict.Values)
		{
			if (!IsTimeInRange(value.StartTime, value.EndTime, dateTime))
			{
				continue;
			}
			List<OutputAdditionInfo> list2 = new List<OutputAdditionInfo>();
			foreach (PlayerOutputAdditionPropTypeDataWrap value2 in value.OutputAdditionPropDict.Values)
			{
				OutputAdditionInfo outputAdditionInfo = outputAdditionInfoParser.Get(value2.ClassId);
				if (outputAdditionInfo.OutputGroupType == outputGroupType && IsTimeInRange(value2.StartTime, value2.EndTime, dateTime))
				{
					list2.Add(outputAdditionInfo);
				}
			}
			List<OutputAdditionInfo> outputAdditionInfoList = GetOutputAdditionInfoList(player, value, list2);
			if (outputAdditionInfoList.Count > 0)
			{
				list.Add((outputAdditionInfoList, value.EndTime));
			}
		}
		return list;
	}

	public static List<int> GetCurrOutputActionClassIdList(HomeOutputAdditionPlayer player, OutputActionType outputActionType)
	{
		List<int> list = new List<int>();
		DateTime dateTime = player.GetDateTime();
		if (player.CheckFunctionDisabled(null))
		{
			return list;
		}
		OutputAdditionInfoParser outputAdditionInfoParser = player.Configuration;
		foreach (PlayerOutputAdditionDataWrap value2 in player.mAllOutputAdditionDict.Values)
		{
			if (value2.OutputActionType != outputActionType || !IsTimeInRange(value2.StartTime, value2.EndTime, dateTime))
			{
				continue;
			}
			foreach (KeyValuePair<OutputAdditionPropType, PlayerOutputAdditionPropTypeDataWrap> item in value2.OutputAdditionPropDict)
			{
				_ = item.Key;
				PlayerOutputAdditionPropTypeDataWrap value = item.Value;
				if (outputAdditionInfoParser.Get(value.ClassId) != null && IsTimeInRange(value.StartTime, value.EndTime, dateTime))
				{
					int key = (value.ClassId);
					if (!player.CheckFunctionDisabled(key))
					{
						list.Add(value.ClassId);
					}
				}
			}
		}
		return list;
	}

	private static List<OutputAdditionInfo> GetOutputAdditionInfoList(HomeOutputAdditionPlayer player, PlayerOutputAdditionDataWrap outputAdditionData, List<OutputAdditionInfo> dataList)
	{
		List<OutputAdditionInfo> list = new List<OutputAdditionInfo>();
		foreach (OutputAdditionInfo data in dataList)
		{
			if (outputAdditionData.OutputAdditionPropDict.TryGetValue(data.OutputAdditionPropType, out var _))
			{
				int key = (data.ClassId);
				if (!player.CheckFunctionDisabled(key))
				{
					list.Add(data);
				}
			}
		}
		return list;
	}

	public static List<OutputActionType> GetActivityOutputActionTypeList(HomeOutputAdditionPlayer player)
	{
		List<OutputActionType> list = new List<OutputActionType>();
		if (player.CheckFunctionDisabled(null))
		{
			return list;
		}
		DateTime dateTime = player.GetDateTime();
		foreach (PlayerOutputAdditionDataWrap value2 in player.mAllOutputAdditionDict.Values)
		{
			if (!IsTimeInRange(value2.StartTime, value2.EndTime, dateTime))
			{
				continue;
			}
			bool flag = true;
			foreach (KeyValuePair<OutputAdditionPropType, PlayerOutputAdditionPropTypeDataWrap> item in value2.OutputAdditionPropDict)
			{
				_ = item.Key;
				PlayerOutputAdditionPropTypeDataWrap value = item.Value;
				if (IsTimeInRange(value.StartTime, value.EndTime, dateTime))
				{
					int key = (value.ClassId);
					if (!player.CheckFunctionDisabled(key))
					{
						flag = false;
						break;
					}
				}
			}
			if (!flag)
			{
				list.Add(value2.OutputActionType);
			}
		}
		return list;
	}

	public static int GetOutputAdditionPropValue(this HomeOutputAdditionPlayer player, OutputAdditionPropType outputAdditionPropType)
	{
		if (player.CheckFunctionDisabled(null))
		{
			return 0;
		}
		OutputAdditionInfoParser outputAdditionInfoParser = player.Configuration;
		DateTime dateTime = player.GetDateTime();
		int num = 0;
		foreach (PlayerOutputAdditionDataWrap value2 in player.mAllOutputAdditionDict.Values)
		{
			if (value2.EndTime < dateTime || value2.StartTime > dateTime || !value2.OutputAdditionPropDict.TryGetValue(outputAdditionPropType, out var value))
			{
				continue;
			}
			int key = (value.ClassId);
			if (!player.CheckFunctionDisabled(key))
			{
				OutputAdditionInfo outputAdditionInfo = outputAdditionInfoParser.Get(value.ClassId);
				if (outputAdditionInfo != null)
				{
					num += outputAdditionInfo.OutputAdditionPropVal;
				}
			}
		}
		return num;
	}

	public static float ToPercentage(int val)
	{
		return Formula.Int2Float(val) * 100f;
	}

	public static bool IsTimeInRange(DateTime startTime, DateTime endTime, DateTime time)
	{
		if (time >= startTime)
		{
			return time < endTime;
		}
		return false;
	}
}
}
