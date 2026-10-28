// Migrated original map tip-dot application code; see entity tip-dot receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Official.EntityTipDotViews
{
public class EntityOwnershipData
{

	public EntityOwnershipType Type;

	public long OwnerId;

	public ushort GetOpCode()
	{
		return 40340;
	}
}

}
