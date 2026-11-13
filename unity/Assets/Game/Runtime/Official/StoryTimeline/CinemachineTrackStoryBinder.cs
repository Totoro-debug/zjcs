// Migrated original source; see story-timeline-source-manifest.json.
using Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Zjcs.Official.StoryTimeline
{
using Object = UnityEngine.Object;
[RequireComponent(typeof(PlayableDirector))]
public class CinemachineTrackStoryBinder : MonoBehaviour
{
	public string MainCameraKey;

	private PlayableDirector mDirector;
	private IStoryTimelineBindings Bindings;
	public void Bind(IStoryTimelineBindings bindings) => Bindings = bindings;

	private void Awake()
	{
		mDirector = ((Component)this).GetComponent<PlayableDirector>();
		PlayableAsset playableAsset = mDirector.playableAsset;
		TimelineAsset val = (TimelineAsset)(object)((playableAsset is TimelineAsset) ? playableAsset : null);
		if ((Object)(object)val == (Object)null || !TryGetNode(MainCameraKey, out var node))
		{
			return;
		}
		foreach (TrackAsset outputTrack in val.GetOutputTracks())
		{
			CinemachineTrack val2 = (CinemachineTrack)(object)((outputTrack is CinemachineTrack) ? outputTrack : null);
			if (!((Object)(object)val2 == (Object)null) && !(mDirector.GetGenericBinding((Object)(object)outputTrack) != (Object)null))
			{
				mDirector.SetGenericBinding((Object)(object)val2, (Object)(object)node.GetComponent<CinemachineBrain>());
			}
		}
	}
private bool TryGetNode(string key, out GameObject node)
    { node = Bindings.ResolveNode(key); return node != null; }
}

}
