// Original GameFramework playable trigger.
using UnityEngine;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.StoryPlayableManager
{
public class MyPtStopPlayable : MyPlayableTrigger
{
	public MyPlayable myPlayable;

	public bool myImmediate;

	public override void Play(MyPlayable playable)
	{
		if ((Object)(object)myPlayable != (Object)null)
		{
			myPlayable.Stop(myImmediate);
		}
	}
}

}
