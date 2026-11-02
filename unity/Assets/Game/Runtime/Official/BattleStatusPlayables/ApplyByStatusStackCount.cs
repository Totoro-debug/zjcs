// Migrated official status application source; see status view runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using UnityEngine;

namespace Zjcs.Official.BattleStatusPlayables
{
public class ApplyByStatusStackCount : ApplyOnInit
{
	private IChangePlayableStatusStackCount mChangePlayableStatusStackCount;

	private IFightStatusPlayableState mStatusComponent;

	protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
	}

	protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		mChangePlayableStatusStackCount = playable as IChangePlayableStatusStackCount;
		if (mChangePlayableStatusStackCount == null)
		{
			Debug.LogWarning("ApplyByStatusStackCount requires IChangePlayableStatusStackCount.");
			return;
		}
		MyFightStatus myFightStatus = playable.TryGetParam("Status") as MyFightStatus;
		if ((Object)(object)myFightStatus == (Object)null)
		{
			Debug.LogWarning("ApplyByStatusStackCount requires the original Status playable parameter.");
		}
		else
		{
			mStatusComponent = myFightStatus.Component;
			playable.OnStop += new Action<MyPlayable>(Playable_OnStop);
			mStatusComponent.OnStatusStackUpdate += new Action<IFightStatusPlayableState, int, int>(Component_OnStatusStackUpdate);
			Component_OnStatusStackUpdate(myFightStatus.Component, -1, myFightStatus.Component.StackedCount);
		}
	}

	protected override void OnDestroy()
	{
		Playable_OnStop(myPlayable);
		base.OnDestroy();
	}

	private void Playable_OnStop(MyPlayable playable)
	{
		if ((Object)(object)playable != (Object)null)
		{
			playable.OnStop -= new Action<MyPlayable>(Playable_OnStop);
		}
		if (mStatusComponent != null)
		{
			mStatusComponent.OnStatusStackUpdate -= new Action<IFightStatusPlayableState, int, int>(Component_OnStatusStackUpdate);
			mStatusComponent = null;
		}
		mChangePlayableStatusStackCount = null;
	}

	private void Component_OnStatusStackUpdate(IFightStatusPlayableState status, int fromCount, int toCount)
	{
		mChangePlayableStatusStackCount.SetPlayableStatusStackCount(toCount);
	}
}

}
