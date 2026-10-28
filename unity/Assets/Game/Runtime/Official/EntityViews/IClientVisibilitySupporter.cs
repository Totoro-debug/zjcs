// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using System;

namespace Zjcs.Official.EntityViews
{
public interface IClientVisibilitySupporter : IMyPlayableNeedStop
{
	bool IsVisible { get; }

	event Action OnVisibleUpdate;

	event Action OnShow;

	event Action OnHide;

	bool SetEntityVisible(int key, bool isVisible);

	void SetForceVisible(bool forceVisible);

	void ResetVisibility();

	void Register(ECViewEntityBinding owner);

	void Unregister();

	IEntityFadeAnimationSupporter GetFadeAnimationSupporter();
}

}
