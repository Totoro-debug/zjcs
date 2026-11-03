// Complete current original application source; see track runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.EntityLifecycle;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleTrackPlayables
{
[MyPlayableApply(new Type[]
{
	typeof(MyTransform),
	typeof(Transform)
})]
public class MyPlayableTrack : MyPlayable, IPlayableOverrideAttachOffset
{
	[Header("曲线的基本配置")]
	[Tooltip("如果为空，则是一米每秒的直线轨迹")]
	public MyTrack myTrack;

	[Tooltip("轨迹速度")]
	public float myTrackSpeed = 1f;

	[Tooltip("轨迹起点")]
	public AttachPosCfg myStartAttachPos = new AttachPosCfg(EAttachTo.Source);

	[Tooltip("轨迹终点")]
	public AttachPosCfg myEndAttachPos = new AttachPosCfg(EAttachTo.Target);

	[Tooltip("true：在不同距离下，轨迹时间不变，false：在不同距离下，轨迹速度不变")]
	public bool myFixDuration;

	[Tooltip("是否每帧更新目标点的位置")]
	public bool myFollowTarget;

	[Tooltip("飞行物的额外缩放系数")]
	public float myAdditionScale = 1f;

	[Tooltip("轨迹结束后停留在结束位置多久")]
	public float myTrackEndStopTime;

	[Tooltip("随着轨迹百分比播放的触发器")]
	public List<MyPlayableTriggerTimeCfg> myTrackTriggerList;

	[Tooltip("配置为true，则在stop的时候把所有未触发的trackTrigger都触发掉")]
	public bool myTrackTriggerReliable = true;

	public bool myUseTrackRotation = true;

	public bool myUseTrackScale = true;

	[Tooltip("轨迹x轴偏移上，根据距离进行缩放的系数\n如果是0，则无论距离多远x轴偏移不变\n如果是1，x轴偏移随距离成正比缩放")]
	public float myXScaleByRange = 1f;

	[Tooltip("轨迹的x轴偏移是否随着角色的翻转而翻转")]
	public bool myFlipBySourceX = true;

	[Tooltip("轨迹的x轴偏移是否随着目标所在的Y轴坐标位置而翻转")]
	public bool myFlipByTargetY = true;

	[Tooltip("轨迹的x轴偏移是否随着方向X而翻转")]
	[SerializeField]
	private bool mFlipByDirX;

	[SerializeField]
	[Tooltip("Z轴是否等于Y")]
	private bool mIsZEquipY;

	private MyTransform mEcTransform;

	private Transform mTransform;

	private Vector3 mStartPos;

	private Vector3 mEndPos;

	private Vector3 mDir;

	private float mDist;

	private float mRealTrackSpeed = -1f;

	private float mDuration = -1f;

	private int mTrackTriggerIndex;

	private bool mFlip;

	private Vector3 mInitialPos;

	private Quaternion mInitialRot;

	private Vector3 mInitialScale;

	private float mEndStopStartTime;

	private MyPlayableApplyObj mApplyObj;

	protected override void InitImpl()
	{
		base.InitImpl();
		CalcInitialDurationSpd(out mDuration, out mRealTrackSpeed);
	}

	public void CalcInitialDurationSpd(out float duration, out float speed)
	{
		duration = -1f;
		speed = -1f;
		if ((Object)(object)myTrack == (Object)null)
		{
			if (myFixDuration)
			{
				duration = 1f / myTrackSpeed;
			}
			else
			{
				speed = myTrackSpeed;
			}
		}
		else if (myFixDuration)
		{
			duration = myTrack.GetDuration() / myTrackSpeed;
		}
		else
		{
			speed = myTrackSpeed / myTrack.GetDuration();
		}
	}

	private void InitDataOnPlay()
	{
		mEcTransform = myApplyComponent as MyTransform;
		Component val = myApplyComponent;
		mTransform = (Transform)(object)((val is Transform) ? val : null);
		if (!((Object)(object)mEcTransform == (Object)null) || !((Object)(object)mTransform == (Object)null))
		{
			mApplyObj = myApplyComponent.GetComponent<MyPlayableApplyObj>();
			if (myFadeInDuration > 0f)
			{
				mInitialPos = (((Object)(object)mEcTransform != (Object)null) ? mEcTransform.Position : mTransform.position);
				mInitialRot = (((Object)(object)mEcTransform != (Object)null) ? mEcTransform.Rotation : mTransform.rotation);
				mInitialScale = (((Object)(object)mEcTransform != (Object)null) ? mEcTransform.Scale : mTransform.localScale);
			}
			CalcInitialDurationSpd(out mDuration, out mRealTrackSpeed);
			InitStartPos();
			if (!myFollowTarget)
			{
				UpdateEndPos();
			}
			InitFlip();
			mTrackTriggerIndex = 0;
			mEndStopStartTime = 0f;
		}
	}

	protected override void PlayImpl()
	{
		base.PlayImpl();
		InitDataOnPlay();
	}

	private void InitFlip()
	{
		mFlip = false;
		if ((Object)(object)mySource == (Object)null)
		{
			return;
		}
		Transform transform = mySource.transform;
		if (myFlipBySourceX && transform.localScale.x < 0f)
		{
			mFlip = !mFlip;
		}
		if (!((Object)(object)myTarget == (Object)null))
		{
			Transform transform2 = myTarget.transform;
			if (myFlipByTargetY && transform2.position.y < transform.position.y)
			{
				mFlip = !mFlip;
			}
			if (mFlipByDirX && transform.position.x > transform2.position.x)
			{
				mFlip = !mFlip;
			}
		}
	}

	private void InitStartPos()
	{
		mStartPos = myStartAttachPos.CalcPos((MyPlayable)this);
		if ((Object)(object)mEcTransform != (Object)null)
		{
			mStartPos = ((IEntityContainerLocalPosition)mEcTransform.myContainer).ToLocalPos(mStartPos);
		}
	}

	private void UpdateEndPos()
	{
		if (myEndAttachPos.TryCalcPos(this, out var pos))
		{
			mEndPos = pos;
			if ((Object)(object)mEcTransform != (Object)null)
			{
				mEndPos = ((IEntityContainerLocalPosition)mEcTransform.myContainer).ToLocalPos(mEndPos);
			}
		}
		Vector3 val = mEndPos - mStartPos;
		val.z = 0f;
		mDir = val.normalized;
		mDist = val.magnitude;
		if (!myFixDuration)
		{
			mDuration = mDist / mRealTrackSpeed;
			mDuration = Mathf.Max(1E-05f, mDuration);
		}
	}

	private void CalcPos(float t, out Vector3 pos, out Quaternion rot, out Vector3 scale)
	{
		if ((Object)(object)myTrack == (Object)null)
		{
			if (mDuration == 0f)
			{
				pos = mEndPos;
			}
			else
			{
				pos = Vector3.Lerp(mStartPos, mEndPos, t);
			}
			rot = Quaternion.LookRotation(Vector3.forward, mDir);
			scale = Vector3.one;
		}
		else
		{
			float time = t * myTrack.GetDuration();
			myTrack.Evaluate(time, mStartPos, mEndPos, out pos, out rot, out scale, myXScaleByRange, (!mFlip) ? 1 : (-1));
		}
		if (mIsZEquipY)
		{
			pos = new Vector3(pos.x, pos.y, pos.y);
		}
		if (mFlip)
		{
			scale.x = 0f - scale.x;
		}
		scale *= myAdditionScale;
	}

	protected override bool ProcessFrameImpl(float deltaTime)
	{
		if ((Object)(object)mEcTransform == (Object)null && (Object)(object)mTransform == (Object)null)
		{
			return true;
		}
		if (mShouldEnd)
		{
			return true;
		}
		if (mEndStopStartTime == 0f)
		{
			if (myFollowTarget)
			{
				UpdateEndPos();
			}
			float num = Math.Min(1f, Time / mDuration);
			CalcPos(num, out var pos, out var rot, out var scale);
			if (myStatus == PlayableStatus.FadeIn)
			{
				float preferWeight = GetPreferWeight();
				pos = Vector3.Lerp(mStartPos, pos, preferWeight);
				float fadeInTime = GetFadeInTime();
				mApplyObj.FadeIn(fadeInTime);
			}
			if ((Object)(object)mEcTransform != (Object)null)
			{
				mEcTransform.Position = pos;
				if (myUseTrackRotation)
				{
					mEcTransform.Rotation = rot;
				}
				if (myUseTrackScale)
				{
					mEcTransform.Scale = scale;
				}
			}
			else
			{
				mTransform.position = pos;
				if (myUseTrackRotation)
				{
					mTransform.rotation = rot;
				}
				if (myUseTrackScale)
				{
					mTransform.localScale = scale;
				}
			}
			while (mTrackTriggerIndex < myTrackTriggerList.Count)
			{
				MyPlayableTriggerTimeCfg myPlayableTriggerTimeCfg = myTrackTriggerList[mTrackTriggerIndex];
				if (myPlayableTriggerTimeCfg.myTime > num)
				{
					break;
				}
				myPlayableTriggerTimeCfg.myTrigger.Play(this);
				mTrackTriggerIndex++;
			}
			if (myTrackEndStopTime > 0f && Time >= mDuration)
			{
				mEndStopStartTime = Time;
			}
		}
		if (myTrackEndStopTime == 0f)
		{
			return Time >= mDuration;
		}
		if (myTrackEndStopTime > 0f)
		{
			if (mEndStopStartTime > 0f)
			{
				return Time - mEndStopStartTime >= myTrackEndStopTime;
			}
			return false;
		}
		return false;
	}

	protected override void StopImpl()
	{
		mEndStopStartTime = 0f;
		if ((Object)(object)mApplyObj != (Object)null)
		{
			mApplyObj.ResumeFadeIn();
		}
		DoTrackTriggerReliable();
	}

	public override void StopBeforeResetData()
	{
		DoTrackTriggerReliable();
	}

	public override float GetDuration()
	{
		if (mDuration < 0f)
		{
			return -1f;
		}
		return mDuration + myTrackEndStopTime;
	}

	private void DoTrackTriggerReliable()
	{
		if (myTrackTriggerReliable)
		{
			while (mTrackTriggerIndex < myTrackTriggerList.Count)
			{
				myTrackTriggerList[mTrackTriggerIndex].myTrigger.Play(this);
				mTrackTriggerIndex++;
			}
		}
	}

	public void SetAttachOffset(Vector3 offset)
	{
		myStartAttachPos.myOffset = offset;
	}
}

}
