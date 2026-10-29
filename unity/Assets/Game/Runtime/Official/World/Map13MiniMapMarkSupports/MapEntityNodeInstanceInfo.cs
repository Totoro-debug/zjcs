
namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.EntityLifecycle;
public class MapEntityNodeInstanceInfo : BaseNodeInstanceInfo
{
	public IMiniMapMarkEntity Entity;

	public Pos2d Pos { get; private set; }

	public MapEntityNodeInstanceInfo(IMiniMapMarkEntity ent)
	{
		Entity = ent;
		Pos = ent.GridTransform.Pos;
	}

	public override string GetInstanceDesc()
	{
		return $"[EntClassId:{Entity.ClassId}, Pos:({Pos})]";
	}

	public override bool Equals(object obj)
	{
		if (!(obj is MapEntityNodeInstanceInfo mapEntityNodeInstanceInfo))
		{
			return false;
		}
		if (mapEntityNodeInstanceInfo.Entity.ClassId == Entity.ClassId)
		{
			return mapEntityNodeInstanceInfo.Pos == Pos;
		}
		return false;
	}

	public override int GetHashCode()
	{
		int hashCode = Entity.ClassId.GetHashCode();
		int hashCode2 = Pos.GetHashCode();
		return 31 * hashCode + hashCode2;
	}
}

}
