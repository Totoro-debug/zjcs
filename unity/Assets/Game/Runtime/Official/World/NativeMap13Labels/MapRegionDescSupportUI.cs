// Migrated official Map13 world-label call path; see source receipt.
namespace Zjcs.Official.World.NativeMap13Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using UnityEngine;

public class MapRegionDescSupportUI : MapUIBase
{
	[Header("区域描述Ui")]
	[SerializeField]
	private MapRegionDescItemUI mMapRegionDescItemUI;

	private bool mInited;

	

	

	protected override void OnEnableAndInited()
	{

		base.OnEnableAndInited();
		int num = Services.QueryRegion(((Component)this).transform.position);
		if (num == -1)
		{
			Debug.LogError((object)("prefab[" + ((Object)this).name + "]超出地图范围"), (Object)(object)((Component)this).gameObject);
		}
		else if (!mInited)
		{
			mMapRegionDescItemUI.Init(Services.MapClassId, num);
			mInited = true;
		}
	}
}

}
