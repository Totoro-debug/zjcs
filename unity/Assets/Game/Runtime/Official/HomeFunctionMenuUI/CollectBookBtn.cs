// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CollectBookBtn : MonoBehaviour, IWindowOperator
{
	public IHomeFunctionMenuServices Services { private get; set; }

	public Button Btn;

	[Tooltip("打开图鉴界面后默认选中哪种图鉴")]
	public CollectionType DefaultCollectionType;

	private Dictionary<FunctionType, IHomeHudCondition> mConditionDict = new Dictionary<FunctionType, IHomeHudCondition>();

	private bool mRegistered;

	private CollectBookDataBase mData;

	public GameObject Operator => gameObject;

	private void Awake()
	{
		Btn.onClick.AddListener(OnBtnClick);
		InitCondition(FunctionType.SkillCollectBook);
		InitCondition(FunctionType.TreasureCollectBook);
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
		Services.ReportClick("FunctionEntrysPanel", "FunctionEntryPanel_Book_Click");
		Services.PopupCollectBook(this, (IHomeMenuCollectBookPanel go) =>
		{
			go.Init(DefaultCollectionType, mData);
		});
	}

	public void SetCollectBookData(CollectBookDataBase data)
	{
		mData = data;
		DefaultCollectionType = data.CollectionType;
	}
}
}
