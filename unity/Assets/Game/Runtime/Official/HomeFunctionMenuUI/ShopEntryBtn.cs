// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using UnityEngine;
using UnityEngine.UI;

public class ShopEntryBtn : MonoBehaviour
{
	public IHomeFunctionMenuServices Services { private get; set; }

	public Button Btn;

	public FunctionEntryBigBtnItem BtnItem;

	private IHomeHudCondition mFunctionLimitCondition;

	private void Awake()
	{
		Btn.onClick.AddListener(HandleOnClick);
		if (Services.Player.IsFunctionLimit(FunctionType.ShopActivity))
		{
			mFunctionLimitCondition = Services.GetFunctionLimit(FunctionType.ShopActivity).Condition;
			Services.ConditionHandler.Register(mFunctionLimitCondition, OnShopFunctionLimitUpdate);
			Btn.enabled = false;
			BtnItem.SetEmpty(isEmpty: true);
		}
	}

	private void OnDestroy()
	{
		Btn.onClick.RemoveListener(HandleOnClick);
		if (mFunctionLimitCondition != null)
		{
			Services.ConditionHandler.Deregister(mFunctionLimitCondition, OnShopFunctionLimitUpdate);
			mFunctionLimitCondition = null;
		}
	}

	private void OnShopFunctionLimitUpdate()
	{
		if (!Services.Player.IsFunctionLimit(FunctionType.ShopActivity))
		{
			if (mFunctionLimitCondition != null)
			{
				Services.ConditionHandler.Deregister(mFunctionLimitCondition, OnShopFunctionLimitUpdate);
				mFunctionLimitCondition = null;
			}
			Btn.enabled = true;
			BtnItem.SetEmpty(isEmpty: false);
		}
	}

	private void HandleOnClick()
	{
		Services.ReportClick("FunctionEntrysPanel", "FunctionEntryPanel_Shop_Click");
		Services.PopupShopActivityEntry((IHomeMenuPanel panel) =>
		{
			panel.Init();
		});
	}
}
}
