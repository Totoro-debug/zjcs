using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
namespace Zjcs.Official.HomeMainUiControls
{
// Original selected home UI control; local service bindings only.
using UnityEngine;

public class MainTaskFunctionLimit : MonoBehaviour
{
	[SerializeField]
	private GameObject myTaskSlot;

	private IHomeHudCondition mCondition;

	public IHomeMainUiConditions Services { private get; set; }

	protected void OnEnable()
	{
		int taskFirstTraceConditionId = Services.TaskFirstTraceConditionId;
		mCondition = Services.GetCondition(taskFirstTraceConditionId);
		if (mCondition == null)
		{
			Debug.LogError("没有找到 Condition: "+taskFirstTraceConditionId+" <- TaskFirstTraceConditionId");
		}
		else
		{
			Services.ConditionHandler.Register(mCondition, OnConditionUpdate);
			OnConditionUpdate();
		}
	}

	protected void OnDisable()
	{
		if (mCondition != null)
		{
			Services.ConditionHandler.Deregister(mCondition, OnConditionUpdate);
			mCondition = null;
		}
	}

	private void OnConditionUpdate()
	{
		IHomeHudCondition condition = mCondition;
		if (condition != null && Services.IsMet(condition) && !myTaskSlot.activeSelf)
		{
			myTaskSlot.SetActive(value: true);
		}
	}
}
}
