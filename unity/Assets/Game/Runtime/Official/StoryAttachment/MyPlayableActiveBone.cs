// Migrated official application source; see map14-story-attachment-runtime source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.StoryAttachment
{
[MyPlayableApply(new Type[] { typeof(AttachAgent) })]
public class MyPlayableActiveBone : MyPlayable
{
	[Header("隐藏骨骼的基本配置")]
	[Tooltip("要隐藏的骨骼列表，若需要此功能，则applyComponent对象上需要挂载AttachAgent脚本。")]
	[SerializeField]
	private List<string> mBoneNames;

	[Tooltip("是否隐藏根节点")]
	[SerializeField]
	private bool mRootBone;

	[Tooltip("True激活，False隐藏")]
	[SerializeField]
	private bool mActive;

	[Tooltip("是否在停止时还原")]
	[SerializeField]
	private bool mRevertOnStop = true;

	[Tooltip("持续时间，0立马结束")]
	[SerializeField]
	private float mDuration;

	private bool mUpdatedBone;

	private bool mUpdatedRoot;

	private AttachAgent mAttachAgent;

	protected override void PlayImpl()
	{
		base.PlayImpl();
		mUpdatedBone = false;
		mUpdatedRoot = false;
		mAttachAgent = myApplyComponent as AttachAgent;
		if ((Object)(object)mAttachAgent != (Object)null)
		{
			foreach (string mBoneName in mBoneNames)
			{
				mAttachAgent.SetBoneActiveStatus(mBoneName, mActive);
			}
			mUpdatedBone = true;
		}
		if (mRootBone && (Object)(object)myApplyComponent != (Object)null)
		{
			myApplyComponent.gameObject.SetActive(mActive);
			mUpdatedRoot = true;
		}
	}

	public override float GetDuration()
	{
		return mDuration;
	}

	protected override void StopImpl()
	{
		if (mRevertOnStop)
		{
			if (mUpdatedBone && (Object)(object)mAttachAgent != (Object)null)
			{
				foreach (string mBoneName in mBoneNames)
				{
					mAttachAgent.SetBoneActiveStatus(mBoneName, !mActive);
				}
			}
			if (mUpdatedRoot && (Object)(object)myApplyComponent != (Object)null)
			{
				myApplyComponent.gameObject.SetActive(!mActive);
			}
		}
		mUpdatedBone = false;
		mUpdatedRoot = false;
		mAttachAgent = null;
		base.StopImpl();
	}
}

}
