// Migrated official client source; see entity world-stage receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using System;
using UnityEngine;

namespace Zjcs.Official.EntityWorldStages
{
public class ECWorldStageView : ECViewBase, IMyEntityReplaceEventReceiver, IEventReceiver
{
	[Tooltip("当前Entity的图标")]
	public SpriteRenderer EntityIcon;

	[SerializeField]

	private bool mCanInteractAfterMove = true;

	public WorldStateSwitcher<int> StageSwitcher;

	[SerializeField]
	private ECWorldStageStateCfgBaseProviderItemData mWorldStageCfgProviderData;

	private ECViewEntityBinding mMyEntity;

	private ECWorldStageStateData mWorldStageComponent;

	private Action<EntityWorldStageUpdateEventMessage> mStageUpdateAction;

	private LegacyWorldStageStateCfg mStageCfgTmp = new LegacyWorldStageStateCfg();

	private IDisposable mEcEntityBufferEventDisposable;

	public int State => mWorldStageComponent.State;

	public IEcEntityBufferEventHandleModule EcEntityBufferEventHandleModule { get; set; }

	public event Action<ECWorldStageStateData, int, object> OnStateUpdate;

	protected override void Load()
	{
		base.Load();
		mMyEntity = mOwner.GetComponentInParent<ECViewEntityBinding>(true);
		mWorldStageComponent = GetComponent<ECWorldStageStateData>();
		((IECWorldStageReplacementSource)mMyEntity).AddReplaceReceiver(this);
		InitStateEffect();
		mEcEntityBufferEventDisposable = EcEntityBufferEventHandleModule.Subscribe<EntityWorldStageUpdateEventMessage>(mMyEntity.Entity, WorldStageUpdate);
	}

	protected override void Unload()
	{
		mEcEntityBufferEventDisposable?.Dispose();
		mEcEntityBufferEventDisposable = null;
		((IECWorldStageReplacementSource)mMyEntity).RemoveReplaceReceiver(this);
		mWorldStageCfgProviderData.ECWorldStageCfgProvider.Release();
		base.Unload();
	}

	private void InitStateEffect()
	{
		UpdateStageSwitcher();
	}

	private void WorldStageUpdate(EntityWorldStageUpdateEventMessage eventMsg)
	{
		UpdateStageSwitcher();
		OnStateUpdate?.Invoke(eventMsg.StageComp, eventMsg.PreState, eventMsg.Param);
	}

	private void UpdateStageSwitcher()
	{
		if (StageSwitcher != null)
		{
			StageSwitcher.SetState(mWorldStageComponent.State);
		}
	}

	public OfficialStageStateInfo GetCurStateInfo()
	{
		return mWorldStageComponent.GetCurStateInfo();
	}

	public WorldStageStateCfg GetCurStateCfg()
	{
		return mWorldStageCfgProviderData.ECWorldStageCfgProvider.GetStageStateCfg(mServiceProvider, mWorldStageComponent);
	}

	public LegacyWorldStageStateCfg GetCurStateOldCfg()
	{
		WorldStageStateCfg stageStateCfg = mWorldStageCfgProviderData.ECWorldStageCfgProvider.GetStageStateCfg(mServiceProvider, mWorldStageComponent);
		if (stageStateCfg == null)
		{
			return null;
		}
		mStageCfgTmp.myUIName = stageStateCfg.myUIName;
		mStageCfgTmp.myLoopPlayable = stageStateCfg.myLoopPlayable;
		mStageCfgTmp.myStartPlayable = stageStateCfg.myStartPlayable;
		mStageCfgTmp.InteractionOperation = stageStateCfg.InteractionOperation;
		return mStageCfgTmp;
	}

	public int GetStateComponentIndex(StageComponentType compType, int subStateIndex = 0)
	{
		if (mWorldStageComponent == null)
		{
			return -1;
		}
		return mWorldStageComponent.GetStateComponentIndex(compType, subStateIndex);
	}

	void IMyEntityReplaceEventReceiver.MyEntity_OnReplace(ECViewEntityBinding entity, EcEntityIdentifier preEntId)
	{
		InitStateEffect();
	}

	public bool CanInteractAfterMove()
	{
		return mCanInteractAfterMove;
	}
}

}
