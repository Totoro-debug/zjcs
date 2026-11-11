// Original menu recommendation implementation; explicit data/resource bindings.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.SkillsUI.FancyScrollView;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ActivityRecommendEventUpItem : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
{
	public ActivityRecommendItemCell Items;
	private IHomeMenuActivityServices Services => Items.ActivityServices;


	public void OnPointerClick(PointerEventData eventData)
	{
		if (Items == null || Items.CurItem == null)
		{
			return;
		}
		Dictionary<string, string> extend = new Dictionary<string, string>
		{
			{
				"ActivityType",
				Items.CurItem.ActivityEntryData.Type.ToString()
			},
			{
				"Id",
				Items.CurItem.ActivityEntryData.Id.ToString()
			}
		};
		Services.ReportRecommendationClick("FunctionEntrysPanel", "FunctionEntryPanel_Activity_Recommend_Click", extend);
		GameObject gameObject = eventData.pointerCurrentRaycast.gameObject;
		foreach (RaycastResult item in Services.ForeachRaycastResults(eventData.position))
		{
			if (gameObject != item.gameObject)
			{
				ExecuteEvents.Execute(item.gameObject, eventData, ExecuteEvents.pointerClickHandler);
				break;
			}
		}
	}
}
}
