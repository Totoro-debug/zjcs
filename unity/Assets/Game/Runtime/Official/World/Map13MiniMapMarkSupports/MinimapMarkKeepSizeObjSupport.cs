using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.EntityLifecycle;
public class MinimapMarkKeepSizeObjSupport : MiniMapMarkSupportBase
{
	[SerializeField]
	private GameObject mMyContent;

	[SerializeField]
	private float mDefaultZoomThreshold = 0.1f;

	private MinimapKeepSizeObjHelper mKeepSizeHelper = new MinimapKeepSizeObjHelper();

	public override void Init(MiniMapMarkUIBase markUi)
	{
		MiniMapMarkContext miniMap = markUi.MiniMap;
		float zoomThreshold = GetZoomThreshold(miniMap);
		mKeepSizeHelper.Start(miniMap, mMyContent, zoomThreshold);
	}

	public override void Release(MiniMapMarkUIBase markUi)
	{
		mKeepSizeHelper.End();
	}

	private float GetZoomThreshold(MiniMapMarkContext minimap)
	{
		if ((Object)(object)minimap == (Object)null || minimap.MiniMapSetting == null)
		{
			return mDefaultZoomThreshold;
		}
		float markZoomThreshold = minimap.MiniMapSetting.MarkZoomThreshold;
		if (markZoomThreshold > 0f)
		{
			return markZoomThreshold;
		}
		return mDefaultZoomThreshold;
	}
}

}
