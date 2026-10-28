using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeMap13Labels;
public class MiniMapKeepSizeObj : UIBase
{
	public GameObject myContent;

	
	public float ZoomThreshold = 0.5f;

	protected MiniMapSizingContext mMinimap;

	private MinimapKeepSizeObjHelper mKeepSizeHelper = new MinimapKeepSizeObjHelper();

	public void Init(MiniMapSizingContext miniMap, float zoomThreshold)
	{
		InitWrapper(() =>
		{
			mMinimap = miniMap;
			if (zoomThreshold > 0f)
			{
				ZoomThreshold = zoomThreshold;
			}
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mKeepSizeHelper.Start(mMinimap, myContent, ZoomThreshold);
	}

	protected override void OnDisableAndInited()
	{
		mKeepSizeHelper.End();
		base.OnDisableAndInited();
	}
}

}
