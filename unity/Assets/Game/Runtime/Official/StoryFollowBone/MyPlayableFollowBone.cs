// Migrated official application/framework source; see bone-following source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryFollowBone
{
[MyPlayableApply(new Type[] { typeof(Transform) })]
public class MyPlayableFollowBone : MyPlayable, IMyPlayableStateMutex, IPlayableOverrideAttachOffset
{
	public enum EScaleByGridSizeType
	{
		None,
		Source,
		Target,
		AttachGO
	}

	[Header("跟随骨骼的基本配置")]
	public AttachPosRotCfg myAttach;

	[Tooltip("是否是静态，如果是静态则不更新位置")]
	public bool IsStatic;

	[Tooltip("是否跟随位置")]
	public bool IsFollowPos = true;

	[Tooltip("是否跟随旋转")]
	public bool IsFollowRot = true;

	[Tooltip("是否跟随缩放")]
	public bool IsFollowScale = true;

	[Tooltip("是否跟随显示/隐藏通用节点。如角色在动画中将spine隐藏，这里是否需要跟随隐藏。")]
	public bool IsFollowShowHideNode;

	[Tooltip("跟随骨骼移动多久，-1表示全程跟随")]
	public float myFollowDuration;

	[Tooltip("淡出期间是否停止跟随")]
	public bool myStopFollowWhileFadeout;

	[Tooltip("总时间多久")]
	public float myDuration;

	[Tooltip("挂接物大小是否随着格子体型大小缩放")]
	public EScaleByGridSizeType ScaleByGridSizeType;

	[SerializeField]
	[Tooltip("是否开启状态互斥")]
	private bool mIsOpenStateMutex;

	[SerializeField]
	[HideInInspector]
	private string mStateMutexUniqueId;

	[SerializeField]
	private ActionBehaviour mMutexAction = ActionBehaviour.Discard;

	[Tooltip("是否开启被跟随对象的可见性")]
	public bool IsEnableFollowTargetActive;

	[Tooltip("Z轴是否等于Y")]
	public bool mIsZEquipY;

	public bool mIsReset;

	private Transform mTransform;

	private Transform mParentTransform;

	private GameObject mTransformFollow;

	private bool mFollowTargetOriginActive;

	private MyPlayableApplyObj mApplyObj;

	private Vector3 mInitialPos;

	private Quaternion mInitialRot;

	private Vector3 mInitialScale;

	private float mGridScale = 1f;

	private GameObject mShowHideNode;

	private TransformSyncType mSyncType;

	public string StateMutexUniqueIdName => "mStateMutexUniqueId";

	public MyPlayableConfig MyPlayableConfig { private get; set; }

	protected override void Awake()
	{
		base.Awake();
		if (IsFollowPos)
		{
			mSyncType |= TransformSyncType.Position;
		}
		if (IsFollowRot)
		{
			mSyncType |= TransformSyncType.Rotation;
		}
		if (IsFollowScale)
		{
			mSyncType |= TransformSyncType.Scale;
		}
	}

	protected override void OnDestroy()
	{
		mSyncType = TransformSyncType.None;
		base.OnDestroy();
	}

	private void Reset()
	{
		GenerateStateMutexID(force: true);
	}

	protected override void PlayImpl()
	{
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		base.PlayImpl();
		Component val = myApplyComponent;
		mTransform = (Transform)(object)((val is Transform) ? val : null);
		if ((Object)(object)mTransform == (Object)null)
		{
			return;
		}
		mParentTransform = mTransform.parent;
		mApplyObj = myApplyComponent.GetComponent<MyPlayableApplyObj>();
		if (myFadeInDuration > 0f || mIsReset)
		{
			mInitialPos = mTransform.localPosition;
			mInitialRot = mTransform.localRotation;
			mInitialScale = mTransform.localScale;
		}
		mGridScale = 1f;
		switch (ScaleByGridSizeType)
		{
		case EScaleByGridSizeType.Source:
			mGridScale = CalcGridScale(mySource);
			break;
		case EScaleByGridSizeType.Target:
			mGridScale = CalcGridScale(myTarget);
			break;
		case EScaleByGridSizeType.AttachGO:
		{
			GameObject attachToGo = AttachPosCfg.GetAttachToGo(this, myAttach.AttachTo);
			mGridScale = CalcGridScale(attachToGo);
			break;
		}
		default:
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			break;
		}
		case EScaleByGridSizeType.None:
			break;
		}
		if (IsFollowShowHideNode)
		{
			GameObject attachToGo2 = AttachPosCfg.GetAttachToGo(this, myAttach.AttachTo);
			mShowHideNode = ((Component)AttachPosRotCfg.GetBone(attachToGo2, "ShowHideNode")).gameObject;
		}
		Transform bone = myAttach.GetBone(this);
		if ((Object)(object)bone != (Object)null && IsEnableFollowTargetActive)
		{
			mTransformFollow = ((Component)bone).gameObject;
			mFollowTargetOriginActive = mTransformFollow.activeSelf;
			if (!mTransformFollow.activeSelf)
			{
				mTransformFollow.SetActive(true);
			}
		}
		if (!IsStatic)
		{
			return;
		}
		Transform bone2 = myAttach.GetBone(this);
		if (!((Object)(object)bone2 != (Object)null))
		{
			return;
		}
		using (ProfilerScope.Sample("MyPlayableFollowBone.SyncTransform"))
		{
			TransformUtils.SyncTransformAccurately(bone2, mTransform, mSyncType, myAttach.myOffset, myAttach.myRotation, myAttach.ScaleV3);
		}
	}

	protected override void StopImpl()
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		if (ScaleByGridSizeType != EScaleByGridSizeType.None && (Object)(object)mTransform != (Object)null)
		{
			mTransform.localScale = Vector3.one;
		}
		if (mIsReset)
		{
			mTransform.localPosition = mInitialPos;
			mTransform.localRotation = mInitialRot;
			mTransform.localScale = mInitialScale;
		}
		if ((Object)(object)mApplyObj != (Object)null)
		{
			mApplyObj.ResumeFadeIn();
			mApplyObj.ResumeFadeOut();
		}
		if ((Object)(object)mTransformFollow != (Object)null && IsEnableFollowTargetActive && mTransformFollow.activeSelf != mFollowTargetOriginActive)
		{
			mTransformFollow.SetActive(mFollowTargetOriginActive);
		}
		mTransformFollow = null;
		mShowHideNode = null;
		base.StopImpl();
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
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		base.PostProcessFrameImpl(deltaTime);
		if ((Object)(object)mTransform == (Object)null)
		{
			return true;
		}
		if (!IsFollow())
		{
			return false;
		}
		if (FollowShowHideNode(out var isActive) && !isActive)
		{
			return false;
		}
		Transform bone = myAttach.GetBone(this);
		if ((Object)(object)bone != (Object)null)
		{
			TransformUtils.SyncTransformAccurately(bone, mTransform, mSyncType, myAttach.myOffset, myAttach.myRotation, myAttach.ScaleV3);
		}
		if (ScaleByGridSizeType != EScaleByGridSizeType.None)
		{
			mTransform.localScale = Vector3.one * mGridScale;
		}
		if (myStatus == PlayableStatus.FadeIn)
		{
			UpdateOnFadeIn();
		}
		if (mIsZEquipY)
		{
			Vector3 position = mTransform.position;
			mTransform.position = new Vector3(position.x, position.y, position.y + myAttach.myOffset.z);
		}
		return false;
	}

	public override float GetDuration()
	{
		return myDuration;
	}

	private static void SyncTransform(Transform source, Transform applicator, TransformSyncType syncType, Vector3 extraPosition, Quaternion extraRotation, Vector3 extraScale)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		Vector3? val = null;
		Quaternion? val2 = null;
		Vector3? val3 = null;
		if (!syncType.HasFlag(TransformSyncType.Position))
		{
			val = applicator.localPosition;
		}
		if (!syncType.HasFlag(TransformSyncType.Rotation))
		{
			val2 = applicator.localRotation;
		}
		if (!syncType.HasFlag(TransformSyncType.Scale))
		{
			val3 = applicator.localScale;
		}
		TransformUtils.SyncTransformAccurately(source, applicator, TransformSyncType.All, extraPosition, extraRotation, extraScale);
		if (val.HasValue)
		{
			applicator.localPosition = val.Value;
		}
		if (val2.HasValue)
		{
			applicator.localRotation = val2.Value;
		}
		if (val3.HasValue)
		{
			applicator.localScale = val3.Value;
		}
	}

	private bool FollowShowHideNode(out bool isActive)
	{
		isActive = false;
		if (IsFollowShowHideNode && (Object)(object)mShowHideNode != (Object)null)
		{
			isActive = mShowHideNode.activeSelf;
			if (((Component)mTransform).gameObject.activeSelf != isActive)
			{
				((Component)mTransform).gameObject.SetActive(isActive);
			}
			return true;
		}
		return false;
	}

	private void UpdateOnFadeIn()
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)mTransform == (Object)null) && myFadeInDuration > 0f)
		{
			float preferWeight = GetPreferWeight();
			mTransform.localScale = Vector3.Lerp(mInitialScale, mTransform.localScale, preferWeight);
		}
	}

	private bool IsFollow()
	{
		if (IsStatic)
		{
			return false;
		}
		if (myFollowDuration >= 0f && Time > myFollowDuration)
		{
			return false;
		}
		if (myStatus == PlayableStatus.FadeOut && myStopFollowWhileFadeout)
		{
			return false;
		}
		return true;
	}

	private float CalcGridScale(GameObject target)
	{
		float result = 1f;
		MySizeCfg mySizeCfg = default;
		if ((Object)(object)target != (Object)null && target.TryGetComponent<MySizeCfg>(out mySizeCfg))
		{
			int gridSize = mySizeCfg.GetGridSize();
			result = ((!MyPlayableConfig.FollowBoneGridSizeToSize.TryGetValue(gridSize, out var value)) ? ((float)gridSize) : value);
		}
		return result;
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

	public ActionBehaviour GetMutexAction()
	{
		return mMutexAction;
	}

	public void GenerateStateMutexID(bool force)
	{
		if (force || string.IsNullOrEmpty(mStateMutexUniqueId))
		{
			mStateMutexUniqueId = Guid.NewGuid().ToString();
		}
	}

	public void SetAttachOffset(Vector3 offset)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		myAttach.myOffset = offset;
	}
}

}
