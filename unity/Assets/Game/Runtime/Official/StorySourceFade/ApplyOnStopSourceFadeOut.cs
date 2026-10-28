// Migrated official application source; see source-actor fading receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using UnityEngine;

namespace Zjcs.Official.StorySourceFade
{
public class ApplyOnStopSourceFadeOut : ApplyOnStop
{
	protected override void HandleOnFadeOut(MyPlayable playable, float duration)
	{
		IEntityFadeAnimationBridge componentInChildren = playable.mySource.GetComponentInChildren<IEntityFadeAnimationBridge>(true);
		if (componentInChildren != null)
		{
			componentInChildren.EntityFadeAnimationSupporter.FadeOut(playable.myFadeOutDuration);
			return;
		}
		SpriteRendererList spriteRendererList = SpriteRendererList.PlayableSourceGetSpriteRendererList(playable);
		if ((Object)(object)spriteRendererList != (Object)null)
		{
			spriteRendererList.FadeOut(playable.myFadeOutDuration);
		}
	}
}

}
