
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
public class LoEntityStageResultTransportDescParamsProvider : ILoEntityDescParamsProviders
{
	bool ILoEntityDescParamsProviders.TryGetParams(IMiniMapMarkDataQueries queries, int mapClassId, MiniMapMarkLoEntityInfo wrap, out CultureTextArg[] paramsObject)
	{
		paramsObject = null;
		MiniMapMarkTransportTargetInfo mapTransportTargetInfo = queries.GetMapTransportTargetInfo(mapClassId, wrap.LoEntityId);
		if (mapTransportTargetInfo == null)
		{
			return false;
		}
		string regionNameKey = $"region_{mapTransportTargetInfo.TargetMapClassId}_{mapTransportTargetInfo.TargetRegion}";
		paramsObject = new CultureTextArg[1]
		{
			new CultureTextArg(regionNameKey)
		};
		return true;
	}
}

}
