// Selected original Home TipDot condition body; explicit local ports.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeTipDots
{

public class HangUpNodeInstanceInfo : BaseNodeInstanceInfo
{
	public int HangUpId;

	public HangUpNodeInstanceInfo(int hangUpId)
	{
		HangUpId = hangUpId;
	}

	public override string GetInstanceDesc()
	{
		return $"道具ID：{HangUpId}";
	}

	public override bool Equals(object obj)
	{
		if (obj is HangUpNodeInstanceInfo hangUpNodeInstanceInfo)
		{
			return HangUpId == hangUpNodeInstanceInfo.HangUpId;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine<int>(HangUpId);
	}
}

}
