// Original client component; see first-promotion status runtime source receipt.
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleStatusPlayables;
namespace Zjcs.Official.BattleGridItemEntities
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

public class MyFightStatusShieldBase<T, TInfo> : MyFightStatusBase<T, TInfo> where T : BattleStatusShieldViewData
{
	[Tooltip("Playable-护盾用完时播放")]
	public GameObject RunOutPlayable;

	[Tooltip("护盾用完时播放的Playable是否在Destory时停止")]
	public bool RunOutStopOnDestory = true;

	protected MyPlayable mCurPlayable;

	protected MyPlayable mRunOutPlayable;

	protected GameObject ApplyEntity => Component.ApplyObject;

	public override void OnEnd(object userData)
	{
		base.OnEnd(userData);
		if ((Object)(object)mCurPlayable != (Object)null)
		{
			mCurPlayable.Stop();
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if ((Object)(object)mRunOutPlayable != (Object)null && RunOutStopOnDestory)
		{
			OnRunOutPlayableStop(mRunOutPlayable);
		}
	}

	public void OnRunOutPlayableStop(MyPlayable runOutPlayable)
	{
		runOutPlayable.Stop();
		runOutPlayable.OnStop -= new Action<MyPlayable>(OnRunOutPlayableStop);
	}
}
}
