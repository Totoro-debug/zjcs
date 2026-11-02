// Migrated current official application source; see status playable runtime receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryAttachment;
using UnityEngine;

namespace Zjcs.Official.BattleStatusPlayables
{
public class MyPlayableStackPlay
{
	private IMyPlayableManager mMyPlayableManager;

	private GameObject mLastEndPlayablePrefab;

	private MyPlayable mStayPlayable;

	private int mStack;

	public MyPlayableStackPlay(IMyPlayableManager myPlayableManager)
	{
		mMyPlayableManager = myPlayableManager;
	}

	public void Play(int stack, GameObject beginPlayable, GameObject stayPlayable, GameObject endPlayable, GameObject source, GameObject target)
	{
		if ((Object)(object)stayPlayable == (Object)null)
		{
			Debug.LogError((object)"stayPlayable is null, cannot play stay playable.");
			return;
		}
		if (stack > mStack && (Object)(object)beginPlayable != (Object)null)
		{
			mMyPlayableManager.Play(beginPlayable, source, target);
		}
		if (stack < mStack && mStack != 0 && (Object)(object)mLastEndPlayablePrefab != (Object)null)
		{
			mMyPlayableManager.Play(mLastEndPlayablePrefab, source, target);
		}
		mStack = stack;
		mLastEndPlayablePrefab = endPlayable;
		if ((Object)(object)mStayPlayable != (Object)null)
		{
			mStayPlayable.Stop();
		}
		mStayPlayable = mMyPlayableManager.Play(stayPlayable, source, target);
	}

	public void Stop(int stack, GameObject source, GameObject target)
	{
		if ((Object)(object)mStayPlayable != (Object)null)
		{
			mStayPlayable.Stop();
			mStayPlayable = null;
		}
		if (stack < mStack && mStack != 0 && (Object)(object)mLastEndPlayablePrefab != (Object)null)
		{
			mMyPlayableManager.Play(mLastEndPlayablePrefab, source, target);
		}
		mLastEndPlayablePrefab = null;
	}
}

}
