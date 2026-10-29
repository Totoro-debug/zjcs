using Cysharp.Threading.Tasks;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
public class MiniMapEntityEntryMark : MiniMapEntityMark
{
	private IMiniMapMarkWorldEntry mWorldEntry;

	private IMiniMapMarkDataQueries MapRootManagerProxy => Queries;

	private IMiniMapMarkDataQueries MyMapPositionHandlerProxy => Queries;

	public MiniMapEntityEntryMark(int mapClassId, int region, IMiniMapMarkEntity entity, IMiniMapMarkDataQueries queries)
		: base(mapClassId, region, entity, queries)
	{
		mWorldEntry = Entity.GetComponent<IMiniMapMarkWorldEntry>();
		if (mWorldEntry == null)
		{
			UnityEngine.Debug.LogError(string.Concat(new object[] { "物件", entity.ClassId, "对应的MiniMapEntityEntryMark 没有配置WorldActiveableEntityComponent" }));
		}
	}

	public override bool InteractPossible()
	{
		int regionId = MyMapPositionHandlerProxy.QueryRegion(mWorldEntry.GridTransform.Pos);
		if (Queries.RegionIsActived(MapClassId, regionId))
		{
			return true;
		}
		return mWorldEntry.Active;
	}

	public override void OnClick()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		if (Queries.CurrentMeMapId > 0)
		{
			UniTaskExtensions.Forget<bool>(MapRootManagerProxy.DoTransfer(mWorldEntry.Entity));
		}
	}
}

}
