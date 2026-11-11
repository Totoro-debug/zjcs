// Original menu component; explicit local service bindings only.
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using UnityEngine;
using UnityEngine.UI;

public class FriendBtn : MonoBehaviour, IWindowOperator
{
	public IHomeFunctionMenuServices Services { private get; set; }

	public Button myBtn;

	private IHomeHudCondition mCondition;

	private bool mRegistered;

	public GameObject Operator => gameObject;

	private void Awake()
	{
		myBtn.onClick.AddListener(onBtnClick);
		mCondition = Services.GetFunctionLimit(FunctionType.Friend).Condition;
	}

	private void OnEnable()
	{
		bool flag = IsAvailable();
		Refresh();
		if (!flag)
		{
			Services.ConditionHandler.Register(mCondition, Refresh);
			mRegistered = true;
		}
	}

	private void OnDisable()
	{
		if (mRegistered)
		{
			Services.ConditionHandler.Deregister(mCondition, Refresh);
			mRegistered = false;
		}
	}

	private void onBtnClick()
	{
		Services.ReportClick("FunctionEntrysPanel", "FunctionEntryPanel_Friend_Click");
		Services.PopupFriend(this);
	}

	public bool IsAvailable()
	{
		return Services.IsMet(mCondition);
	}

	private void Refresh()
	{
		bool active = IsAvailable();
		myBtn.gameObject.SetActive(active);
	}
}
}
