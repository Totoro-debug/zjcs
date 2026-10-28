// Migrated official application source; see attachment transform source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryAttachment
{
[MyPlayableApply(new Type[] { typeof(Transform) })]
public class MyPlayableAttachToBone : MyPlayable, IMyPlayableStateMutex
{
	[Header("挂载到骨骼的基本配置")]
	public AttachPosRotCfg myAttach;

	[Tooltip("是否跟随显示/隐藏通用节点。如角色在动画中将spine隐藏，这里是否需要跟随隐藏。")]
	public bool IsFollowShowHideNode;

	[Tooltip("总时间多久")]
	public float myDuration;

	[SerializeField]
	[Tooltip("是否开启状态互斥")]
	private bool mIsOpenStateMutex;

	[SerializeField]
	[HideInInspector]
	private string mStateMutexUniqueId;

	[SerializeField]
	private ActionBehaviour mMutexAction = ActionBehaviour.Discard;

	private Transform mApplyParentTransform;

	private Transform mTransform;

	private MyPlayableApplyObj mApplyObj;

	private GameObject mShowHideNode;

	public string StateMutexUniqueIdName => "mStateMutexUniqueId";

	protected override void PlayImpl()
	{
		base.PlayImpl();
		Component val = myApplyComponent;
		mTransform = (Transform)(object)((val is Transform) ? val : null);
		if (!((Object)(object)mTransform == (Object)null))
		{
			mApplyObj = myApplyComponent.GetComponent<MyPlayableApplyObj>();
			mApplyParentTransform = mTransform.parent;
			if (IsFollowShowHideNode)
			{
				GameObject attachToGo = AttachPosCfg.GetAttachToGo(this, myAttach.AttachTo);
				mShowHideNode = ((Component)AttachPosRotCfg.GetBone(attachToGo, "ShowHideNode")).gameObject;
			}
			myAttach.Attach(this, mTransform);
		}
	}

	public override float GetDuration()
	{
		return myDuration;
	}

	protected override bool ProcessFrameImpl(float deltaTime)
	{
		if ((Object)(object)mApplyObj != (Object)null)
		{
			if (myStatus == PlayableStatus.FadeIn)
			{
				float fadeInTime = GetFadeInTime();
				mApplyObj.FadeIn(fadeInTime);
			}
			else if (myStatus == PlayableStatus.FadeOut)
			{
				float fadeOutTime = GetFadeOutTime();
				mApplyObj.FadeOut(fadeOutTime);
			}
		}
		return base.ProcessFrameImpl(deltaTime);
	}

	protected override bool PostProcessFrameImpl(float deltaTime)
	{
		base.PostProcessFrameImpl(deltaTime);
		FollowShowHideNode();
		return false;
	}

	private void FollowShowHideNode()
	{
		if (IsFollowShowHideNode && (Object)(object)mShowHideNode != (Object)null)
		{
			bool activeSelf = mShowHideNode.activeSelf;
			if (((Component)mTransform).gameObject.activeSelf != activeSelf)
			{
				((Component)mTransform).gameObject.SetActive(activeSelf);
			}
		}
	}

	protected override void StopImpl()
	{
		if ((Object)(object)mApplyObj != (Object)null)
		{
			mApplyObj.ResumeFadeIn();
			mApplyObj.ResumeFadeOut();
		}
		if ((Object)(object)mTransform != (Object)null)
		{
			mTransform.SetParent(mApplyParentTransform);
		}
		mShowHideNode = null;
		base.StopImpl();
	}

	public bool IsOpenStateMutex()
	{
		return mIsOpenStateMutex;
	}

	public string GetStateMutexID()
	{
		return mStateMutexUniqueId;
	}

	public GameObject GetStateMutexGo()
	{
		return AttachPosCfg.GetAttachToGo(this, myAttach.AttachTo);
	}

	public void GenerateStateMutexID(bool force)
	{
		if (force || string.IsNullOrEmpty(mStateMutexUniqueId))
		{
			mStateMutexUniqueId = Guid.NewGuid().ToString();
		}
	}

	public ActionBehaviour GetMutexAction()
	{
		return mMutexAction;
	}
}

}
