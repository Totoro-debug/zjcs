// Migrated official application source; see source-actor fading receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using System;
using UnityEngine;

namespace Zjcs.Official.StorySourceFade
{
public interface IEntityFadeAnimationSupporter
{
	Transform ModelTransform { get; }

	bool IsIn { get; }

	void FadeIn(float durationS = -1f, bool hasEvent = true, int priority = 0);

	void ImmediateFadeIn(bool hasEvent = true, int priority = 0);

	void FadeOut(float durationS = -1f, bool hasEvent = true, int priority = 0);

	void ImmediateFadeOut(bool hasEvent = true, int priority = 0);

	void AddOnFadeOutStartEvent(Action onFadeOutStart);

	void AddOnFadeOutCompletedEvent(Action onFadeOutCompleted);

	void AddOnFadeInStartEvent(Action onFadeInStart);

	void AddOnFadeInCompletedEvent(Action onFadeInCompleted);

	void RemoveOnFadeOutStartEvent(Action onFadeOutStart);

	void RemoveOnFadeOutCompletedEvent(Action onFadeOutCompleted);

	void RemoveOnFadeInStartEvent(Action onFadeInStart);

	void RemoveOnFadeInCompletedEvent(Action onFadeInCompleted);

	void ResetFadeAnimation();
}

}
