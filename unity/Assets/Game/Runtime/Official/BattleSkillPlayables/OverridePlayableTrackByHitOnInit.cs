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
public class OverridePlayableTrackByHitOnInit : MyPlayableOnInit
{
	public List<MyTrack> Tracks = new List<MyTrack>();

	protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		MyFightHitFixed myFightHitFixed = default;
		if (!(playable is MyPlayableTrack myPlayableTrack))
		{

			Debug.LogWarning("OverridePlayableTrackByHitOnInit: invalid original playable target or type.");
		}
		else if (!((Object)(object)playable.myTarget == (Object)null) && playable.myTarget.TryGetComponent<MyFightHitFixed>(out myFightHitFixed))
		{
			int delayOverrideInfoIndex = myFightHitFixed.DelayOverrideInfoIndex;
			if (delayOverrideInfoIndex >= 0 && delayOverrideInfoIndex < Tracks.Count)
			{
				myPlayableTrack.myTrack = Tracks[delayOverrideInfoIndex];
			}
		}
	}

	protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
	}
}

}
