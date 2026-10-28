// Migrated official application source; see source-actor fading receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using UnityEngine;

namespace Zjcs.Official.StorySourceFade
{
public class ApplyInitOnSourceFadeIn : ApplyOnInit
{
	[Tooltip("淡入的时候是否有事件")]
	public bool HasFadeInEvent = true;

	protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		SetMyApplyComponent(playable, applyAttr);
	}

	protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		SetMyApplyComponent(playable, applyAttr);
		IEntityFadeAnimationBridge componentInChildren = playable.mySource.GetComponentInChildren<IEntityFadeAnimationBridge>(true);
		if (componentInChildren != null)
		{
			componentInChildren.EntityFadeAnimationSupporter.FadeIn(playable.myFadeInDuration, HasFadeInEvent);
			return;
		}
		SpriteRendererList spriteRendererList = SpriteRendererList.PlayableSourceGetSpriteRendererList(playable);
		if ((Object)(object)spriteRendererList != (Object)null)
		{
			spriteRendererList.FadeIn(playable.myFadeInDuration, HasFadeInEvent);
		}
	}

	private void SetMyApplyComponent(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
	{
		MyPlayableApplyUtils.SetPlayableApplyComponent(playable, applyAttr, myPlayable.mySource);
	}
}

}
