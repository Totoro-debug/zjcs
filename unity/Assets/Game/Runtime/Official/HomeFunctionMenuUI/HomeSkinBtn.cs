// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using UnityEngine;
using UnityEngine.UI;

public class HomeSkinBtn : MonoBehaviour
{
	public IHomeFunctionMenuServices Services { private get; set; }

	public Button Button;

	public WorldStateSwitcher<bool> ShowSwitcher;

	private IHomeHudCondition mCondition;


	private void Awake()
	{
		Button.onClick.AddListener(OnBtnClick);
	}

	private void OnEnable()
	{
		if (Services.Player.IsFunctionLimit(FunctionType.HomeSkin))
		{
			HomeMenuFunctionLimitInfo functionLimitInfo = Services.GetFunctionLimit(FunctionType.HomeSkin);
			mCondition = functionLimitInfo.Condition;
			Services.ConditionHandler.Register(functionLimitInfo.Condition, OnCondition);
		}
		OnCondition();
	}

	private void OnDisable()
	{
		if (mCondition != null)
		{
			Services.ConditionHandler.Deregister(mCondition, OnCondition);
			mCondition = null;
		}
	}

	private void OnBtnClick()
	{
		if (!Services.Player.IsFunctionLimit(FunctionType.HomeSkin))
		{
			Services.ReportClick("HomeSkinPanel", "FunctionEntryPanel_HomeSkin_Click");
			Services.PopupHomeSkin((IHomeMenuPanel go) =>
			{
				go.Init();
			});
		}
	}

	private void OnCondition()
	{
		bool flag = Services.Player.IsFunctionLimit(FunctionType.HomeSkin);
		ShowSwitcher.SetState(!flag && Services.IsHomeScene);
	}
}
}
