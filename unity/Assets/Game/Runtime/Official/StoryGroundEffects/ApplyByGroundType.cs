// Migrated original ApplyByGroundType. Only service boundaries and diagnostics are adapted.
// See ground-effects-runtime-source-manifest.json.
using Object = UnityEngine.Object;
using System.Collections.Generic;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using System;
using UnityEngine;

namespace Zjcs.Official.StoryGroundEffects
{
public class ApplyByGroundType : ApplyOnInit
{
	[Serializable]
	public struct AssetGuidAndSlotProxy
	{
		[field: SerializeField]
		public AssetGuidProxy<GameObject> AssetGuidProxy { get; private set; }

		[field: SerializeField]
		public Transform Slot { get; private set; }

		public bool IsVaild
		{
			get
			{
				if (AssetGuidProxy != null)
				{
					return AssetGuidProxy.IsValid;
				}
				return false;
			}
		}
	}

	[SerializeField]
	private EAttachTo mAttachType;

	[SerializeField]
	private MonoDictionary<GroundType, AssetGuidAndSlotProxy> mGroundTypeAssetToApplyDict;

	[SerializeField]
	private bool mIsAutoUpdateApply;

	[SerializeField]
	private float mUpdateInterval = 0.05f;

	private IPlayableGroundSurface mCurrentGridGroundComponent;

	public IPlayableGroundEffectHost Host { get; set; }

	private GameObject mAttachGO;

	private float mTime;

	private GameObject mApplyGO;

	private GroundType? mLastGroundType;

	private List<IDisposable> mDisposableBag;

	protected override void Awake()
	{
		base.Awake();
		mDisposableBag = new List<IDisposable>();
	}

	protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		UpdateGridGroundComponent();
		if (mCurrentGridGroundComponent != null)
		{
			UpdateGetAttachGO(playable, out mAttachGO);
			_ = (Object)(object)mAttachGO == (Object)null;
		}
	}

	protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		if (mCurrentGridGroundComponent != null)
		{
			UpdateGetAttachGO(playable, out mAttachGO);
			if (!((Object)(object)mAttachGO == (Object)null))
			{
				mTime = 0f;
				playable.OnStop += new Action<MyPlayable>(Playable_OnStop);
				SetMyApplyComponent(playable, applyAttr);
			}
		}
	}

	private void Update()
	{
		if (mIsAutoUpdateApply)
		{
			mTime += Time.deltaTime;
			if (mTime > mUpdateInterval)
			{
				SetMyApplyComponent(myPlayable, mAttr);
				mTime %= mUpdateInterval;
			}
		}
	}

	protected override void OnDestroy()
	{
		foreach (var handle in mDisposableBag) handle.Dispose();
		mDisposableBag.Clear();
		base.OnDestroy();
	}

	private void Playable_OnStop(MyPlayable playable)
	{
		playable.OnStop -= new Action<MyPlayable>(Playable_OnStop);
		ResetApplyGO();
		mLastGroundType = null;
	}

	private void UpdateGridGroundComponent()
	{
		if (Host == null) throw new InvalidOperationException("ApplyByGroundType requires an explicit ground/effect host.");
		mCurrentGridGroundComponent = Host.ResolveGround();
		if (mCurrentGridGroundComponent == null) Debug.LogError("[MyPlayable] ApplyByGroundType has no fight/map ground.", this);
	}

	private void UpdateGetAttachGO(MyPlayable playable, out GameObject result)
	{
		result = AttachPosCfg.GetAttachToGo(playable, mAttachType);
		if (result == null) Debug.LogError("[MyPlayable] ApplyByGroundType has no attachment object: " + mAttachType, this);
	}

	private GroundType GetGroundType()
	{
		return mCurrentGridGroundComponent.GetGroundType(mAttachGO.transform.localPosition);
	}

	private AssetGuidAndSlotProxy GetPrefabAsset(GroundType groundType)
	{
		if (mGroundTypeAssetToApplyDict.TryGetValue(groundType, out var value)) return value;
		if (!mGroundTypeAssetToApplyDict.TryGetValue(GroundType.IgnoreKey, out value))
			Debug.LogWarning("[MyPlayable] No ground effect or IgnoreKey configuration: " + groundType, this);
		return value;
	}

	private void SetMyApplyComponent(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		GroundType groundType = GetGroundType();
		if (mLastGroundType.HasValue && mLastGroundType.Value == groundType)
		{
			return;
		}
		mLastGroundType = groundType;
		ResetApplyGO();
		AssetGuidAndSlotProxy prefabAsset = GetPrefabAsset(groundType);
		if (!prefabAsset.IsVaild)
		{
			return;
		}
		var handle = Host.LoadAssetByGuid(prefabAsset.AssetGuidProxy.AssetGuid);
		mDisposableBag.Add(handle);
		GameObject result = handle.Result;
		mApplyGO = Host.Instantiate(result);
		mApplyGO.transform.SetParent(GetPrefabAsset(groundType).Slot);
		if ((Object)(object)mApplyGO == (Object)null)
		{
			Debug.LogError("[MyPlayable] Ground effect instantiation returned null: " + groundType, this);
		}
		else
		{
			mApplyGO.SetActive(true);
			MyPlayableApplyUtils.SetPlayableApplyComponent(playable, applyAttr, mApplyGO, applyTargetOfNullAttr: true);
		}
	}

	private void ResetApplyGO()
	{
		if ((Object)(object)mApplyGO != (Object)null)
		{
			mApplyGO.SetActive(false);
			mApplyGO = null;
		}
	}
}

}
