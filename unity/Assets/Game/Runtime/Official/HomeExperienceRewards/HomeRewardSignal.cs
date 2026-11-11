// Migrated selected official implementation; service ports only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeExperienceRewards
{
public class HomeRewardSignal<T1, T2>
{
	private readonly HashSet<Action<T1, T2>> mActionSet = new HashSet<Action<T1, T2>>();

	private readonly Stack<List<Action<T1, T2>>> mStack = new Stack<List<Action<T1, T2>>>();

	private List<Action<T1, T2>> PopList()
	{
		if (mStack.Count > 0)
		{
			return mStack.Pop();
		}
		return new List<Action<T1, T2>>();
	}

	private void PushList(List<Action<T1, T2>> list)
	{
		mStack.Push(list);
	}

	public void AddListener(Action<T1, T2> action)
	{
		mActionSet.Add(action);
	}

	public void RemoveListener(Action<T1, T2> action)
	{
		mActionSet.Remove(action);
	}

	public void ClearListener()
	{
		mActionSet.Clear();
	}

	public void Invoke(T1 param1, T2 param2)
	{
		if (mActionSet.Count == 0)
		{
			return;
		}
		List<Action<T1, T2>> list = PopList();
		list.Clear();
		list.AddRange(mActionSet);
		foreach (Action<T1, T2> item in list)
		{
			try
			{
				item(param1, param2);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		list.Clear();
		PushList(list);
	}

	public static HomeRewardSignal<T1, T2> operator +(HomeRewardSignal<T1, T2> safeAction, Action<T1, T2> action)
	{
		safeAction.AddListener(action);
		return safeAction;
	}

	public static HomeRewardSignal<T1, T2> operator -(HomeRewardSignal<T1, T2> safeAction, Action<T1, T2> action)
	{
		safeAction.RemoveListener(action);
		return safeAction;
	}
}

}
