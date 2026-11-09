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
public class OverridePlayableAttachOffsetByHitOnInit : MyPlayableOnInit
{
	protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{

		MyFightHitFixed myFightHitFixed = default;
		if (!(playable is IPlayableOverrideAttachOffset playableOverrideAttachOffset))
		{

			Debug.LogWarning("OverridePlayableAttachOffsetByHitOnInit: invalid original playable target or type.");
		}
		else if (!((Object)(object)playable.myTarget == (Object)null) && playable.myTarget.TryGetComponent<MyFightHitFixed>(out myFightHitFixed))
		{
			playableOverrideAttachOffset.SetAttachOffset(myFightHitFixed.DelayInfoProvider.Offset);
		}
	}

	protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
	}
}

}
