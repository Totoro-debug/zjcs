// Original client home animation clock; local message boundaries only.
namespace Zjcs.Official.HomeNativeTime
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

[Serializable]
public abstract class BaseTimeDriver : HomeEnvDrive, IHomeEnvDriveUpdatable, IHomeEnvDriveInjectable
{
	public List<SkeletonAnimation> SkeletonAnimations;

	public List<Animation> Animations;

	private readonly List<SkeletonAnimation> mDynamicSkeletonAnimations = new List<SkeletonAnimation>();

	private readonly List<Animator> mAnimators = new List<Animator>();

	private readonly List<Animation> mDynamicAnimations = new List<Animation>();

	private List<IDisposable> mDisposable;

	private float mTimeScale;

	private bool mIsActive;
	
	public IHomeTimeDriveEvents HomeTimeDriveSkeletonSubscriber { get; set; }
	public IHomeTimeDriveEvents HomeTimeDriveAnimatorSubscriber { get; set; }
	public IHomeTimeDriveEvents HomeTimeDriveAnimationSubscriber { get; set; }

	public override void Init(IHomeNativeTimeManager manager)
	{
		base.Init(manager);
		foreach (SkeletonAnimation skeletonAnimation in SkeletonAnimations)
		{
			skeletonAnimation.UpdateTiming = (UpdateTiming)0;
		}
		mDisposable = new List<IDisposable>();
		mDisposable.Add(HomeTimeDriveSkeletonSubscriber.Subscribe(OnHandleHomeTimeSkeletonDriveData));
		mDisposable.Add(HomeTimeDriveAnimatorSubscriber.Subscribe(OnHandleHomeTimeAnimatorDriveData));
		mDisposable.Add(HomeTimeDriveAnimationSubscriber.Subscribe(OnHandleHomeTimeAnimationDriveData));
	}

	public override void OnEnable()
	{
		base.OnEnable();
		mIsActive = true;
	}

	public override void OnDisable()
	{
		mIsActive = false;
		ResetDynamicDriveObjects();
		base.OnDisable();
	}

	public override void OnDestroy()
	{
		if (mDisposable != null) { foreach (var subscription in mDisposable) subscription.Dispose(); mDisposable = null; }
		base.OnDestroy();
	}

	private void OnHandleHomeTimeSkeletonDriveData(HomeTimeDriveSkeletonData data)
	{
		if (!mIsActive)
		{
			return;
		}
		if (data.IsRegister)
		{
			foreach (SkeletonAnimation skeletonAnimation in data.SkeletonAnimationList)
			{
				skeletonAnimation.UpdateTiming = (UpdateTiming)0;
				mDynamicSkeletonAnimations.Add(skeletonAnimation);
			}
			return;
		}
		foreach (SkeletonAnimation skeletonAnimation2 in data.SkeletonAnimationList)
		{
			skeletonAnimation2.UpdateTiming = (UpdateTiming)1;
			mDynamicSkeletonAnimations.Remove(skeletonAnimation2);
		}
	}

	private void OnHandleHomeTimeAnimatorDriveData(HomeTimeDriveAnimatorData data)
	{
		if (mIsActive)
		{
			if (data.IsRegister)
			{
				data.Animator.speed = mTimeScale;
				mAnimators.Add(data.Animator);
			}
			else
			{
				data.Animator.speed = 1f;
				mAnimators.Remove(data.Animator);
			}
		}
	}

	private void OnHandleHomeTimeAnimationDriveData(HomeTimeDriveAnimationData data)
	{
		if (mIsActive)
		{
			if (data.IsRegister)
			{
				SetAnimationTimeScale(data.Animation, mTimeScale);
				mDynamicAnimations.Add(data.Animation);
			}
			else
			{
				SetAnimationTimeScale(data.Animation, 1f);
				mDynamicAnimations.Remove(data.Animation);
			}
		}
	}

	public virtual void Update()
	{
		float playableDeltaTime = mManager.PlayableDeltaTime;
		foreach (SkeletonAnimation skeletonAnimation in SkeletonAnimations)
		{
			if (((Behaviour)skeletonAnimation).isActiveAndEnabled)
			{
				skeletonAnimation.Update(playableDeltaTime);
			}
		}
		foreach (SkeletonAnimation mDynamicSkeletonAnimation in mDynamicSkeletonAnimations)
		{
			mDynamicSkeletonAnimation.Update(playableDeltaTime);
		}
		UpdateTimeScale();
	}

	private void UpdateTimeScale()
	{
		if (!(Math.Abs(mTimeScale - mManager.PlayableTimeScale) > 0.001f))
		{
			return;
		}
		mTimeScale = mManager.PlayableTimeScale;
		foreach (Animator mAnimator in mAnimators)
		{
			mAnimator.speed = mTimeScale;
		}
		foreach (Animation animation in Animations)
		{
			SetAnimationTimeScale(animation, mTimeScale);
		}
		foreach (Animation mDynamicAnimation in mDynamicAnimations)
		{
			SetAnimationTimeScale(mDynamicAnimation, mTimeScale);
		}
	}

	private void ResetDynamicDriveObjects()
	{
		foreach (Animator mAnimator in mAnimators)
		{
			if ((Object)(object)mAnimator != (Object)null)
			{
				mAnimator.speed = 1f;
			}
		}
		mAnimators.Clear();
		foreach (SkeletonAnimation mDynamicSkeletonAnimation in mDynamicSkeletonAnimations)
		{
			if ((Object)(object)mDynamicSkeletonAnimation != (Object)null)
			{
				mDynamicSkeletonAnimation.UpdateTiming = (UpdateTiming)1;
			}
		}
		mDynamicSkeletonAnimations.Clear();
		foreach (Animation mDynamicAnimation in mDynamicAnimations)
		{
			if ((Object)(object)mDynamicAnimation != (Object)null)
			{
				SetAnimationTimeScale(mDynamicAnimation, 1f);
			}
		}
		mDynamicAnimations.Clear();
	}

	private void SetAnimationTimeScale(Animation animation, float timeScale)
	{
		foreach (AnimationState item in animation)
		{
			item.speed = timeScale;
		}
	}
}

}
