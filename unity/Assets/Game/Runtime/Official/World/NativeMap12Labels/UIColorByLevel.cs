// Selected official Map12 world-label implementation; see source receipt.
namespace Zjcs.Official.World.NativeMap12Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIColorByLevel : MonoBehaviour
{
	public IMap12LabelServices Services { get; set; }

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



	public void SetColorByLevel(int level)
	{



		Services.TryGetLevelColor(mCombatRateColorType, level, out var color);
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
