// Migrated selected original; see story-mask-runtime-source-manifest.json.
using System;
using Zjcs.Official.StoryBubbles;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.StoryMasks
{
using Object = UnityEngine.Object;
using UnityEngine;
using UnityEngine.UI;

public class AttachToObjectLocalUI : AttachToObjUI
{
	private CanvasScaler mCanvasScaler;

	protected override void UpdatePosition()
	{

		if (!((Object)(object)AttachObject == (Object)null))
		{
			if ((Object)(object)mCanvasScaler == (Object)null)
			{
				mCanvasScaler = ((Component)this).GetComponentInParent<CanvasScaler>();
			}
			Vector2 val = RectTransformUtility.WorldToScreenPoint(StoryMaskProjection.GetUiCamera(this), AttachObject.transform.position);
			float canvasScaleFactor = StoryMaskProjection.GetCanvasScaleFactor(Canvas, mCanvasScaler);
			val /= canvasScaleFactor;
			((Component)this).transform.localPosition = (val + new Vector2(myOffset.x, myOffset.y));
		}
	}
}

}
