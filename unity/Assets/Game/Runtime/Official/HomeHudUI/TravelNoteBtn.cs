namespace Zjcs.Official.HomeHudUI
{
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TravelNoteBtn : MonoBehaviour
{
	public IHomeBottomBarEntryServices Services { get; set; }

	public Button myLockBtn;

	public GameObject myUnlockObj;

	private IHomeHudCondition mMapGamePlayEnergyCondition;

	private IHomeHudCondition mMapGameExploreCondition;

	private void Awake()
	{
		((UnityEvent)myLockBtn.onClick).AddListener((UnityAction)OnLockBtnClick);
	}

	private void OnEnable()
	{
		RefreshUI();
		Services.MainPlayer.OnStageCountUpdate += PlayerOnStageCountUpdate;
		Services.MainPlayer.OnActiveableEntUpdate += OnActiveableEntityUpdate;
		Services.MainPlayer.OnMapStatusUpdate += PlayerOnMapStatusUpdate;
		Services.MainPlayer.OnLevelUpdate += MainPlayer_OnLevelUpdate;
		mMapGamePlayEnergyCondition = Services.GetFunctionCondition(FunctionType.MapGamePlayEnergyCollect);
		Services.ConditionHandler.Register(mMapGamePlayEnergyCondition, RefreshUI);
		mMapGameExploreCondition = Services.GetFunctionCondition(FunctionType.WorldExplore);
		Services.ConditionHandler.Register(mMapGameExploreCondition, RefreshUI);
	}

	private void PlayerOnMapStatusUpdate(int arg1, int arg2, MapRegionStatus arg3, MapRegionStatus arg4)
	{
		RefreshUI();
	}

	private void OnActiveableEntityUpdate(IHomeMapActiveableEntity obj)
	{
		if (obj.ActiveEntType == MapWorldActiveableEntityType.Goddess)
		{
			RefreshUI();
		}
	}

	private void PlayerOnStageCountUpdate(int arg1, int arg2, int arg3)
	{
		RefreshUI();
	}

	private void MainPlayer_OnLevelUpdate(int level)
	{
		RefreshUI();
	}

	private void OnDisable()
	{
		if (Services.MainPlayer != null)
		{
			Services.MainPlayer.OnStageCountUpdate -= PlayerOnStageCountUpdate;
			Services.MainPlayer.OnActiveableEntUpdate -= OnActiveableEntityUpdate;
			Services.MainPlayer.OnMapStatusUpdate -= PlayerOnMapStatusUpdate;
			Services.MainPlayer.OnLevelUpdate -= MainPlayer_OnLevelUpdate;
		}
		if (Services.ConditionHandler != null)
		{
			Services.ConditionHandler.Deregister(mMapGamePlayEnergyCondition, RefreshUI);
			Services.ConditionHandler.Deregister(mMapGameExploreCondition, RefreshUI);
		}
	}

	private void OnLockBtnClick()
	{
		Services.SystemMessage("TravelNote.FunctionLimit");
	}

	private void RefreshUI()
	{
		bool flag = IsLimit();
		((Component)myLockBtn).gameObject.SetActive(flag);
		myUnlockObj.SetActive(!flag);
	}

	private bool IsLimit()
	{
		if (Services.MainPlayer.IsUnlockDemonBattle())
		{
			return false;
		}
		if (!Services.MainPlayer.IsFunctionLimit(FunctionType.SecretArea))
		{
			return false;
		}
		if (!Services.MainPlayer.IsFunctionLimit(FunctionType.DemonInvade))
		{
			return false;
		}
		if (Services.MainPlayer.IsMopUpUnlocked())
		{
			return false;
		}
		if (Services.MainPlayer.IsUnlockDailyDungeon())
		{
			return false;
		}
		if (!Services.MainPlayer.IsFunctionLimit(FunctionType.TeamChallenge))
		{
			return false;
		}
		if (!Services.MainPlayer.IsFunctionLimit(FunctionType.VoidFissure))
		{
			return false;
		}
		if (Services.MainPlayer.IsMapGamePlayFruitUnlock())
		{
			return false;
		}
		if (Services.MainPlayer.IsMapGamePlayEnergyCollectUnlock())
		{
			return false;
		}
		if (!Services.MainPlayer.IsFunctionLimit(FunctionType.NpcAdventure))
		{
			return false;
		}
		return true;
	}
}

}
