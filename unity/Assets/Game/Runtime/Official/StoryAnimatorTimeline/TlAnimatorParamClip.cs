// Migrated original source; see map14-story-animator-timeline receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryAnimation;
using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Zjcs.Official.StoryAnimatorTimeline
{
[Serializable]
public class TlAnimatorParamClip : PlayableAsset
{
	public TlAnimatorParamBehaviour template = new TlAnimatorParamBehaviour();

	public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
	{
		return ScriptPlayable<TlAnimatorParamBehaviour>.Create(graph, template, 0);
	}
}

}
