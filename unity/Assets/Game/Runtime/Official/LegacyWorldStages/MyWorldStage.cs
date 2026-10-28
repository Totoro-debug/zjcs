// Complete original MyWorldStage client class; see legacy world-stage receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.LegacyWorldStages
{
public class MyWorldStage : LegacyWorldStageComponent, IMyEntityReplaceEventReceiver, IEventReceiver
{


	[Tooltip("当前Entity的图标")]
	public SpriteRenderer EntityIcon;

	public GameObject myAppearPlayable;

	[SerializeField]

	private bool mCanInteractAfterMove = true;

	public List<LegacyWorldStageStateCfg> myStateCfgs;

	public WorldStateSwitcher<int> StageSwitcher;

	[SerializeField]
	[Tooltip("不受状态控制的LoopPlayable")]
	private GameObject mStateNotControlLoopPlayable;

	private IEntityFadeAnimationSupporter mFadeAnimationSupporter;

	private ECWorldStageView mWorldStageView;

	private MyPlayable mLoopPlayable;

	private MyPlayable mStatePlayable;

	private bool mNewEntity;

	private Action<EntityWorldStageUpdateEventMessage> mStageUpdateAction;

	private Action mOnEntityVisible;

	private Action mSpriteListOnFadeInCompleted;

	private Action mStopPlayable;

	private IDisposable mEcEntityBufferEventDisposable;

	public ILegacyWorldStageEntityLookup MapRootManagerProxy { get; set; }

	public IMyPlayableManager MyPlayableManager { get; set; }

	public IEcEntityBufferEventHandleModule EcEntityBufferEventHandleModule { get; set; }

	protected override void Awake()
	{
		base.Awake();
		mStageUpdateAction = WorldStageUpdate;
		mOnEntityVisible = OnEntityVisible;
		mSpriteListOnFadeInCompleted = SpriteListOnFadeInCompleted;
		mStopPlayable = StopPlayable;
	}

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		ECViewDriver componentInChildren = ((Component)this).GetComponentInChildren<ECViewDriver>(true);
		if ((Object)(object)componentInChildren != (Object)null)
		{
			mWorldStageView = componentInChildren.GetView<ECWorldStageView>();
		}
		mFadeAnimationSupporter = ((Component)this).GetComponentInChildren<IEntityFadeAnimationBridge>()?.EntityFadeAnimationSupporter;
		if (((ILegacyWorldStageVisibilitySource)myEntity).ClientVisibilitySupporter != null)
		{
			((ILegacyWorldStageVisibilitySource)myEntity).ClientVisibilitySupporter.OnVisibleUpdate += mOnEntityVisible;
		}
		mNewEntity = newEntity;
		if (mFadeAnimationSupporter != null)
		{
			mFadeAnimationSupporter.AddOnFadeInCompletedEvent(mSpriteListOnFadeInCompleted);
			mFadeAnimationSupporter.AddOnFadeInStartEvent(mStopPlayable);
			mFadeAnimationSupporter.AddOnFadeOutStartEvent(mStopPlayable);
		}
		InitStateEffect();
		((IECWorldStageReplacementSource)myEntity).AddReplaceReceiver(this);
		mEcEntityBufferEventDisposable = EcEntityBufferEventHandleModule.Subscribe(myEntity.Entity, mStageUpdateAction);
	}

	private void SpriteListOnFadeInCompleted()
	{
		PlayStatePlayable();
	}

	private void PlayStatePlayable()
	{
		if (CheckCanPlayable())
		{
			if ((Object)(object)mLoopPlayable == (Object)null && (Object)(object)mStateNotControlLoopPlayable != (Object)null)
			{
				mLoopPlayable = MyPlayableManager.Play(mStateNotControlLoopPlayable, ((Component)this).gameObject, ((Component)this).gameObject);
			}
			if (mNewEntity && (Object)(object)myAppearPlayable != (Object)null)
			{
				PlayAppearPlayable();
			}
			else
			{
				PlayLoopPlayable();
			}
		}
	}

	private bool CheckCanPlayable()
	{
		if (mFadeAnimationSupporter != null && !mFadeAnimationSupporter.IsIn)
		{
			return false;
		}
		if (((ILegacyWorldStageVisibilitySource)myEntity).ClientVisibilitySupporter == null || !((ILegacyWorldStageVisibilitySource)myEntity).ClientVisibilitySupporter.IsVisible)
		{
			return false;
		}
		return true;
	}

	public override void OnEnd(object userData)
	{
		mEcEntityBufferEventDisposable?.Dispose();
		mEcEntityBufferEventDisposable = null;
		StopPlayable();
		((IECWorldStageReplacementSource)myEntity).RemoveReplaceReceiver(this);
		if (mFadeAnimationSupporter != null)
		{
			mFadeAnimationSupporter.RemoveOnFadeInCompletedEvent(mSpriteListOnFadeInCompleted);
			mFadeAnimationSupporter.RemoveOnFadeInStartEvent(mStopPlayable);
			mFadeAnimationSupporter.RemoveOnFadeOutStartEvent(mStopPlayable);
		}
		if ((bool)(Object)(object)myEntity && ((ILegacyWorldStageVisibilitySource)myEntity).ClientVisibilitySupporter != null)
		{
			((ILegacyWorldStageVisibilitySource)myEntity).ClientVisibilitySupporter.OnVisibleUpdate -= mOnEntityVisible;
		}
		base.OnEnd(userData);
	}

	private void InitStateEffect()
	{
		if (mWorldStageView == null)
		{
			UpdateStageSwitcher();
			PlayStatePlayable();
		}
	}

	private void WorldStageUpdate(EntityWorldStageUpdateEventMessage eventMsg)
	{
		if (mWorldStageView != null)
		{
			return;
		}
		UpdateStageSwitcher();
		StopPlayable();
		LegacyWorldStageStateCfg curStateCfg = GetCurStateCfg();
		if (curStateCfg == null)
		{
			return;
		}
		if ((Object)(object)curStateCfg.myStartPlayable != (Object)null)
		{
			mStatePlayable = MyPlayableManager.Play(curStateCfg.myStartPlayable, ((Component)this).gameObject, ((Component)this).gameObject);
			if ((Object)(object)mStatePlayable != (Object)null)
			{
				mStatePlayable.OnStop += new Action<MyPlayable>(OnStartPlayableStop);
			}
		}
		else
		{
			PlayLoopPlayable(curStateCfg);
		}
	}

	private void OnStartPlayableStop(MyPlayable playable)
	{
		playable.OnStop -= new Action<MyPlayable>(OnStartPlayableStop);
		PlayLoopPlayable();
	}

	private void OnAppearPlayableStop(MyPlayable playable)
	{
		playable.OnStop -= new Action<MyPlayable>(OnAppearPlayableStop);
		mNewEntity = !CheckCanPlayable();
		PlayLoopPlayable();
	}

	private void PlayLoopPlayable(LegacyWorldStageStateCfg cfg)
	{
		if (CheckCanPlayable() && (Object)(object)cfg.myLoopPlayable != (Object)null)
		{
			StopPlayable();
			mStatePlayable = MyPlayableManager.Play(cfg.myLoopPlayable, ((Component)this).gameObject, ((Component)this).gameObject);
		}
	}

	public void PlayLoopPlayable()
	{
		if (mWorldStageView == null)
		{
			LegacyWorldStageStateCfg curStateCfg = GetCurStateCfg();
			if (curStateCfg != null)
			{
				PlayLoopPlayable(curStateCfg);
			}
		}
	}

	public void PlayAppearPlayable()
	{
		if (mNewEntity && !((Object)(object)myAppearPlayable == (Object)null))
		{
			StopPlayable();
			mStatePlayable = MyPlayableManager.Play(myAppearPlayable, ((Component)this).gameObject, ((Component)this).gameObject);
			if (!((Object)(object)mStatePlayable == (Object)null))
			{
				mStatePlayable.OnStop += new Action<MyPlayable>(OnAppearPlayableStop);
			}
		}
	}

	public void StopPlayable()
	{
		if (!((Object)(object)mStatePlayable == (Object)null))
		{
			mStatePlayable.Stop();
			mStatePlayable = null;
		}
	}

	private void UpdateStageSwitcher()
	{
		if (StageSwitcher != null)
		{
			StageSwitcher.SetState(Component.State);
		}
	}

	public OfficialStageStateInfo GetCurStateInfo()
	{
		return Component.GetCurStateInfo();
	}

	public LegacyWorldStageStateCfg GetCurStateCfg()
	{
		if (Component.State < 0)
		{
			return null;
		}
		ECViewDriver componentInChildren = ((Component)this).GetComponentInChildren<ECViewDriver>(true);
		if ((Object)(object)componentInChildren != (Object)null)
		{
			ECWorldStageView view = componentInChildren.GetView<ECWorldStageView>();
			if (view != null)
			{
				return view.GetCurStateOldCfg();
			}
		}
		if (Component.State >= myStateCfgs.Count)
		{
			return null;
		}
		return myStateCfgs[Component.State];
	}

	public int GetStateComponentIndex(StageComponentType compType, int subStateIndex = 0)
	{
		if (Component == null)
		{
			return -1;
		}
		return Component.GetStateComponentIndex(compType, subStateIndex);
	}

	void IMyEntityReplaceEventReceiver.MyEntity_OnReplace(ECViewEntityBinding entity, EcEntityIdentifier preEntId)
	{
		InitStateEffect();
	}

	public bool CanInteractAfterMove()
	{
		return mCanInteractAfterMove;
	}

	private void OnEntityVisible()
	{
		if (!((ILegacyWorldStageVisibilitySource)myEntity).ClientVisibilitySupporter.IsVisible)
		{
			StopPlayable();
		}
		else
		{
			InitStateEffect();
		}
	}

	public ECViewEntityBinding GetInteractionRedirectEnt()
	{
		if (Component?.InteractionRedirectStage == null)
		{
			return null;
		}
		ECViewEntityBinding entity = MapRootManagerProxy.GetEntity(Component.InteractionRedirectStage.Entity.Id);
		if ((Object)(object)entity == (Object)null)
		{
			return null;
		}
		return entity;
	}
}

}
