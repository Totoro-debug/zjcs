namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public abstract class BaseSkeletonFollower : MonoBehaviour, ISkeletonFollower, IProfessionAppearancePartFollower
{
	[Header("Bone")]
	public string BoneName;

    string IProfessionAppearancePartFollower.SlotName => SlotName;
    bool IProfessionAppearancePartFollower.ZFront => ZFront;

	[Header("Attachment")]
	public string SlotName;

	public string AttachmentName;

	public GameObject[] Gos;

	public bool FollowZSpacing = true;

	public bool ZFront = true;

	[Header("Common")]
	public bool FollowLocalScale = true;

	public bool FollowBoneWorldFlip;

	public bool FollowRotation = true;

	public bool FollowSkeletonTransformFlipX;

	public bool InitializeOnAwake = true;

	private IProfessionFollowerBone mBone;

	private IProfessionFollowerAnimation mSkeletonAnimation;

	private IProfessionFollowerSkeleton mSkeleton;

	private IProfessionFollowerSlot mSlot;

	private object mAttachment;

	private Transform mSkeletonTransform;

	private bool mUseCustomZ;

	private bool mSkeletonTransformIsParent;

	private bool mSlotValid;

	private bool mVisible;

	private bool mFollowValid;

	private bool mEnableFollow;

	private readonly List<int> mSlotIndexHistoryList = new List<int>();

	private int mSlotIndex;

	private Action<IProfessionFollowerAnimation> mOnAnimationRebuild;

	public IProfessionFollowerBone Bone
	{
		get
		{
			IProfessionFollowerBone bone = mBone;
			if (bone == null)
			{
				IProfessionFollowerSlot val = mSlot;
				if (val == null)
				{
					return null;
				}
				bone = val.Bone;
			}
			return bone;
		}
	}

	public IProfessionFollowerAnimation SkeletonAnimation => mSkeletonAnimation;

	private void Awake()
	{
		if (InitializeOnAwake)
		{
			Initialize();
		}
	}

	private void OnDestroy()
	{
		DeRegister();
	}

	private void OnEnable()
	{
		Register();
		if (mSkeletonAnimation != null && mSkeletonAnimation.Skeleton != mSkeleton)
		{
			Initialize();
		}
	}

	private void OnDisable()
	{
		DeRegister();
	}

	protected virtual void Register()
	{

		if (mOnAnimationRebuild == null)
		{
			mOnAnimationRebuild = OnAnimationRebuild;
		}
		if (mSkeletonAnimation != null)
		{
			mSkeletonAnimation.OnAnimationRebuild -= mOnAnimationRebuild;
			mSkeletonAnimation.OnAnimationRebuild += mOnAnimationRebuild;
		}
	}

	protected virtual void DeRegister()
	{
		if (mSkeletonAnimation != null)
		{
			mSkeletonAnimation.OnAnimationRebuild -= mOnAnimationRebuild;
		}
	}

	public void ForceUpdate()
	{
		Update();
		LateUpdate();
	}

	public abstract void InitFollowTarget(GameObject target);

	protected abstract bool UpdateVisible();

	public void Initialize()
	{
		mSlot = null;
		mAttachment = null;
		mSlotValid = false;
		mFollowValid = false;
		if (!InitComponent(ref mSkeletonAnimation, ref mSkeletonTransform, ref mUseCustomZ))
		{
			return;
		}
		DisableFollow();
		mSkeleton = mSkeletonAnimation.Skeleton;
		mSkeletonTransformIsParent = mSkeletonTransform == ((Component)this).transform.parent;
		IProfessionFollowerSkeleton val = mSkeleton;
		if (!string.IsNullOrEmpty(BoneName))
		{
			mBone = val.FindBone(BoneName);
		}
		if (!string.IsNullOrEmpty(SlotName))
		{
			mSlot = val.FindSlot(SlotName);
			mSlotValid = mSlot != null;
			if (mSlotValid)
			{
				bool flag = !string.IsNullOrEmpty(AttachmentName);
				if (flag)
				{
					mAttachment = val.GetAttachment(mSlot.Index, AttachmentName);
				}
				mFollowValid = (Gos != null) & flag;
				if (!mFollowValid || (mAttachment == mSlot.Attachment && mAttachment != null))
				{
					EnableFollow();
				}
			}
		}
		else
		{
			EnableFollow();
		}
	}

	public virtual void Clear()
	{
		DeRegister();
		mSkeletonAnimation = null;
		mSkeletonTransform = null;
		mSkeleton = null;
		mBone = null;
		mSlot = null;
		mAttachment = null;
		mSkeleton = null;
		mSlotIndex = 0;
		mSlotIndexHistoryList.Clear();
	}

	private void OnAnimationRebuild(IProfessionFollowerAnimation _)
	{
		Initialize();
	}

	protected virtual void Update()
	{
		if (mBone == null && mSlot == null)
		{
			Initialize();
			return;
		}
		mVisible = UpdateVisible();
		if (!mFollowValid || !mVisible)
		{
			return;
		}
		if (mAttachment == mSlot.Attachment && mAttachment != null)
		{
			if (!mEnableFollow)
			{
				EnableFollow();
			}
		}
		else if (mEnableFollow)
		{
			DisableFollow();
		}
	}

	protected virtual void LateUpdate()
	{

		if (!mVisible || !mEnableFollow)
		{
			return;
		}
		IProfessionFollowerBone bone = Bone;
		if (bone == null || !bone.Active)
		{
			return;
		}
		Transform transform = ((Component)this).transform;
		if (mSkeletonTransformIsParent)
		{
			Vector3 boneLocalPosition = GetBoneLocalPosition();
			if (FollowZSpacing && mSlotValid)
			{
				boneLocalPosition.z = GetPosZ();
			}
			transform.localPosition = boneLocalPosition;
			if (FollowRotation)
			{
				float num = Mathf.Atan2(bone.C, bone.A) * 0.5f;
				transform.localRotation = new Quaternion
				{
					z = Mathf.Sin(num),
					w = Mathf.Cos(num)
				};
			}
		}
		else
		{
			Vector3 val = mSkeletonTransform.TransformPoint(GetBoneLocalPosition());
			Vector3 val2 = new Vector3(val.x, val.y, val.z);
			Vector3 lossyScale = mSkeletonTransform.lossyScale;
			if (FollowZSpacing && mSlotValid)
			{
				val2.z = mSkeletonTransform.position.z + GetPosZ() * Mathf.Abs(lossyScale.z);
			}
			if (FollowRotation)
			{
				float num2 = bone.WorldRotationX;
				if (lossyScale.x * lossyScale.y < 0f)
				{
					num2 = 0f - num2;
				}
				Quaternion rotation = mSkeletonTransform.rotation;
				Vector3 eulerAngles = rotation.eulerAngles;
				transform.SetPositionAndRotation(val2, Quaternion.Euler(eulerAngles.x, eulerAngles.y, eulerAngles.z + num2));
			}
			else
			{
				transform.position = val2;
			}
		}
		if (!FollowSkeletonTransformFlipX && !FollowLocalScale)
		{
			return;
		}
		Vector3 one = Vector3.one;
		if (FollowBoneWorldFlip)
		{
			one.x = bone.ScaleX;
			float a = bone.A;
			float b = bone.B;
			float c = bone.C;
			float d = bone.D;
			if (a * d - b * c < 0f)
			{
				one.y = 0f - Math.Abs(bone.ScaleY);
			}
			else
			{
				one.y = Math.Abs(bone.ScaleY);
			}
		}
		else
		{
			one.x = bone.ScaleX;
			one.y = bone.ScaleY;
		}
		if (FollowSkeletonTransformFlipX && mSkeletonTransform.lossyScale.x < 0f)
		{
			one.x *= -1f;
		}
		transform.localScale = one;
	}

	protected abstract bool InitComponent(ref IProfessionFollowerAnimation skeletonAnimation, ref Transform skeletonTransform, ref bool useCustomZ);

	protected virtual float GetZSpacing()
	{
		return 0f;
	}

	private Vector3 GetBoneLocalPosition()
	{

		IProfessionFollowerBone bone = Bone;
		Vector3 result = new Vector3(bone.WorldX, bone.WorldY, 0f);
		Transform transform = ((Component)this).transform;
		RectTransform val = (RectTransform)(object)((transform is RectTransform) ? transform : null);
		if (val != null)
		{
			Rect rect = val.rect;
			result.x *= rect.width;
			result.y *= rect.height;
		}
		return result;
	}

	private float GetPosZ()
	{
		float zSpacing = GetZSpacing();
		if (zSpacing == 0f)
		{
			return 0f;
		}
		float num = (ZFront ? (zSpacing / 2f) : ((0f - zSpacing) / 2f));
		if (mUseCustomZ)
		{
			return (float)mSlot.zOrder * zSpacing + num;
		}
		IProfessionFollowerSlot[] items = mSkeleton.DrawOrder;
		if (items[mSlotIndex] != mSlot)
		{
			bool flag = false;
			foreach (int mSlotIndexHistory in mSlotIndexHistoryList)
			{
				if (items[mSlotIndexHistory] == mSlot)
				{
					mSlotIndex = mSlotIndexHistory;
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				int i = 0;
				for (int num2 = items.Length; i < num2; i++)
				{
					if (items[i] == mSlot)
					{
						mSlotIndexHistoryList.Add(i);
						mSlotIndex = i;
						break;
					}
				}
			}
		}
		return zSpacing * (float)mSlotIndex + num;
	}

	private void EnableFollow()
	{
		SetFollowActive(active: true);
	}

	private void DisableFollow()
	{
		SetFollowActive(active: false);
	}

	private void SetFollowActive(bool active)
	{
		if (Gos != null)
		{
			int i = 0;
			for (int num = Gos.Length; i < num; i++)
			{
				Gos[i].SetActive(active);
			}
		}
		mEnableFollow = active;
	}
}

}
