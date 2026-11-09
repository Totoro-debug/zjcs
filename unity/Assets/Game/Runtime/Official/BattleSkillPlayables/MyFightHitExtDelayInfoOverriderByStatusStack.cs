// Migrated official hit presentation; see hit-view-runtime source receipt.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.BattleTrackPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.BattleSkillPlayables
{
[RequireComponent(typeof(MyFightSkill))]
public class MyFightHitExtDelayInfoOverriderByStatusStack : MyComponent<FightHitExtDelayInfoOverriderByStatusStackComponent, FightHitExtDelayInfoOverriderByStatusStackComponentInfo>
{
	public IFightHitSourceStatusServices StatusServices { get; set; }

	public int GetOverriderIndex()
	{
		MyFightSkill myFightSkill = default;
		if (!TryGetComponent<MyFightSkill>(out myFightSkill)) return -1;
		var statusAgent = StatusServices.GetSourceStatusAgent(myFightSkill);
		if (statusAgent == null) return -1;
		return GetStatusCount(statusAgent);
	}

	private int GetStatusCount(FightHitSourceStatusAgent statusAgent)
	{
		if (statusAgent.StatusToShareStack.TryGetValue(Info.StatusId, out var value))
		{
			return value - 1;
		}
		foreach (KeyValuePair<EcEntityIdentifier, FightHitSourceStatus> item in statusAgent.ApplyStatusDict)
		{
			FightHitSourceStatus value2 = item.Value;
			if (value2.Entity.ClassId == Info.StatusId)
			{
				return value2.StackedCount - 1;
			}
		}
		return -1;
	}
}

}
