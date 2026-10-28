// Migrated original application fade handler; see Map13 source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.StoryPlayables;
using UnityEngine;


namespace Zjcs.Official.World.Map13EntityFadeHandlers
{
[System.Serializable]
public class EcMapEntityFadeOutDelayDestoryHandler : EcMapEntityFadeOutPlayableHandlerBase
{
	[Tooltip("物件销毁延迟时间用配置")]
	public float FadeOutTime;

	protected float mEndTime = float.MaxValue;

	protected float mSecondPlayableTime = float.MaxValue;

	private float mAppendFadeOutTime;

	private GameObject mAppendFadeOutPlayable;

	public void SetAppendFadeOut(GameObject appendFadeOutPlayable, float time)
	{
		mAppendFadeOutPlayable = appendFadeOutPlayable;
		mAppendFadeOutTime = time;
	}

	protected override bool CheckEnd()
	{
		return Time.time >= mEndTime;
	}

	protected override void Play()
	{
		base.Play();
		if (Time.time >= mSecondPlayableTime && mAppendFadeOutTime > 0f)
		{
			if ((Object)(object)myPlayable != (Object)null && myPlayable.myStatus != MyPlayable.PlayableStatus.Stop)
			{
				myPlayable.Stop();
				myPlayable = null;
			}
			mSecondPlayableTime = float.MaxValue;
			base.PlayPlayable();
		}
	}

	protected override void PlayImpl()
	{
		base.PlayImpl();
		if ((Object)(object)myPlayable != (Object)null)
		{
			myPlayable.myFadeOutDuration = FadeOutTime;
		}
		mSecondPlayableTime = Time.time + mAppendFadeOutTime;
		mEndTime = mSecondPlayableTime + FadeOutTime;
	}

	protected override void PlayPlayable()
	{
		if (mAppendFadeOutTime <= 0f)
		{
			base.PlayPlayable();
		}
		else
		{
			myPlayable = mECPlayableSupporterView.PlayPlayable(mAppendFadeOutPlayable, mSrcGo, mSrcGo);
		}
	}

	protected override void StopImpl()
	{
		mEndTime = float.MaxValue;
		mSecondPlayableTime = float.MaxValue;
		mAppendFadeOutTime = 0f;
		mAppendFadeOutPlayable = null;
		base.StopImpl();
	}
}

}
