// Complete selected original coordinator with an injected local-data request port.
namespace Zjcs.Official.World.Map13MiniMap
{
using Zjcs.Official.World.NativeLabelUI;
using UnityEngine;

public class OfflineMiniMap : MiniMapManagerBase
{
	public IMiniMapOfflineDataSource DataSource { get; set; }

	public OfflineMiniMapMaskManager OfflineMiniMapMask;

	public OfflineMiniMapMarkManager OfflineMiniMapEntity;

	[Header("True为得到数据时展示的内容")]
	public StateSwitcher<bool> HaveDataShow;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if (!MapActived && MiniMap.MapClassId > 0)
		{
			HaveDataShow.SetState(state: false);
			DataSource.Request(MiniMap.MapClassId).OnComplete += GetOfflineMiniMapData_OnComplete;
		}
	}

	private void GetOfflineMiniMapData_OnComplete(PlayerOfflineMiniMapDataResponse resp, int errCode)
	{
		if (resp != null && errCode == 0)
		{
			HaveDataShow.SetState(state: true);
			OfflineMiniMapMask.Init(MiniMap.MapClassId, resp);
			OfflineMiniMapEntity.Init(MiniMap.MapClassId, resp);
		}
	}
}

}
