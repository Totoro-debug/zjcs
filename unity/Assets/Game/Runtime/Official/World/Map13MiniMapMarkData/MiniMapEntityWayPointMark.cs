using Cysharp.Threading.Tasks;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
public class MiniMapEntityWayPointMark : MiniMapEntityMark
{
	private IMiniMapMarkWayPoint wayPoint;

	private IMiniMapMarkDataQueries MapRootManagerProxy => Queries;

	public MiniMapEntityWayPointMark(int mapClassId, int region, IMiniMapMarkEntity entity, IMiniMapMarkDataQueries queries)
		: base(mapClassId, region, entity, queries)
	{
		wayPoint = Entity.GetComponent<IMiniMapMarkWayPoint>();
	}

	public override bool InteractPossible()
	{
		return wayPoint.IsUsable();
	}

	public override void OnClick()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		UniTaskExtensions.Forget<bool>(MapRootManagerProxy.DoTransfer(wayPoint.Entity));
	}
}

}
