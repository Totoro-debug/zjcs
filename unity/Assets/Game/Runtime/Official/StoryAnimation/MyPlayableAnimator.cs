// Migrated official source; see map14-story-animator-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

namespace Zjcs.Official.StoryAnimation
{
[MyPlayableApply(new Type[]
{
	typeof(AnimationController),
	typeof(Animator)
})]
public class MyPlayableAnimator : MyPlayable
{
	[Header("动画的基本配置")]
	public string myBoolParam;

	public bool myBoolValue;

	public string myFloatParam;

	public float myFloatValue;

	public string IntParam;

	public int IntValue;

	public string myTriggerParam;

	[Tooltip("前置状态限制。期望播放动画时，当前正在播放的动画是以下列表中的某一个。若不是，则不进行播放。")]
	public List<string> PreStateNameAllowList;

	[Tooltip("-1表示无限持续")]
	public float myDuration;

	[Tooltip("动画层的名称")]
	public string LayerName;

	[Tooltip("动画层的期望权重，范围：[0,1]")]
	public float LayerWeight;

	[Tooltip("结束时是否将animator参数还原")]
	public bool myEndResumeParam;

	private Animator mAnimator;

	private bool mPreBoolValue;

	private float mPreFloatValue;

	private int mPreIntValue;

	private float mPreLayerWeightValue;

	private bool mResumed;

	private AnimationController mAnimationController;

	private int mInstanceID;

	private int mBoolParamHash;

	private int mFloatParamHash;

	private int mIntParamHash;

	private int mTriggerParamHash;

	private int mLayerIndex = -1;

	protected override void Awake()
	{
		base.Awake();
		mInstanceID = ((Object)this).GetInstanceID();
		UpdateHashMapString();
	}

	public void UpdateHashMapString()
	{
		mBoolParamHash = Animator.StringToHash(myBoolParam);
		mFloatParamHash = Animator.StringToHash(myFloatParam);
		mIntParamHash = Animator.StringToHash(IntParam);
		mTriggerParamHash = Animator.StringToHash(myTriggerParam);
	}

	protected override void PlayImpl()
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		mAnimationController = myApplyComponent as AnimationController;
		if ((Object)(object)mAnimationController != (Object)null)
		{
			mAnimator = mAnimationController.myAnimator;
		}
		else
		{
			Component val = myApplyComponent;
			mAnimator = (Animator)(object)((val is Animator) ? val : null);
		}
		if ((Object)(object)mAnimator == (Object)null)
		{
			return;
		}
		if (PreStateNameAllowList.Count > 0)
		{
			bool flag = false;
			AnimatorStateInfo currentAnimatorStateInfo = mAnimator.GetCurrentAnimatorStateInfo(0);
			foreach (string preStateNameAllow in PreStateNameAllowList)
			{
				if (currentAnimatorStateInfo.IsName(preStateNameAllow))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return;
			}
		}
		if (mBoolParamHash != 0)
		{
			if (myEndResumeParam)
			{
				mPreBoolValue = mAnimator.GetBool(mBoolParamHash);
			}
			SetBool(mBoolParamHash, myBoolValue, myEndResumeParam);
		}
		if (mFloatParamHash != 0)
		{
			if (myEndResumeParam)
			{
				mPreFloatValue = mAnimator.GetFloat(myFloatParam);
			}
			SetFloat(mFloatParamHash, myFloatValue, myEndResumeParam);
		}
		if (mIntParamHash != 0)
		{
			if (myEndResumeParam)
			{
				mPreIntValue = mAnimator.GetInteger(mIntParamHash);
			}
			SetInteger(mIntParamHash, IntValue, myEndResumeParam);
		}
		if (mTriggerParamHash != 0)
		{
			SetTrigger(mTriggerParamHash);
		}
		mLayerIndex = mAnimator.GetLayerIndex(LayerName);
		if (mLayerIndex >= 0)
		{
			if (myEndResumeParam)
			{
				mPreLayerWeightValue = mAnimator.GetLayerWeight(mLayerIndex);
			}
			SetLayerWeight(mLayerIndex, LayerWeight);
		}
	}

	protected void SetBool(int paramKey, bool paramValue, bool isEndResume)
	{
		if ((Object)(object)mAnimationController != (Object)null)
		{
			mAnimationController.SetBool(mInstanceID, paramKey, paramValue, isEndResume);
		}
		else
		{
			mAnimator.SetBool(paramKey, paramValue);
		}
	}

	protected void SetFloat(int paramKey, float paramValue, bool isEndResume)
	{
		if ((Object)(object)mAnimationController != (Object)null)
		{
			mAnimationController.SetFloat(mInstanceID, paramKey, paramValue, isEndResume);
		}
		else
		{
			mAnimator.SetFloat(paramKey, paramValue);
		}
	}

	protected void SetInteger(int paramKey, int paramValue, bool isEndResume)
	{
		if ((Object)(object)mAnimationController != (Object)null)
		{
			mAnimationController.SetInteger(mInstanceID, paramKey, paramValue, isEndResume);
		}
		else
		{
			mAnimator.SetInteger(paramKey, paramValue);
		}
	}

	protected void SetTrigger(int paramKey)
	{
		if ((Object)(object)mAnimationController != (Object)null)
		{
			mAnimationController.SetTrigger(mInstanceID, paramKey);
		}
		else
		{
			mAnimator.SetTrigger(paramKey);
		}
	}

	protected void SetLayerWeight(int layerIndex, float layerWeight)
	{
		mAnimator.SetLayerWeight(layerIndex, layerWeight);
	}

	protected void ResumeBool(int paramKey)
	{
		if ((Object)(object)mAnimationController != (Object)null)
		{
			mAnimationController.ResumeBool(mInstanceID, paramKey);
		}
		else
		{
			mAnimator.SetBool(paramKey, mPreBoolValue);
		}
	}

	protected void ResumeFloat(int paramKey)
	{
		if ((Object)(object)mAnimationController != (Object)null)
		{
			mAnimationController.ResumeFloat(mInstanceID, paramKey);
		}
		else
		{
			mAnimator.SetFloat(paramKey, mPreFloatValue);
		}
	}

	protected void ResumeInteger(int paramKey)
	{
		if ((Object)(object)mAnimationController != (Object)null)
		{
			mAnimationController.ResumeInteger(mInstanceID, paramKey);
		}
		else
		{
			mAnimator.SetInteger(paramKey, mPreIntValue);
		}
	}

	protected void ResumeLayerWeight(int layerIndex)
	{
		mAnimator.SetLayerWeight(layerIndex, mPreLayerWeightValue);
	}

	protected override void FadeOutImpl()
	{
		TryResumeParam();
	}

	private void TryResumeParam()
	{
		if (myEndResumeParam && !mResumed && !((Object)(object)mAnimator == (Object)null))
		{
			if (mBoolParamHash != 0)
			{
				ResumeBool(mBoolParamHash);
			}
			if (mFloatParamHash != 0)
			{
				ResumeFloat(mFloatParamHash);
			}
			if (mIntParamHash != 0)
			{
				ResumeInteger(mIntParamHash);
			}
			if (mLayerIndex >= 0)
			{
				ResumeLayerWeight(mLayerIndex);
			}
			mResumed = true;
		}
	}

	protected override void StopImpl()
	{
		TryResumeParam();
		mAnimator = null;
		mPreBoolValue = false;
		mPreFloatValue = 0f;
		mPreIntValue = 0;
		mPreLayerWeightValue = 0f;
		mResumed = false;
		mAnimationController = null;
		mLayerIndex = -1;
		base.StopImpl();
	}

	public override float GetDuration()
	{
		return myDuration;
	}

	public override void Stop(bool immediate = false)
	{
		base.Stop(immediate);
		TryResumeParam();
	}
}

}
