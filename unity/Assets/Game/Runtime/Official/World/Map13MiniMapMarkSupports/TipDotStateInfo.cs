using System;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.EntityLifecycle;
[Serializable]
public struct TipDotStateInfo
{
	public ETipDotLevelType TipLevel;

	public string Data;

	public override string ToString()
	{
		return string.Format("{0}[ TipLevel:{1} Data:{2} ]", "TipDotStateInfo", TipLevel, Data);
	}
}

}
