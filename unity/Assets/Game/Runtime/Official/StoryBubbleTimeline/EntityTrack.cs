// Migrated original source; see map14-story-bubble-timeline receipt.
using Object = UnityEngine.Object;
using MyEntity = Zjcs.Official.StoryBubbles.BubbleEntityBinding;
using Zjcs.Official.StoryBubbles;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Zjcs.Official.StoryBubbleTimeline
{
[DisplayName("地图物件轨道")]
[TrackClipType(typeof(EntityClip))]
[TrackBindingType(typeof(MyEntity))]
public class EntityTrack : TrackAsset
{
	public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
	{
		MyEntity myEnt = go.GetComponent<PlayableDirector>().GetGenericBinding((Object)(object)this) as MyEntity;
		foreach (TimelineClip clip in ((TrackAsset)this).GetClips())
		{
			(clip.asset as EntityClip).BindEntity(myEnt);
		}
		return base.CreateTrackMixer(graph, go, inputCount);
	}
}

}
