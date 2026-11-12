// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
using UnityEngine;

public class TaskConditionItem : TaskConditionItemBase
{
	public GameObject FinishedGo;

	public GameObject UnFinishedGo;

	public override void Populate(ICondition condition, ICondition.DataHub dataHub)
	{
		(string, string) color = GetColor();
		ConditionDescComponent descComponent = condition.GetDescComponent(dataHub, null, color.Item1, color.Item2);
		mText.SetKey(descComponent.Key, descComponent.TextProcessor, null, descComponent.Args);
		bool flag = condition.IsMet(dataHub);
		if (FinishedGo != null)
		{
			FinishedGo.SetActive(flag);
		}
		if (UnFinishedGo != null)
		{
			UnFinishedGo.SetActive(!flag);
		}
	}
}
}
