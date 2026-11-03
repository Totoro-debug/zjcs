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
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleSpinePlayables
{
public class SkeletonAttachmentActiveTracker : MonoBehaviour
{
	public SkeletonRenderer SkeletonRenderer;

	public string SlotName;

	public string AttachmentName;

	public List<GameObject> TargetList;

	private Slot mSlot;

	private Attachment mAttachment;
	private bool mValid;
	private bool mIsActive;

	public bool IsActive => mIsActive;

	private void Awake()
	{
		Initialize();
	}
	public void Initialize()
	{
		if (string.IsNullOrEmpty(SlotName) || string.IsNullOrEmpty(AttachmentName))
		{
			return;
		}
		mValid = false;
		Skeleton skeleton = ((IProfessionFollowerRenderer)SkeletonRenderer).Skeleton;
		if (skeleton == null)
		{
			return;
		}
		mSlot = skeleton.FindSlot(SlotName);
		if (mSlot != null)
		{
			mAttachment = skeleton.GetAttachment(mSlot.Index, AttachmentName);
			if (mAttachment != null)
			{
				mValid = true;
				SetActive(mSlot.Attachment == mAttachment);
			}
		}
	}

	private void Update()
	{
		if (!mValid)
		{
			Initialize();
			return;
		}
		bool flag = ((IProfessionFollowerSlotAlpha)mSlot).Alpha != 0f && mSlot.Attachment == mAttachment;
		if (mIsActive != flag)
		{
			SetActive(flag);
		}
	}

	private void SetActive(bool active)
	{
		if (TargetList == null || mIsActive == active)
		{
			return;
		}
		foreach (GameObject target in TargetList)
		{
			target.SetActive(active);
		}
		mIsActive = active;
	}
}

}
