// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
using UnityEngine;

public abstract class TaskConditionItemBase : MonoBehaviour
{
	public IHomeTaskServices Services { get; set; }
	[SerializeField]
	protected MultiLanguageText mText;

	private static string sMetColor;

	private static string sNotMetColor;

		private IHomeTaskGlobalColors GlobalColors => Services.GlobalColors;

	public abstract void Populate(ICondition condition, ICondition.DataHub dataHub);

	protected (string metColor, string notMetColor) GetColor()
	{
		if (string.IsNullOrEmpty(sMetColor))
		{
			sMetColor = ColorUtility.ToHtmlStringRGB(GlobalColors.Value.EnoughColor);
		}
		if (string.IsNullOrEmpty(sNotMetColor))
		{
			sNotMetColor = ColorUtility.ToHtmlStringRGB(GlobalColors.Value.LackColor);
		}
		return (metColor: sMetColor, notMetColor: sNotMetColor);
	}
}
}
