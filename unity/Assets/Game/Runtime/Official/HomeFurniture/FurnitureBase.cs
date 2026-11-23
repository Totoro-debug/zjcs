// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;

public abstract class FurnitureBase : ClickableHomeObj
{
	public enum EFurnitureState
	{
		Locked,
		RepairNotice,
		CanRepair,
		Functional
	}

	[SerializeField]
	protected EFurniture mFurniture;

	[SerializeField]
	private WorldStateSwitcher<EFurnitureState> mStateSwitcher;

	[FormerlySerializedAs("mHomeRepairNotice")]
	[SerializeField]
	private HomeNotice mHomeNotice;

	[SerializeField]
	private GameObject mRepairEffect;

	[SerializeField]
	private float mRepairEffectShowTime = 0.6f;

	[SerializeField]
	private float mRepairEffectFullTime = 1.5f;

	protected EFurnitureState mFurnitureState;

	private FurnitureInfo mFurnitureInfo;

	private IHomeFurnitureStoryContext mStoryPlayContext;

	private bool mIsListeningUnlockedCondition;

	private Action mUpdateState;

	private IDisposable mHomeChangeDisposable;

	private IDisposable mConditionHandlerRegistration;

	public EFurniture Furniture => mFurniture;
    public IHomeFurnitureServices Services { get; set; }

    // Bind the recovered original fields before activation/Start.
    public void Bind(EFurniture furniture, WorldStateSwitcher<EFurnitureState> states,
        HomeNotice notice, GameObject repairEffect, float showTime, float fullTime,
        IHomeFurnitureServices services)
    {
        mFurniture = furniture;
        mStateSwitcher = states;
        mHomeNotice = notice;
        mRepairEffect = repairEffect;
        mRepairEffectShowTime = showTime;
        mRepairEffectFullTime = fullTime;
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

	protected FurnitureInfo FurnitureInfo
	{
		get
		{
			if (mFurnitureInfo == null)
			{
				mFurnitureInfo = Services.GetFurnitureInfo(mFurniture);
			}
			return mFurnitureInfo;
		}
	}

	protected override void Start()
	{
		base.Start();
		if (mUpdateState == null)
		{
			mUpdateState = UpdateState;
		}
		UpdateState();
		if (mFurnitureState != EFurnitureState.Functional)
		{
			mHomeNotice.Init(FurnitureInfo.RepairNoticeCondition, FurnitureInfo.UnlockCondition, mUpdateState, mUpdateState);
		}
		EFurnitureState eFurnitureState = mFurnitureState;
		mIsListeningUnlockedCondition = eFurnitureState == EFurnitureState.Locked || eFurnitureState == EFurnitureState.RepairNotice;
		if (mIsListeningUnlockedCondition)
		{
			mConditionHandlerRegistration = Services.SubscribeCondition(FurnitureInfo.UnlockCondition, OnMeetLockedCondition);
		}
		Services.RegisterAwardHandler(HandleReward);
		Services.Player.OnRepairFurniture += MainPlayer_OnRepairFurniture;
		mHomeChangeDisposable = Services.SubscribePostHomeChange(OnHomeChange);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (Services != null)
		{
			Services.RemoveAwardHandler(HandleReward);
		}
		if (Services.Player != null)
		{
			Services.Player.OnRepairFurniture -= MainPlayer_OnRepairFurniture;
		}
		if (mConditionHandlerRegistration != null)
		{
			mConditionHandlerRegistration.Dispose();
			mConditionHandlerRegistration = null;
		}
		if (mStoryPlayContext != null)
		{
			Services.DisposeStory(mStoryPlayContext);
			mStoryPlayContext = null;
		}
		mHomeChangeDisposable?.Dispose();
		mHomeChangeDisposable = null;
	}

	protected virtual void OnHomeChange()
	{
		UpdateState();
		if (mFurnitureState != EFurnitureState.Functional)
		{
			mHomeNotice.Init(FurnitureInfo.RepairNoticeCondition, FurnitureInfo.UnlockCondition, mUpdateState, mUpdateState);
		}
	}

	private void MainPlayer_OnRepairFurniture(EFurniture furniture)
	{
		if (furniture == mFurniture)
		{
			if (((Behaviour)this).isActiveAndEnabled && (Object)(object)mRepairEffect != (Object)null)
			{
				((MonoBehaviour)this).StartCoroutine(RepairEffect());
			}
			else
			{
				UpdateState();
			}
		}
	}

	private void OnMeetLockedCondition()
	{
		UpdateState();
	}

	protected override void OnValidClick()
	{
		switch (mFurnitureState)
		{
		case EFurnitureState.Functional:
			if (Services.CheckFunctionDisabled(mFurniture))
			{
				Services.ShowError(53851);
			}
			else
			{
				OnClick();
			}
			break;
		case EFurnitureState.RepairNotice:
			mHomeNotice.ShowConditionTip();
			break;
		case EFurnitureState.CanRepair:
			DoRepair();
			break;
		case EFurnitureState.Locked:
			break;
		}
	}

	protected abstract void OnClick();

	public virtual void UpdateState()
	{
		mFurnitureState = GetState();
		mStateSwitcher.SetState(mFurnitureState);
	}

	public virtual EFurnitureState GetState()
	{
		IHomeFurniturePlayerData playerData = Services.PlayerData;
		if (playerData.HasFurniture(mFurniture))
		{
			return EFurnitureState.Functional;
		}
		if (playerData.IsMetFurnitureRepairCondition(FurnitureInfo))
		{
			return EFurnitureState.CanRepair;
		}
		if (!playerData.IsMetFurnitureNoticeCondition(FurnitureInfo))
		{
			return EFurnitureState.Locked;
		}
		return EFurnitureState.RepairNotice;
	}

	private bool HandleReward(HomeFurnitureAward msg)
	{
		if (!msg.IsRepairFurniture)
		{
			return false;
		}
		Debug.Log(GetType().Name + " HandleAward RepairFurniture", this);
		return true;
	}

	private IEnumerator RepairEffect()
	{
		mRepairEffect.SetActive(true);
		yield return (object)new WaitForSeconds(mRepairEffectShowTime);
		UpdateState();
		yield return (object)new WaitForSeconds(mRepairEffectFullTime - mRepairEffectShowTime);
		mRepairEffect.SetActive(false);
	}

	private void DoRepair()
	{
		string storyName = mFurnitureInfo.RepairStoryName;
		if (!string.IsNullOrEmpty(storyName) && !Services.Player.HasStartedStory(storyName))
		{
			Services.DoStoryAction(() =>
			{
				if (mStoryPlayContext == null)
				{
					mStoryPlayContext = Services.CreateStoryContext("FurnitureBase");
				}
				Services.PlayStory(mStoryPlayContext, storyName);
			});
		}
		else
		{
			Services.PopupRepair(Furniture, isBuild: false);
		}
	}
}

}
