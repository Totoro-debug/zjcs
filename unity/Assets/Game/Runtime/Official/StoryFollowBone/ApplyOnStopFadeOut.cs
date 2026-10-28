// Migrated official application/framework source; see bone-following source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;

namespace Zjcs.Official.StoryFollowBone
{
public class ApplyOnStopFadeOut : ApplyOnStop
{
	protected override void HandleOnFadeOut(MyPlayable playable, float duration)
	{
		MyEffectFadeOut.EffectFadeOut(playable.myApplyComponent.gameObject, duration, playable);
	}
}

}
