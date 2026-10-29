namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.EntityLifecycle;
public class MiniMapManagedMapPartInfo
{
public bool SupportPublicRegion,SupportPrivateRegion;
public bool IsRegionStatusMix()
	{
		if (SupportPublicRegion)
		{
			return SupportPrivateRegion;
		}
		return false;
	}
public virtual bool RegionStatusSupported(MapRegionStatus status)
	{
		return MiniMapManagedRules.MapRegionStatusSupport(status, SupportPublicRegion, SupportPrivateRegion);
	}
public virtual bool RegionStatusOnlySupport(MapRegionStatus status)
	{
		if (IsRegionStatusMix())
		{
			return false;
		}
		return MiniMapManagedRules.MapRegionStatusSupport(status, SupportPublicRegion, SupportPrivateRegion);
	}
}

}
