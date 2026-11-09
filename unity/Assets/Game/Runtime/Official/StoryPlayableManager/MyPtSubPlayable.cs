// Original GameFramework playable trigger.
using UnityEngine;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.StoryPlayableManager
{
public class MyPtSubPlayable : MyPlayableTrigger
{
	[Tooltip("playable预制体")]
	public GameObject PlayablePrefab;

	public IMyPlayableManager mMyPlayableManager { get; set; }

	public override void Play(MyPlayable playable)
	{
		mMyPlayableManager.Play(PlayablePrefab, playable.mySource, playable.myTarget, playable.Speed, playable.myParentGo, playable.myParam);
	}
}

}
