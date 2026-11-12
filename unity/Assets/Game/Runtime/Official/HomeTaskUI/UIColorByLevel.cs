// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIColorByLevel : MonoBehaviour
{
	public IHomeTaskServices Services { get; set; }
	[Header("颜色类型")]
	[SerializeField]
	private CombatRateingColorType mCombatRateColorType;

	[Header("变色对象")]
	[SerializeField]
	private List<Graphic> mGraphics;

	[Header("变色对象")]
	[SerializeField]
	private List<TextMeshProColorProxy> mTmpMediators;

	[SerializeField]
	private List<SpriteRenderer> mSprites;

		private IHomeTaskCombatColors CombatRatingConfig => Services.CombatRatingConfig;

	public void SetColorByLevel(int level)
	{
		CombatRatingConfig.Value.TryGetLevelColor(mCombatRateColorType, level, out var color);
		foreach (Graphic mGraphic in mGraphics)
		{
			mGraphic.color = color;
		}
		foreach (TextMeshProColorProxy mTmpMediator in mTmpMediators)
		{
			mTmpMediator.SetColor(color);
		}
		foreach (SpriteRenderer mSprite in mSprites)
		{
			mSprite.color = color;
		}
	}
}
}
