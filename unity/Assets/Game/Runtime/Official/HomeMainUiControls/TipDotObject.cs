using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
namespace Zjcs.Official.HomeMainUiControls
{
// Original selected home UI control; local service bindings only.
using System;
using UnityEngine;
using UnityEngine.UI;

public class TipDotObject : MonoBehaviour
{
	[SerializeField]
	private Image mTipDotImage;

	[SerializeField]
	private SpriteRenderer mTipDotSprite;

	public IProfessionTipDotSystem TipDotSystem { private get; set; }

	private void OnEnable()
	{
		IProfessionTipDotSystem tipDotSystem = TipDotSystem;
		tipDotSystem.OnTipDotShowChanged = (Action)Delegate.Combine(tipDotSystem.OnTipDotShowChanged, new Action(OnTipDotShowChanged));
		Refresh();
	}

	private void OnDisable()
	{
		IProfessionTipDotSystem tipDotSystem = TipDotSystem;
		tipDotSystem.OnTipDotShowChanged = (Action)Delegate.Remove(tipDotSystem.OnTipDotShowChanged, new Action(OnTipDotShowChanged));
	}

	private void OnTipDotShowChanged()
	{
		Refresh();
	}

	private void Refresh()
	{
		if (mTipDotImage != null)
		{
			mTipDotImage.enabled = TipDotSystem.IsShowTipDot;
		}
		if (mTipDotSprite != null)
		{
			mTipDotSprite.enabled = TipDotSystem.IsShowTipDot;
		}
	}
}
}
