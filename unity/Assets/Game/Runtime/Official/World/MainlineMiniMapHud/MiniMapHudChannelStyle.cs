using UnityEngine;
namespace Zjcs.Official.World.Map13MiniMap
{
public static class MiniMapHudChannelStyle
{
public static (Color Color, string DescKey) GetChannelUiInfo(MiniMapHudChannelColors globalColors, PublicMapLineStatus status)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		return status switch
		{
			PublicMapLineStatus.Busy => (Color: globalColors.ServiceLoadBusy, DescKey: "ui_channel_busy"),
			PublicMapLineStatus.Full => (Color: globalColors.ServiceLoadFull, DescKey: "ui_channel_full"),
			PublicMapLineStatus.Overload => (Color: globalColors.ServiceLoadOverLoad, DescKey: "ui_channel_overload"),
			_ => (Color: globalColors.ServiceLoadFree, DescKey: "ui_channel_free"),
		};
	}
}
}
