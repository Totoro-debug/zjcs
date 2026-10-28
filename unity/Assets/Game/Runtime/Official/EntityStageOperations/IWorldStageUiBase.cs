// Migrated complete original stage operation; see stage operation receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityWorldStages;
using StageDialog = Zjcs.Official.WorldUI.StageDialog;

namespace Zjcs.Official.EntityStageOperations
{
public interface IWorldStageUiBase
{
	void InitWindowClick(ECWorldStageStateData worldStage, IWorldStageInteractionOperationWindowHandlerBase windowsHandler);

	void OnClick(WorldStageInteractionOperationWindowClickType type);
}

}
