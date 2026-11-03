using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

public class MyPlayableGrapplingLine : MyPlayable
{
	[Header("抓钩的基本配置")]
	[SerializeField]
	[Tooltip("抓钩起点")]
	private AttachPosCfg mStartAttachPos = new AttachPosCfg(EAttachTo.Source);

	[SerializeField]
	[Tooltip("抓钩目标")]
	private AttachPosCfg mTargetAttachPos = new AttachPosCfg(EAttachTo.Target);

	[SerializeField]
	[Tooltip("抓钩出去时间")]
	private float mOutDuration = 10f;

	[SerializeField]
	[Tooltip("抓钩出去速度，出去时间<=0时使用速度")]
	private float mOutSpeed = 10f;

	[SerializeField]
	[Tooltip("抓钩停留时间")]
	private float mStandDuration = 0.2f;

	[SerializeField]
	[Tooltip("抓钩回来时间")]
	private float mBackDuration = 0.2f;

	[SerializeField]
	[Tooltip("抓钩回来速度，回来时间<=0时使用速度")]
	private float mBackSpeed = 10f;

	[SerializeField]
	[Tooltip("抓钩起点是否跟随骨骼点")]
	private bool mFollowStartAttach;

	private GrapplingLineCtrl mGrapplingLineCtrl;

	private Vector3 mStartPos;

	private Vector3 mTargetPos;

	private float mRealOutDuration;

	private float mRealBackDuration;

	private float mDuration;

	private GameObject mFollowStartBone;

	private bool mFollowStartBoneOriginActive;

	protected override void PlayImpl()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		base.PlayImpl();
		mGrapplingLineCtrl = myApplyComponent.GetComponent<GrapplingLineCtrl>();
		if ((Object)(object)mGrapplingLineCtrl == (Object)null)
		{
			return;
		}
		mStartPos = mStartAttachPos.CalcPos((MyPlayable)this);
		mTargetPos = mTargetAttachPos.CalcPos((MyPlayable)this);
		Vector3 val = mTargetPos - mStartPos;
		val.z = 0f;
		float magnitude = val.magnitude;
		if (!TryGetOutDuration(magnitude, out mRealOutDuration) || !TryGetBackDuration(magnitude, out mRealBackDuration))
		{
			return;
		}
		mDuration = mRealOutDuration + mStandDuration + mRealBackDuration;
		mGrapplingLineCtrl.Play(mRealOutDuration, mStandDuration, mRealBackDuration, mStartPos, mTargetPos);
		if (mFollowStartAttach)
		{
			mFollowStartBone = mStartAttachPos.GetAttachToBone(this);
			if ((Object)(object)mFollowStartBone != (Object)null)
			{
				mFollowStartBoneOriginActive = mFollowStartBone.activeSelf;
				mFollowStartBone.SetActive(true);
			}
		}
	}

	protected override bool PostProcessFrameImpl(float deltaTime)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		if (mFollowStartAttach)
		{
			mGrapplingLineCtrl.FixOriginPosition(mStartAttachPos.CalcPos((MyPlayable)this));
		}
		mGrapplingLineCtrl.Render();
		return base.PostProcessFrameImpl(deltaTime);
	}

	protected override void StopImpl()
	{
		if ((Object)(object)mGrapplingLineCtrl != (Object)null)
		{
			mGrapplingLineCtrl.Stop();
		}
		if (mFollowStartAttach && (Object)(object)mFollowStartBone != (Object)null)
		{
			mFollowStartBone.SetActive(mFollowStartBoneOriginActive);
		}
		base.StopImpl();
	}

	public override float GetDuration()
	{
		return mDuration;
	}

	private bool TryGetOutDuration(float dist, out float outDuration)
	{
		if (mOutDuration > 0f)
		{
			outDuration = mOutDuration;
			return true;
		}
		if (mOutSpeed > 0f)
		{
			outDuration = dist / mOutSpeed;
			outDuration = Mathf.Max(1E-05f, outDuration);
			return true;
		}
		Debug.LogWarning("[Playable] MyPlayableGrapplingLine mOutDuration and mOutSpeed is wrong configuration.", this);
		outDuration = 0f;
		return false;
	}

	private bool TryGetBackDuration(float dist, out float backDuration)
	{
		if (mBackDuration >= 0f)
		{
			backDuration = mBackDuration;
			return true;
		}
		if (mBackSpeed > 0f)
		{
			backDuration = dist / mBackSpeed;
			backDuration = Mathf.Max(1E-05f, backDuration);
			return true;
		}
		Debug.LogWarning("[Playable] MyPlayableGrapplingLine mBackDuration and mBackSpeed is wrong configuration.", this);
		backDuration = 0f;
		return false;
	}
}
