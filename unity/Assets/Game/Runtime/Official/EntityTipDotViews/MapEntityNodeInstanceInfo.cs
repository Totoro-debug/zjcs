// Migrated original map tip-dot application code; see entity tip-dot receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Official.EntityTipDotViews
{
public class MapEntityNodeInstanceInfo : BaseNodeInstanceInfo
{
	public ECViewEntityData Entity;

	public Pos2d Pos { get; private set; }

	public MapEntityNodeInstanceInfo(ECViewEntityData ent)
	{
		Entity = ent;
		Pos = EntityTipDotServices.Require.GetPosition(ent);
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
