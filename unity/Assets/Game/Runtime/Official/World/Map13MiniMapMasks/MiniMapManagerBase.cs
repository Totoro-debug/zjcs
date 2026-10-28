
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
public abstract class MiniMapManagerBase : MapMeCharacterUIBase
{
	public MiniMapMaskContext MiniMap;

	private MiniMapMaskContext MapRootManagerProxy => MiniMap;

	public virtual bool MapActived => MapRootManagerProxy.CurrentMapClassId == MiniMap.MapClassId;
}

}
