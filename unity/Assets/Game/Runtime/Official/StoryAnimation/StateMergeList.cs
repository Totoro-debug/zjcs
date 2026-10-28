// Migrated official source; see map14-story-animator-runtime source receipt.
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Official.StoryAnimation
{
public class StateMergeList<T>
{
	private List<(int, T)> mStates = new List<(int, T)>();

	private T mInitState;

	public T CurState
	{
		get
		{
			if (mStates.Count <= 0)
			{
				return mInitState;
			}
			return mStates.Last().Item2;
		}
	}

	public int Count => mStates.Count;

	public StateMergeList(T initState)
	{
		mInitState = initState;
	}

	public void Add((int, T) info)
	{
		mStates.Add(info);
	}

	public bool Remove(int key)
	{
		foreach (var mState in mStates)
		{
			if (mState.Item1 == key)
			{
				return mStates.Remove(mState);
			}
		}
		return false;
	}
}

}
