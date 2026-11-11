// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class AchievementGroupBtn : MonoBehaviour, IWindowOperator
{
	public IHomeFunctionMenuServices Services { private get; set; }

	public Button Btn;

	private Dictionary<FunctionType, IHomeHudCondition> mConditionDict = new Dictionary<FunctionType, IHomeHudCondition>();

	private bool mRegistered;

	public GameObject Operator => gameObject;

	private void Awake()
	{
		Btn.onClick.AddListener(OnBtnClick);
		InitCondition(FunctionType.AchievementGroup);
	}

	private void OnEnable()
	{
		bool flag = IsAvailable();
		Refresh();
		if (flag)
		{
			return;
		}
		foreach (IHomeHudCondition value in mConditionDict.Values)
		{
			Services.ConditionHandler.Register(value, Refresh);
		}
		mRegistered = true;
	}

	private void OnDisable()
	{
		if (!mRegistered)
		{
			return;
		}
		foreach (IHomeHudCondition value in mConditionDict.Values)
		{
			Services.ConditionHandler.Deregister(value, Refresh);
		}
		mRegistered = false;
	}

	private void Refresh()
	{
		bool active = IsAvailable();
		Btn.gameObject.SetActive(active);
	}

	private void InitCondition(FunctionType type)
	{
		HomeMenuFunctionLimitInfo functionLimitInfo = Services.GetFunctionLimit(type);
		if (functionLimitInfo != null)
		{
			mConditionDict[type] = functionLimitInfo.Condition;
		}
	}

	private bool IsAvailable()
	{
		foreach (IHomeHudCondition value in mConditionDict.Values)
		{
			if (Services.IsMet(value))
			{
				return true;
			}
		}
		return false;
	}

	private void OnBtnClick()
	{
		Services.PopupAchievementGroups(this, (IHomeMenuAchievementPanel go) =>
		{
			go.Init(Services.GetAllAchievementGroups().ToList());
		});
	}
}
}
