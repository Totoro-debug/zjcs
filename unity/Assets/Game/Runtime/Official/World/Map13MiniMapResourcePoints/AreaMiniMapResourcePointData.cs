using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
public class AreaMiniMapResourcePointData
{
	public int Area;

	public List<MiniMapResourcePointData> Datas = new List<MiniMapResourcePointData>();

	public bool Active { get; private set; } = true;

	public void SetActive(bool active)
	{
		if (Active == active)
		{
			return;
		}
		Active = active;
		foreach (MiniMapResourcePointData data in Datas)
		{
			((Component)data.Item).gameObject.SetActive(Active);
		}
	}
}

}
