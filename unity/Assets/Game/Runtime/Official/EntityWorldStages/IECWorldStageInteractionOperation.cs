// Migrated official client source; see entity world-stage receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using System;

namespace Zjcs.Official.EntityWorldStages
{
public interface IECWorldStageInteractionOperation
{
	bool DoOper(IServiceProvider serviceProvider, ECWorldStageStateData worldStageComponent);

	bool ExistOperation<T>() where T : IECWorldStageInteractionOperation;

	void Release();
}

}
