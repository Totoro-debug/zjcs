// Migrated official application source; see entity playable view receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.StoryPlayableManager;
using UnityEngine;

namespace Zjcs.Official.EntityViewPlayables
{
public interface IECPlayableSupporter
{
	IClientVisibilitySupporter ClientVisibilitySupporter { get; }

	IEntityFadeAnimationSupporter EntityFadeAnimationSupporter { get; }

	ECViewEntityData EcEntity { get; }

	GameObject Source { get; }

	bool IsNewEntity { get; }

	T GetComponent<T>();

	T GetECView<T>() where T : ECViewBase;

	MyPlayable PlayPlayable(GameObject playable, GameObject source, GameObject target);
}

}
