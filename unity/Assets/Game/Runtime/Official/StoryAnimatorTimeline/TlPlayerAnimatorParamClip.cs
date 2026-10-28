// Migrated original source; see map14-story-animator-timeline receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryAnimation;
using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Zjcs.Official.StoryAnimatorTimeline
{
[Serializable]
public class TlPlayerAnimatorParamClip : PlayableAsset
{
	public TlPlayerAnimatorParamBehaviour Template = new TlPlayerAnimatorParamBehaviour();

	public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
	{
		return ScriptPlayable<TlPlayerAnimatorParamBehaviour>.Create(graph, Template, 0);
	}
}

}
