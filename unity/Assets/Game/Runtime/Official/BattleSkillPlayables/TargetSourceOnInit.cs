using UnityEngine;
using Zjcs.Official.StoryPlayables;

public abstract class TargetSourceOnInit : MyPlayableOnInit
{
}

public static class MyPlayableTargetSourceUtils
{
	public static void SetPlayableTargetSourceGo(MyPlayable playable, GameObject go, bool isSource)
	{
		if (playable == null || go == null)
			return;

		if (isSource)
			playable.mySource = go;
		else
			playable.myTarget = go;
	}
}
