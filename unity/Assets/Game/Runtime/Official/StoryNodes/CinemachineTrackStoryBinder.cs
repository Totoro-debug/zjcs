// Migrated official source; see map14-story-nodes-runtime source receipt.
using Object = UnityEngine.Object;
using Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Zjcs.Official.StoryNodes
{
[RequireComponent(typeof(PlayableDirector))]
public class CinemachineTrackStoryBinder : MonoBehaviour
{
	public string MainCameraKey;

	private PlayableDirector mDirector;

	public IStoryNodeRegisterMgr NodeRegisterManager { get; set; }

	private void Awake()
	{
		mDirector = ((Component)this).GetComponent<PlayableDirector>();
		PlayableAsset playableAsset = mDirector.playableAsset;
		TimelineAsset val = (TimelineAsset)(object)((playableAsset is TimelineAsset) ? playableAsset : null);
		if ((Object)(object)val == (Object)null || !NodeRegisterManager.TryGetNode(MainCameraKey, out var node))
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
}

}
