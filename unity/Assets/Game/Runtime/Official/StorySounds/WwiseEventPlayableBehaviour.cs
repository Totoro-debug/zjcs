// Migrated official source; see map14-story-sound-runtime source receipt.
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using UnityEngine;
using UnityEngine.Playables;

namespace Zjcs.Official.StorySounds
{
public class WwiseEventPlayableBehaviour : PlayableBehaviour
{
	public Event WwiseEvent;

	public GameObject WwiseEventSource;

	public override void OnBehaviourPlay(Playable playable, FrameData info)
	{
		if (WwiseEvent != null)
		{
			Debug.Log("eventSource" + WwiseEventSource.name);
			WwiseEvent.Post(WwiseEventSource);
		}
	}
}

}
