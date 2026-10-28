// Migrated original game application code; see map13-native-attachment-follower source receipt.
using Object = UnityEngine.Object;
using SkeletonRenderer = Zjcs.Offline.World.NativeMap13Skeletons.NativeMap13PropMecanim;
using SkeletonGraphic = Zjcs.Offline.World.NativeMap13Skeletons.NativeMap13PropMecanim;
using Skeleton = Zjcs.Offline.World.NativeMap13Skeletons.NativeMap13ApplicationPose;
using Bone = Zjcs.Offline.World.NativeMap13Skeletons.NativeMap13ApplicationBone;
using Slot = Zjcs.Offline.World.NativeMap13Skeletons.NativeMap13ApplicationSlot;
using Attachment = Zjcs.Offline.World.NativeMap13Skeletons.NativeMap13Attachment;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13Attachment
{
[AddComponentMenu("Spine/AttachmentFollower")]
[ExecuteAlways]
public class AttachmentFollower : MonoBehaviour
{
	public SkeletonRenderer SkeletonRenderer;

	public SkeletonGraphic SkeletonGraphic;

	[Header("Bone")]
	public string BoneName;

	[Header("Attachment")]
	public string SlotName;

	public string AttachmentName;

	public GameObject[] Gos;

	public bool FollowZSpacing = true;

	public bool ZFront = true;

	[Header("Common")]
	public bool FollowLocalScale = true;

	public bool FollowRotation = true;

	public bool FollowSkeletonTransformFlipX;

	public bool InitializeOnAwake = true;

	private Bone mBone;

	private Slot mSlot;

	private Attachment mAttachment;

	private Transform mSkeletonTransform;

	private bool mSkeletonTransformIsParent;

	private bool mIsBind;

	private bool mNeedUpdate;

	private Skeleton mSkeleton;

	private bool mEnableFollow;

	private bool mAttachmentValid;

	private readonly List<int> mSlotIndexHistoryList = new List<int>();

	private int mSlotIndex;

	private string mSkeletonName;

	private bool mLogSkeletonInstanceErrorFlag;

	private void Awake()
	{
		if (InitializeOnAwake)
		{
			Initialize();
		}
	}

	private void OnDestroy()
	{
		UnBind();
	}

	private void Update()
	{
		if (mBone == null)
		{
			Initialize();
		}
		else
		{
			if (!mNeedUpdate || !mAttachmentValid)
			{
				return;
			}
			if (mAttachment == mSlot.Attachment)
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
	}

	private void LateUpdate()
	{
		if (!mNeedUpdate || mBone == null || !mBone.Active || (mAttachmentValid && !mEnableFollow))
		{
			return;
		}
		if (!(mSkeletonTransform != null))
		{
			if (!mLogSkeletonInstanceErrorFlag)
			{
				mLogSkeletonInstanceErrorFlag = true;
				string text = ((Object)((Component)this).transform).name;
				Transform parent = ((Component)this).transform.parent;
				while ((parent != null))
				{
					text = ((Object)parent).name + "/" + text;
					parent = parent.parent;
				}
				UnityEngine.Debug.LogError("Official playable diagnostic");
			}
			return;
		}
		Transform transform = ((Component)this).transform;
		Bone val = mBone;
		if (mSkeletonTransformIsParent)
		{
			Vector3 boneLocalPosition = GetBoneLocalPosition();
			if (FollowZSpacing && mAttachmentValid)
			{
				boneLocalPosition.z = GetPosZ();
			}
			transform.localPosition = boneLocalPosition;
			if (FollowRotation)
			{
				float num = Mathf.Atan2(val.C, val.A) * 0.5f;
				transform.localRotation = new Quaternion
				{
					z = Mathf.Sin(num),
					w = Mathf.Cos(num)
				};
			}
		}
		else
		{
			Vector3 val2 = mSkeletonTransform.TransformPoint(GetBoneLocalPosition());
			Vector3 val3 = new Vector3(val2.x, val2.y, val2.z);
			if (FollowZSpacing && mAttachmentValid)
			{
				val3.z = mSkeletonTransform.position.z + GetPosZ();
			}
			Vector3 lossyScale = mSkeletonTransform.lossyScale;
			if (FollowRotation)
			{
				float num2 = val.WorldRotationX;
				if (lossyScale.x * lossyScale.y < 0f)
				{
					num2 = 0f - num2;
				}
				Quaternion rotation = mSkeletonTransform.rotation;
				Vector3 eulerAngles = rotation.eulerAngles;
				transform.SetPositionAndRotation(val3, Quaternion.Euler(eulerAngles.x, eulerAngles.y, eulerAngles.z + num2));
			}
			else
			{
				transform.position = val3;
			}
		}
		if (FollowSkeletonTransformFlipX || FollowLocalScale)
		{
			Vector3 localScale = transform.localScale;
			if (FollowLocalScale)
			{
				localScale.x = val.ScaleX;
				localScale.y = val.ScaleY;
			}
			if (FollowSkeletonTransformFlipX)
			{
				localScale.x *= ((mSkeletonTransform.lossyScale.x > 0f) ? 1 : (-1));
			}
			transform.localScale = localScale;
		}
	}

	public void InitFollowTarget(GameObject target)
	{
		if ((Object)(object)SkeletonRenderer == (Object)null)
		{
			SkeletonRenderer = target.GetComponent<SkeletonRenderer>();
		}
		if ((Object)(object)SkeletonGraphic == (Object)null)
		{
			SkeletonGraphic = target.GetComponent<SkeletonGraphic>();
		}
	}

	public void ForceUpdate()
	{
		Update();
		LateUpdate();
	}

	public void Initialize()
	{
		mSlot = null;
		mAttachment = null;
		mAttachmentValid = false;
		InitSettings();
		if (!IsSkeletonComponentValid())
		{
			return;
		}
		UpdateSkeleton();
		Skeleton val = mSkeleton;
		if (!string.IsNullOrEmpty(BoneName))
		{
			mBone = val.FindBone(BoneName);
			if (mBone == null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
			}
		}
		if (!string.IsNullOrEmpty(SlotName) && !string.IsNullOrEmpty(AttachmentName))
		{
			mSlot = val.FindSlot(SlotName);
			if (mSlot == null)
			{
				UnityEngine.Debug.LogError("Official playable diagnostic");
				return;
			}
			bool flag = !string.IsNullOrEmpty(AttachmentName);
			mAttachment = (flag ? val.GetAttachment(SlotName, AttachmentName) : mSlot.Attachment);
			mAttachmentValid = mSlot != null && mAttachment != null;
			if (mBone == null && mSlot != null)
			{
				mBone = mSlot.Bone;
			}
			if (flag)
			{
				DisableFollow();
			}
		}
		if (mBone != null)
		{
			UpdateSkeletonTransform();
			mSkeletonTransformIsParent = mSkeletonTransform == ((Component)this).transform.parent;
			Bind();
		}
	}

	private void Bind()
	{
		if (!mIsBind)
		{
			if ((Object)(object)SkeletonRenderer != (Object)null)
			{
				SkeletonRenderer.OnRebuild += HandleRebuildRenderer;
				mIsBind = true;
			}
			else if ((Object)(object)SkeletonGraphic != (Object)null)
			{
				SkeletonGraphic.OnRebuild += HandleRebuildGraphic;
				mIsBind = true;
			}
		}
	}

	private void UnBind()
	{
		if ((Object)(object)SkeletonRenderer != (Object)null)
		{
			SkeletonRenderer.OnRebuild -= HandleRebuildRenderer;
		}
		else if ((Object)(object)SkeletonGraphic != (Object)null)
		{
			SkeletonGraphic.OnRebuild -= HandleRebuildGraphic;
		}
		mIsBind = false;
	}

	private void HandleRebuildGraphic(SkeletonGraphic _)
	{
		Initialize();
	}

	private void HandleRebuildRenderer(SkeletonRenderer skeletonRenderer)
	{
		Initialize();
	}

	public void SetSkeletonRenderer(SkeletonRenderer skeletonRenderer)
	{
		if (mIsBind)
		{
			UnBind();
		}
		SkeletonRenderer = skeletonRenderer;
		Initialize();
	}

	public void SetSkeletonGraphic(SkeletonGraphic skeletonGraphic)
	{
		if (mIsBind)
		{
			UnBind();
		}
		SkeletonGraphic = skeletonGraphic;
		Initialize();
	}

	private void InitSettings()
	{
		if ((Object)(object)SkeletonRenderer != (Object)null)
		{
			mNeedUpdate = (Object)(object)SkeletonRenderer != (Object)null || (int)SkeletonRenderer.updateWhenInvisible == 3 || SkeletonRenderer.UpdateMode != SkeletonRenderer.updateWhenInvisible;
		}
		else if ((Object)(object)SkeletonGraphic != (Object)null)
		{
			mNeedUpdate = true;
		}
		else
		{
			mNeedUpdate = false;
		}
	}

	private Vector3 GetBoneLocalPosition()
	{
		Vector3 result = new Vector3(mBone.WorldX, mBone.WorldY, 0f);
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

	private bool IsSkeletonComponentValid()
	{
		if ((Object)(object)SkeletonRenderer != (Object)null)
		{
			SkeletonRenderer.Initialize(false, false);
			return SkeletonRenderer.valid;
		}
		if ((Object)(object)SkeletonGraphic != (Object)null)
		{
			SkeletonGraphic.Initialize(false);
			return SkeletonGraphic.IsValid;
		}
		return false;
	}

	private void UpdateSkeleton()
	{
		mSkeleton = (((Object)(object)SkeletonRenderer != (Object)null) ? SkeletonRenderer.Skeleton : (((Object)(object)SkeletonGraphic != (Object)null) ? SkeletonGraphic.Skeleton : null));
	}

	private void UpdateSkeletonTransform()
	{
		mSkeletonTransform = (((Object)(object)SkeletonRenderer != (Object)null) ? ((Component)SkeletonRenderer).transform : (((Object)(object)SkeletonGraphic != (Object)null) ? ((Component)SkeletonGraphic).transform : null));
		if ((mSkeletonTransform != null))
		{
			mSkeletonName = ((Object)((Component)mSkeletonTransform).gameObject).name;
		}
	}

	private float GetPosZ()
	{
		float num = 0f;
		if ((Object)(object)SkeletonRenderer != (Object)null)
		{
			num = SkeletonRenderer.zSpacing;
		}
		else if ((Object)(object)SkeletonGraphic != (Object)null)
		{
			num = SkeletonGraphic.zSpacing;
		}
		if (num == 0f)
		{
			return 0f;
		}
		Slot[] items = mSkeleton.DrawOrder.Items;
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
		float num3 = (ZFront ? (num / 2f) : ((0f - num) / 2f));
		return num * (float)mSlotIndex + num3;
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
		int i = 0;
		for (int num = Gos.Length; i < num; i++)
		{
			Gos[i].SetActive(active);
		}
		mEnableFollow = active;
	}
}

}
