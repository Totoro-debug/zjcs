using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeCamera
{
public class SafeAction<T1>
{
	private readonly HashSet<Action<T1>> mActionSet = new HashSet<Action<T1>>();

	private readonly Stack<List<Action<T1>>> mStack = new Stack<List<Action<T1>>>();

	private List<Action<T1>> PopList()
	{
		if (mStack.Count > 0)
		{
			return mStack.Pop();
		}
		return new List<Action<T1>>();
	}

	private void PushList(List<Action<T1>> list)
	{
		mStack.Push(list);
	}

	public void AddListener(Action<T1> action)
	{
		mActionSet.Add(action);
	}

	public void RemoveListener(Action<T1> action)
	{
		mActionSet.Remove(action);
	}

	public void ClearListener()
	{
		mActionSet.Clear();
	}

	public void Invoke(T1 param1)
	{
		if (mActionSet.Count == 0)
		{
			return;
		}
		List<Action<T1>> list = PopList();
		list.Clear();
		list.AddRange(mActionSet);
		foreach (Action<T1> item in list)
		{
			try
			{
				item(param1);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		list.Clear();
		PushList(list);
	}

	public static SafeAction<T1> operator +(SafeAction<T1> safeAction, Action<T1> action)
	{
		safeAction.AddListener(action);
		return safeAction;
	}

	public static SafeAction<T1> operator -(SafeAction<T1> safeAction, Action<T1> action)
	{
		safeAction.RemoveListener(action);
		return safeAction;
	}
}
}
