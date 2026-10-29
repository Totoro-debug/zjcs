
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
public interface IEntityDescParamsProvider
{
	bool TryGetParams(IMiniMapMarkDataQueries queries, int mapClassId, IMiniMapMarkEntity ecEntity, out CultureTextArg[] paramsObject);
}

}
