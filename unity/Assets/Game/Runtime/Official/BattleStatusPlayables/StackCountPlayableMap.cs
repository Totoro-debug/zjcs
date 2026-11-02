// Migrated current official application source; see status playable runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryAttachment;
using System;
using UnityEngine;

namespace Zjcs.Official.BattleStatusPlayables
{
[Serializable]
public struct StackCountPlayableMap
{
	[Tooltip("触发层数下限（含）")]
	public int StackCount;

	[Tooltip("触发层数上限（含）。为0或小于下限时退化为对 StackCount 的精确匹配")]
	public int MaxStackCount;

	public GameObject PlayablePrefab;

	public GameObject BeginPlayable;

	public GameObject EndPlayable;

	public bool Match(int stack)
	{
		if (MaxStackCount >= StackCount)
		{
			if (stack >= StackCount)
			{
				return stack <= MaxStackCount;
			}
			return false;
		}
		return stack == StackCount;
	}
}

}
