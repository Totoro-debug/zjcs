
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
public class EntityStageResultTransportDescParamsProvider : IEntityDescParamsProvider
{
	bool IEntityDescParamsProvider.TryGetParams(IMiniMapMarkDataQueries queries, int mapClassId, IMiniMapMarkEntity ecEntity, out CultureTextArg[] paramsObject)
	{
		paramsObject = null;
		IMiniMapMarkStageTransport component = ecEntity.GetComponent<IMiniMapMarkStageTransport>();
		if (component == null)
		{
			return false;
		}
		if (component.TargetLoEntityId <= 0)
		{
			paramsObject = new CultureTextArg[1]
			{
				new CultureTextArg($"Mainland_{mapClassId}")
			};
			return true;
		}
		MiniMapMarkLoEntityInfo containerEntityInfo = queries.GetContainerEntityInfo(mapClassId, component.TargetLoEntityId);
		if (containerEntityInfo == null)
		{
			return false;
		}
		paramsObject = new CultureTextArg[1]
		{
			new CultureTextArg($"region_{mapClassId}_{containerEntityInfo.PosInfo.Region}")
		};
		return true;
	}
}

}
