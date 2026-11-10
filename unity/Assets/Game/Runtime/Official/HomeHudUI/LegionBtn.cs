namespace Zjcs.Official.HomeHudUI
{
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LegionBtn : MonoBehaviour
{
	public IHomeBottomBarEntryServices Services { get; set; }

	public Button myLockBtn;

	public GameObject myUnlockObj;

	private IHomeHudCondition mCondition;

	private void Awake()
	{
		((UnityEvent)myLockBtn.onClick).AddListener((UnityAction)OnLockBtnClick);
		mCondition = Services.GetFunctionCondition(FunctionType.Legion);
	}

	private void OnEnable()
	{
		RefreshUI();
		Services.ConditionHandler.Register(mCondition, RefreshUI);
	}

	private void OnDisable()
	{
		Services.ConditionHandler.Deregister(mCondition, RefreshUI);
	}

	private void OnLockBtnClick()
	{
		Services.SystemMessage("Legion.FunctionLimit");
	}

	private void RefreshUI()
	{
		bool flag = Services.MainPlayer != null && Services.MainPlayer.IsFunctionLimit(FunctionType.Legion);
		((Component)myLockBtn).gameObject.SetActive(flag);
		myUnlockObj.SetActive(!flag);
	}
}

}
