using System.Collections.Generic;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
public static class LoEntityDescParamsUtils
{
	private static List<ILoEntityDescParamsProviders> sIEntityDescParamsProvider = new List<ILoEntityDescParamsProviders>
	{
		new LoEntityStageResultTransportDescParamsProvider()
	};

	public static CultureTextArg[] GetLoEntityDescParams(IMiniMapMarkDataQueries queries, int mapClassId, MiniMapMarkLoEntityInfo wrap)
	{
		foreach (ILoEntityDescParamsProviders item in sIEntityDescParamsProvider)
		{
			if (item.TryGetParams(queries, mapClassId, wrap, out var paramsObject))
			{
				return paramsObject;
			}
		}
		return null;
	}
}

}
