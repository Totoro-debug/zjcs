// Migrated official client source; see entity world-stage receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.EntityWorldStages
{
public interface IMyEntityReplaceEventReceiver : IEventReceiver
{
	void MyEntity_OnReplace(ECViewEntityBinding entity, EcEntityIdentifier preEntId);
}

}
