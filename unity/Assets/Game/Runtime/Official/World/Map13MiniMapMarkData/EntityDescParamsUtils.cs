using System.Collections.Generic;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
public static class EntityDescParamsUtils
{
	private static List<IEntityDescParamsProvider> sIEntityDescParamsProvider = new List<IEntityDescParamsProvider>
	{
		new EntityStageResultTransportDescParamsProvider()
	};

	public static CultureTextArg[] GetEntityDescParams(IMiniMapMarkDataQueries queries, int mapClassId, IMiniMapMarkEntity ecEntity)
	{
		foreach (IEntityDescParamsProvider item in sIEntityDescParamsProvider)
		{
			if (item.TryGetParams(queries, mapClassId, ecEntity, out var paramsObject))
			{
				return paramsObject;
			}
		}
		return null;
	}
}

}
