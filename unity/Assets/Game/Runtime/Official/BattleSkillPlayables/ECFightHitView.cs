// Current official EC view; explicit entity/DI boundaries are recorded in its source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.BattleSkillPlayables
{
[System.Serializable]
public class ECFightHitView : ECViewBase, IFightGetUsePlayable
{
	[Tooltip("Hit自身位置格子的playable，source和target为hit本身")]
	[SerializeField]
	private GameObject mPlayable;

	[Tooltip("Hit设置范围内所有格子的playable")]
	[SerializeField]
	private GameObject mScopePlayable;

	[Tooltip("格子上的Playable是否跟随配置的hit延迟后再播放")]
	[SerializeField]
	private bool mScopePlayableFollowDelay = true;

	[Tooltip("格子位置的Z值是否跟随Hit的Z值")]
	[SerializeField]
	private bool mScopeZFollowHitZ;

	[Tooltip("从Source指向Hit自身位置格子的playable")]
	[SerializeField]
	private GameObject mSourceToMePlayable;

	[Tooltip("设置playable ParamSource2TargetDirection 给SourceToMePlayable")]
	[SerializeField]
	private bool mShouldSetPlayableDirectinParamToSouceToMePlayable;

	[Tooltip("SourceToMePlayable 的起点：施法者/偏移；或与技能主 Playable 的 target 一致")]
	[SerializeField]
	private EFightHitSourceToMeAnchor mSourceToMeAnchor;

	[Tooltip("Playable表现上是否使用偏移")]
	[SerializeField]
	private bool mIsApplyOffsetToPlayable;

	protected FightHitBaseComponent mHitComponent;

	private IFightHitViewBinding mDelayInfoProvider;

	protected MyEntity mHitEntity;

	private MyGridTransform mGridTransform;

	private readonly List<GameObject> mTmpObjs = new List<GameObject>();

	private Dictionary<string, object> mPlayableParams;

	private long mDelayCoroutine;

	private long mScopeCoroutine;

	private IDisposable mOnHitPosChangeDisposable;

	public IMyPlayableManager MyPlayableManager { get; set; }
	public IFightHitViewServices ViewServices { get; set; }

	protected override void Load()
	{
		base.Load();
		mHitComponent = GetComponent<FightHitBaseComponent>();
		if (mHitComponent == null)
		{
			return;
		}
		mHitEntity = ((Component)mOwner.transform).GetComponentInParent<MyEntity>(true);
		if (!((Object)(object)mHitEntity == (Object)null))
		{
			mGridTransform = ((Component)mHitEntity).GetComponent<MyGridTransform>();
			if (!((Object)(object)mGridTransform == (Object)null))
			{
				mDelayInfoProvider = ((Component)mHitEntity).GetComponent<IFightHitViewBinding>();
				DoBeforeDelay();
				mDelayInfoProvider?.GetCurrentDelayInfoProvider();
				mDelayCoroutine = StartCoroutine(DelayPlay());
				mOnHitPosChangeDisposable = mHitComponent.OnHitPosChange.Subscribe(OnHitPosChange);
				OnHitLoaded();
			}
		}
	}

	protected virtual void OnHitLoaded()
	{
	}

	protected override void Unload()
	{
		StopCoroutineIfRunning(ref mDelayCoroutine);
		StopCoroutineIfRunning(ref mScopeCoroutine);
		mOnHitPosChangeDisposable?.Dispose();
		mOnHitPosChangeDisposable = null;
		for (int i = 0; i < mTmpObjs.Count; i++)
		{
			if ((Object)(object)mTmpObjs[i] != (Object)null)
			{
				Object.Destroy((Object)(object)mTmpObjs[i]);
			}
		}
		mTmpObjs.Clear();
		mPlayableParams = null;
		mGridTransform = null;
		mHitEntity = null;
		mHitComponent = null;
		mDelayInfoProvider = null;
		base.Unload();
	}

	private void DoAfterDelay()
	{
		if ((Object)(object)mPlayable != (Object)null)
		{
			MyPlayableManager.Play(mPlayable, ((Component)mHitEntity).gameObject, ((Component)mHitEntity).gameObject);
		}
		(ViewServices as IFightHitViewArrivalObserver)?.HitViewArrived(
			mHitComponent.SkillId, mHitComponent.Entity.Id, mHitComponent.GridTransform.Pos, -1);
		if ((Object)(object)mScopePlayable != (Object)null)
		{
			mScopeCoroutine = StartCoroutine(PlayScopePlayable());
		}
	}

	private void DoBeforeDelay()
	{
		if (!((Object)(object)mSourceToMePlayable == (Object)null))
		{
			GameObject val = CreateSourceGo();
			if (!((Object)(object)val == (Object)null))
			{
				MyPlayableManager.Play(mSourceToMePlayable, val, ((Component)mHitEntity).gameObject, 1f, null, mShouldSetPlayableDirectinParamToSouceToMePlayable ? GetPlayableParam() : null);
			}
		}
	}

	private IEnumerator DelayPlay()
	{
		if (TryGetSkillContext(out var _, out var source, out var _))
		{
			Pos2d pos = mGridTransform.Pos;
			IFightSkillViewServices gridGround = ViewServices;
			bool flipX = ((Component)source).GetComponent<MyGridTransform>().FlipX;
			float num = FightHitViewFormula.CalcSkillHitDelay(GetDelayInfoProvider(), GetStartPos(), pos, gridGround, flipX);
			yield return (object)new WaitForSeconds(num);
			mDelayCoroutine = 0L;
			if (ViewServices is IFightHitViewArrivalDeferrer deferrer
				&& deferrer.DeferHitView(mHitComponent.SkillId, mHitComponent.Entity.Id, DoAfterDelay)) yield break;
			DoAfterDelay();
		}
	}

	private IEnumerator PlayScopePlayable()
	{
		float num = 0f;
		int scopeIndex = 0;
		List<HitScopCfg> scopes = mHitComponent.GetScopes();
		foreach (HitScopCfg scope in scopes)
		{
			float num2 = scope.Delay - num;
			if (mScopePlayableFollowDelay && num2 > 0f)
			{
				yield return (object)new WaitForSeconds(num2);
			}
			num = scope.Delay;
			Pos2d grid = mHitComponent.GridTransform.LocalToWorldPos(scope.Pos);
			Vector3 pos = ViewServices.GridToPos(grid);
			if (mScopeZFollowHitZ)
			{
				pos.z = ((Component)mHitEntity).transform.position.z;
			}
			GameObject val = CreateGo(pos, ((Component)mHitEntity).transform.parent, "tmp_scope_obj");
			(ViewServices as IFightHitScopeOriginObserver)?.HitScopeCreated(mHitComponent.Entity.Id, val);
			MyPlayableManager.Play(mScopePlayable, val, val);
			(ViewServices as IFightHitViewArrivalObserver)?.HitViewArrived(
				mHitComponent.SkillId, mHitComponent.Entity.Id, grid, scopeIndex++);
		}
		mScopeCoroutine = 0L;
	}

	private GameObject CreateSourceGo()
	{
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		if (!TryGetSkillContext(out var _, out var source, out var skillView))
		{
			return null;
		}
		if (mSourceToMeAnchor == EFightHitSourceToMeAnchor.SkillPlayableTarget)
		{
			return skillView?.GetPlayableTargetGo();
		}
		if (mIsApplyOffsetToPlayable)
		{
			Pos2d pos = mGridTransform.Pos;
			IFightSkillViewServices gridGround = ViewServices;
			bool flipX = ((Component)source).GetComponent<MyGridTransform>().FlipX;
			FightHitViewFormula.SkillHitBaseDelayInfoProvider delayInfoProvider = GetDelayInfoProvider();
			Vector3 pos2 = FightHitViewFormula.CalcSkillStartPos(gridGround, delayInfoProvider.IsUseCenterRotation, flipX, GetStartPos(), pos, delayInfoProvider.Offset, delayInfoProvider.TargetOffset, delayInfoProvider.RotateCenterOffset);
			return CreateGo(pos2, ((Component)mHitEntity).transform.parent);
		}
		return ((Component)source).gameObject;
	}

	protected virtual Pos2d GetStartPos()
	{
		if (!TryGetSkillContext(out var skillEntity, out var _, out var _))
		{
			return default;
		}
		MyGridTransform component = ((Component)skillEntity).GetComponent<MyGridTransform>();
		if (!((Object)(object)component != (Object)null))
		{
			return default;
		}
		return component.Pos;
	}

	private Dictionary<string, object> GetPlayableParam()
	{
		if (mPlayableParams == null)
		{
			mPlayableParams = new Dictionary<string, object>();
		}
		mPlayableParams["ParamSource2TargetDirection"] = mHitComponent.GridTransform.Direction;
		return mPlayableParams;
	}

	private GameObject CreateGo(Vector3 pos, Transform parent, string name = "")
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject(name);
		val.transform.SetParent(parent);
		val.transform.localPosition = pos;
		val.transform.localRotation = Quaternion.identity;
		val.transform.localScale = Vector3.one;
		mTmpObjs.Add(val);
		return val;
	}

	private void OnHitPosChange(Pos2d newPos)
	{
		mGridTransform.SetPositionByGrid(newPos);
	}

	protected void PlayPlayable(GameObject playable, GameObject source, GameObject target, Dictionary<string, object> param = null)
	{
		MyPlayableManager.Play(playable, source, target, 1f, null, param);
	}

	protected bool TryGetSkillContext(out MyEntity skillEntity, out MyEntity source, out ECFightSkillView skillView)
	{
		skillEntity = null;
		source = null;
		skillView = null;
		MyContainer myContainer = mHitEntity.myContainer;
		if ((Object)(object)myContainer == (Object)null)
		{
			return false;
		}
		skillEntity = ViewServices.GetEntity(mHitComponent.SkillId);
		if ((Object)(object)skillEntity == (Object)null)
		{
			return false;
		}
		FightSkillComponent component = skillEntity.Entity.GetComponent<FightSkillComponent>();
		if (component == null)
		{
			return false;
		}
		source = ViewServices.GetEntity(component.SrcEntityId);
		if ((Object)(object)source == (Object)null)
		{
			return false;
		}
		skillView = CurrentFightViewLookup.Get<ECFightSkillView>(skillEntity);
		return true;
	}

	private FightHitViewFormula.SkillHitBaseDelayInfoProvider GetDelayInfoProvider()
	{
		return mDelayInfoProvider?.GetCurrentDelayInfoProvider() ?? default(FightHitViewFormula.SkillHitBaseDelayInfoProvider);
	}

	private void StopCoroutineIfRunning(ref long coroutineId)
	{
		if (coroutineId != 0L)
		{
			StopCoroutine(coroutineId);
			coroutineId = 0L;
		}
	}

	public virtual void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables != null)
		{
			if ((Object)(object)mPlayable != (Object)null)
			{
				playables.Add(mPlayable);
			}
			if ((Object)(object)mScopePlayable != (Object)null)
			{
				playables.Add(mScopePlayable);
			}
			if ((Object)(object)mSourceToMePlayable != (Object)null)
			{
				playables.Add(mSourceToMePlayable);
			}
		}
	}
}

}
