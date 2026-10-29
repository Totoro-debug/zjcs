
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
public class MiniMapOfflineWayPointMark : MiniMapOfflineMark
{
	public MiniMapOfflineWayPointMark(int mapClassId, int loEntId, int entClassId, int state, IMiniMapMarkDataQueries queries)
		: base(mapClassId, loEntId, entClassId, state, queries)
	{
	}

	public override bool InteractPossible()
	{
		return MiniMapMarkWayPointRules.IsUsable(Queries, MapClassId, EntityWrapInfo.PosInfo.Region);
	}

	public override void OnClick()
	{
		Queries.DoEnterMap(MapClassId, EntityWrapInfo.LoEntityId);
	}
}

}
