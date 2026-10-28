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
[DisplayName("物件冒泡对话")]
public class EntityTalkAsset : EntityClip, ITimelineClipAsset
{
	public EntityTalkBehaviour Template = new EntityTalkBehaviour();

	public ClipCaps clipCaps => (ClipCaps)0;

	public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
	{
		ScriptPlayable<EntityTalkBehaviour> val = ScriptPlayable<EntityTalkBehaviour>.Create(graph, Template, 0);
		EntityTalkBehaviour behaviour = val.GetBehaviour();
		behaviour.SetDuration(GetDuration(owner));
		behaviour.BindEntity(mBindEntity);
		return val;
	}

	private float GetDuration(GameObject owner)
	{
		PlayableDirector component = owner.GetComponent<PlayableDirector>();
		if ((Object)(object)component == (Object)null)
		{
			return 0f;
		}
		PlayableAsset playableAsset = component.playableAsset;
		TimelineAsset val = (TimelineAsset)(object)((playableAsset is TimelineAsset) ? playableAsset : null);
		if (val == null)
		{
			return 0f;
		}
		foreach (TrackAsset outputTrack in val.GetOutputTracks())
		{
			foreach (TimelineClip clip in outputTrack.GetClips())
			{
				if (clip.asset == (Object)(object)this)
				{
					return (float)clip.duration;
				}
			}
		}
		return 0f;
	}
}

}
