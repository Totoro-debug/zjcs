// Migrated official source; see map14-story-sound-runtime source receipt.
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using UnityEngine;
using UnityEngine.Playables;

namespace Zjcs.Official.StorySounds
{
public class WwiseEventPlayableAsset : PlayableAsset
{
	public Event wwiseEvent;

	public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		ScriptPlayable<WwiseEventPlayableBehaviour> val = ScriptPlayable<WwiseEventPlayableBehaviour>.Create(graph, 0);
		WwiseEventPlayableBehaviour behaviour = val.GetBehaviour();
		behaviour.WwiseEvent = wwiseEvent;
		if ((Object)(object)owner != (Object)null)
		{
			behaviour.WwiseEventSource = owner;
		}
		return (Playable)val;
	}
}

}
