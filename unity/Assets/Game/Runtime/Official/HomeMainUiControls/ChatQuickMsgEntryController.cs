using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
namespace Zjcs.Official.HomeMainUiControls
{
// Original selected home UI control; local service bindings only.
using UnityEngine;

public class ChatQuickMsgEntryController : MonoBehaviour
{
	public GameObject ChatQuickMsgEntry;

	private IHomeHudCondition mCondition;

	public IHomeMainUiConditions Services { private get; set; }

	private bool mRegistered;

	private void Awake()
	{
		mCondition = Services.GetFunctionCondition(FunctionType.Chat);
	}

	private void OnEnable()
	{
		if (!IsAvailable())
		{
			mRegistered = true;
			Services.ConditionHandler.Register(mCondition, Refresh);
		}
		Refresh();
	}

	private void OnDisable()
	{
		if (Services.ConditionHandler != null && mRegistered)
		{
			mRegistered = false;
			Services.ConditionHandler.Deregister(mCondition, Refresh);
		}
	}

	public bool IsAvailable()
	{
		if (!Services.IsMet(mCondition))
		{
			return false;
		}
		if (Services.HasMainMap)
		{
			return CheckAvailableInMap();
		}
		return true;
	}

	private void Refresh()
	{
		bool active = IsAvailable();
		if (ChatQuickMsgEntry != null)
		{
			ChatQuickMsgEntry.SetActive(active);
		}
	}

	private bool CheckAvailableInMap()
	{
		return Services.OpenMapChannel;
	}
}
}
