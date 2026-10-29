
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.EntityLifecycle;
public class MiniMapPublicMarkManager : MiniMapMarkManager
{
	private MiniMapManagedContext MarkContext => (MiniMapManagedContext)MiniMap;
	protected override MapRegionStatus HandleStatus => MapRegionStatus.Public;

	protected override void CreateMark(int region, IMiniMapSpawnEntity myEnt, MiniMapMarkExtendInfo info = null)
	{
		info = info ?? MarkContext.MarkDataQueries.GetEntityExtend(myEnt.ClassId);
		if (info.MiniMapMarkShowInPublic && MiniMapManagedRules.EntityShowTipForMe(MarkContext, myEnt))
		{
			base.CreateMark(region, myEnt, info);
		}
	}

	protected override bool CreateEntityIconCheck(int region, IMiniMapManagedStage worldStage, MiniMapMarkExtendInfo info)
	{
		if (worldStage == null)
		{
			return false;
		}
		return info.MiniMapMarkShowInPublic;
	}

	protected override void RegistMark(MiniMapMarkUIBase markUI, EcEntityIdentifier entId)
	{
	}

	protected override void UnregistMark(MiniMapMarkUIBase markUI, EcEntityIdentifier entId)
	{
	}
}

}
