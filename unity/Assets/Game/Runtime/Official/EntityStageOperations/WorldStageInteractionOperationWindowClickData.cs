// Migrated complete original stage operation; see stage operation receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityWorldStages;
using StageDialog = Zjcs.Official.WorldUI.StageDialog;
using System;

namespace Zjcs.Official.EntityStageOperations
{
[Serializable]
public struct WorldStageInteractionOperationWindowClickData
{
	public WorldStageInteractionOperationWindowClickType Type;

	public ECWorldStageInteractionOperationItemData OperationData;
}

}
