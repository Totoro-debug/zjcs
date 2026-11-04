using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.World.Map13MiniMapWindows
{
using Object=UnityEngine.Object;
using Debug=UnityEngine.Debug;
public class WindowMaskEffectColor : WindowMaskEffectBase
{
	private Color DefaultMaskColor = new Color(0f, 0f, 0f, 0.8f);

	public Color MaskColor;

	public override void ApplyEffect(WindowMaskBase mask)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)mask.MaskImg == (Object)null))
		{
			((Graphic)mask.MaskImg).color = MaskColor;
		}
	}

	public override void CancelEffect(WindowMaskBase mask)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)mask.MaskImg == (Object)null))
		{
			((Graphic)mask.MaskImg).color = DefaultMaskColor;
		}
	}

	public WindowMaskEffectColor()
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
	}
}

}
