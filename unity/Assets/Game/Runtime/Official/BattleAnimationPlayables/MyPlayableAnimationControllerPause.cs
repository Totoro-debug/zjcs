// Migrated current official application source; see status pause runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAnimation;
using System;
using UnityEngine;

namespace Zjcs.Official.BattleAnimationPlayables
{
[MyPlayableApply(new Type[]
{
	typeof(AnimationController),
	typeof(Animator)
})]
public class MyPlayableAnimationControllerPause : MyPlayable
{
	[Header("动画暂停的基本配置")]
	[Tooltip("-1表示无限持续")]
	public float Duration;

	[Tooltip("结束时是否将animator参数还原")]
	public bool ResumeOnDestroy = true;

	private AnimationController mAnimationController;

	private bool mResumed;

	protected override void PlayImpl()
	{
		mResumed = false;
		mAnimationController = myApplyComponent as AnimationController;
		if ((Object)(object)mAnimationController == (Object)null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
		}
		else
		{
			mAnimationController.Pause();
		}
	}

	public override float GetDuration()
	{
		return Duration;
	}

	public override void Stop(bool immediate = false)
	{
		base.Stop(immediate);
		TryResume();
	}

	protected override void StopImpl()
	{
		TryResume();
		base.StopImpl();
	}

	private void TryResume()
	{
		if (!mResumed && !((Object)(object)mAnimationController == (Object)null) && ResumeOnDestroy)
		{
			mAnimationController.Resume();
			mResumed = true;
		}
	}
}

}
