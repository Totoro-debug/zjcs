// Migrated current official application source; see spine follower runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.ProfessionUI;
using SkeletonRenderer = UnityEngine.Component;
using SkeletonGraphic = UnityEngine.Component;
using Skeleton = Zjcs.Official.ProfessionUI.IProfessionFollowerSkeleton;
using Bone = Zjcs.Official.ProfessionUI.IProfessionFollowerBone;
using Slot = Zjcs.Official.ProfessionUI.IProfessionFollowerSlot;
using Attachment = System.Object;
using System;
using UnityEngine;

namespace Zjcs.Official.BattleSpinePlayables
{
[MyPlayableApply(new Type[] { typeof(IProfessionFollowerRenderer) })]
public class MyPlayableFollowSpinePart : MyPlayable
{
	[Header("跟随spine的基本配置")]
	[Tooltip("是否跟随显隐")]
	public bool FollowEnable;

	[Tooltip("是否跟随显示/隐藏通用节点。如角色在动画中将spine隐藏，这里是否需要跟随隐藏。")]
	public bool IsFollowShowHideNode;

	[Tooltip("骨骼跟随时间，-1全程跟随")]
	public float FollowDuration = -1f;

	[Tooltip("Playable播放时间，-1不自动停止")]
	public float Duration;

	[Tooltip("骨骼跟随节点")]
	public AttachmentFollower AttachmentFollower;

	public BaseSkeletonFollower SkeletonFollower;

	private GameObject mFollowGo;

	private SkeletonRenderer mSkeletonRenderer;

	private SkeletonAttachmentActiveTracker mShowHideNode;

	protected override void Awake()
	{
		base.Awake();
		HideFollower();
	}

	protected override void PlayImpl()
	{
		base.PlayImpl();
		if (!((Object)(object)AttachmentFollower == (Object)null) || !((Object)(object)SkeletonFollower == (Object)null))
		{
			Component val = myApplyComponent;
			mSkeletonRenderer = (Component)((val is IProfessionFollowerRenderer) ? val : null);
			if (!((Object)(object)mSkeletonRenderer == (Object)null))
			{
				mFollowGo = (((Object)(object)AttachmentFollower != (Object)null) ? ((Component)AttachmentFollower).gameObject : ((Component)SkeletonFollower).gameObject);
				mShowHideNode = ((Component)mSkeletonRenderer).GetComponent<SkeletonAttachmentActiveTracker>();
				StartFollow();
			}
		}
	}

	protected override bool ProcessFrameImpl(float deltaTime)
	{
		bool result = base.ProcessFrameImpl(deltaTime);
		if ((Object)(object)mSkeletonRenderer == (Object)null)
		{
			return result;
		}
		FollowShowHide();
		if (FollowDuration >= 0f && Time >= FollowDuration)
		{
			StopFollow();
		}
		return result;
	}

	protected override void StopImpl()
	{
		if ((!((Object)(object)AttachmentFollower == (Object)null) || !((Object)(object)SkeletonFollower == (Object)null)) && !((Object)(object)mSkeletonRenderer == (Object)null))
		{
			StopFollow();
			HideFollower();
			mShowHideNode = null;
			base.StopImpl();
		}
	}

	public override float GetDuration()
	{
		return Duration;
	}

	private void FollowShowHide()
	{
		if (!((Object)(object)mSkeletonRenderer == (Object)null) && FollowEnable)
		{
			bool isActive = GetIsActive();
			if (mFollowGo.activeInHierarchy != isActive)
			{
				mFollowGo.SetActive(isActive);
			}
		}
	}

	private bool GetIsActive()
	{
		if ((Object)(object)mSkeletonRenderer == (Object)null)
		{
			return false;
		}
		bool flag = ((Component)mSkeletonRenderer).gameObject.activeInHierarchy;
		if (flag && IsFollowShowHideNode && (Object)(object)mShowHideNode != (Object)null)
		{
			flag = mShowHideNode.IsActive;
		}
		return flag;
	}

	private void StartFollow()
	{
		if (!((Object)(object)mSkeletonRenderer == (Object)null))
		{
			if ((Object)(object)AttachmentFollower != (Object)null)
			{
				((Component)AttachmentFollower).gameObject.SetActive(true);
				((Behaviour)AttachmentFollower).enabled = true;
				AttachmentFollower.SetSkeletonRenderer(mSkeletonRenderer);
				AttachmentFollower.ForceUpdate();
			}
			else if ((Object)(object)SkeletonFollower != (Object)null)
			{
				((Component)SkeletonFollower).gameObject.SetActive(true);
				((Behaviour)SkeletonFollower).enabled = true;
				SkeletonFollower.InitFollowTarget(((Component)mSkeletonRenderer).gameObject);
				SkeletonFollower.Initialize();
				SkeletonFollower.ForceUpdate();
			}
		}
	}

	private void StopFollow()
	{
		if (!((Object)(object)mSkeletonRenderer == (Object)null))
		{
			if ((Object)(object)AttachmentFollower != (Object)null)
			{
				AttachmentFollower.SetSkeletonRenderer(null);
				((Behaviour)AttachmentFollower).enabled = false;
			}
			else if ((Object)(object)SkeletonFollower != (Object)null)
			{
				SkeletonFollower.Clear();
				((Behaviour)SkeletonFollower).enabled = false;
			}
		}
	}

	private void HideFollower()
	{
		if ((Object)(object)AttachmentFollower != (Object)null)
		{
			((Component)AttachmentFollower).gameObject.SetActive(false);
		}
		if ((Object)(object)SkeletonFollower != (Object)null)
		{
			((Component)SkeletonFollower).gameObject.SetActive(false);
		}
	}
}

}
