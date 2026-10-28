// Migrated original source; see map14-story-animator-timeline receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryAnimation;
using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Zjcs.Official.StoryAnimatorTimeline
{
[Serializable]
public class TlAnimatorParamBehaviour : PlayableBehaviour
{
	[Tooltip("结束后是否恢复原始属性")]
	public bool EndResume = true;

	public string BoolKey;

	public bool BoolValue;

	private bool BoolOriginValue;

	public string IntKey;

	public int IntValue;

	private int IntOriginValue;

	public string FloatKey;

	public float FloatValue;

	private float FloatOriginValue;

	public string TriggerKey;

	protected Animator myAnimator;

	private bool myFirstFrame = true;

	private int mBoolKeyHash;

	private int mIntKeyHash;

	private int mFloatKeyHash;

	private int mTriggerHash;

	public override void OnPlayableCreate(Playable playable)
	{
		base.OnPlayableCreate(playable);
		mBoolKeyHash = Animator.StringToHash(BoolKey);
		mIntKeyHash = Animator.StringToHash(IntKey);
		mFloatKeyHash = Animator.StringToHash(FloatKey);
		mTriggerHash = Animator.StringToHash(TriggerKey);
	}

	protected virtual Animator GetAnimator(object playerData)
	{
		GameObject val = (GameObject)((playerData is GameObject) ? playerData : null);
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		AnimationController component = val.GetComponent<AnimationController>();
		if ((Object)(object)component != (Object)null)
		{
			return component.myAnimator;
		}
		return val.GetComponent<Animator>();
	}

	protected virtual void PrepareAfterGetAnimator(object playable)
	{
	}

	public override void OnBehaviourPause(Playable playable, FrameData info)
	{
		myFirstFrame = true;
		if (EndResume && !((Object)(object)myAnimator == (Object)null))
		{
			if (mBoolKeyHash != 0)
			{
				myAnimator.SetBool(mBoolKeyHash, BoolOriginValue);
			}
			if (mIntKeyHash != 0)
			{
				myAnimator.SetInteger(mIntKeyHash, IntOriginValue);
			}
			if (mFloatKeyHash != 0)
			{
				myAnimator.SetFloat(mFloatKeyHash, FloatOriginValue);
			}
		}
	}

	public override void ProcessFrame(Playable playable, FrameData info, object playerData)
	{
		if (!myFirstFrame)
		{
			return;
		}
		myFirstFrame = false;
		myAnimator = GetAnimator(playerData);
		PrepareAfterGetAnimator(playerData);
		if ((Object)(object)myAnimator == (Object)null)
		{
			Debug.LogError("TlAnimatorParamBehaviour myAnimator == null");
			return;
		}
		if (mBoolKeyHash != 0)
		{
			BoolOriginValue = myAnimator.GetBool(mBoolKeyHash);
			myAnimator.SetBool(mBoolKeyHash, BoolValue);
		}
		if (mIntKeyHash != 0)
		{
			IntOriginValue = myAnimator.GetInteger(mIntKeyHash);
			myAnimator.SetInteger(mIntKeyHash, IntValue);
		}
		if (mFloatKeyHash != 0)
		{
			FloatOriginValue = myAnimator.GetFloat(mFloatKeyHash);
			myAnimator.SetFloat(mFloatKeyHash, FloatValue);
		}
		if (mTriggerHash != 0)
		{
			myAnimator.SetTrigger(mTriggerHash);
			myAnimator.Update(0f);
		}
	}
}

}
