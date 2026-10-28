// Migrated original source; see map14-story-bubble-timeline receipt.
using Object = UnityEngine.Object;
using MyEntity = Zjcs.Official.StoryBubbles.BubbleEntityBinding;
using Zjcs.Official.StoryBubbles;
using UnityEngine.Playables;

namespace Zjcs.Official.StoryBubbleTimeline
{
public abstract class EntityClip : PlayableAsset
{
	protected MyEntity mBindEntity;

	public void BindEntity(MyEntity myEnt)
	{
		mBindEntity = myEnt;
	}
}

}
