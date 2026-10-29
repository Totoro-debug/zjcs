using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.StoryPlayables;
using IWindowOperator=Zjcs.Official.ProfessionUI.IWindowOperator;
[MiniMapWindow("Map/MiniMap/MiniMapMarkDialog/MiniMapEntryMarkDialog")]
public class MiniMapEntryMarkDialog : MiniMapFunctionMarkDialog
{
	private MiniMapMarkContext MapRootManagerProxy => miniMapMark.MiniMap;

	public override void Init(MiniMapMarkUIBase mark)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		base.Init(mark);
		Vector3 worldPosition = MapRootManagerProxy.GridToWorld(MapRootManagerProxy.CurrentMapClassId, mark.GridPos);
		if (MapRootManagerProxy.HasMapGroundRoot && MapRootManagerProxy.HasPartitionController)
		{
			MapRootManagerProxy.ReservePartition(worldPosition);
		}
	}
}

}
