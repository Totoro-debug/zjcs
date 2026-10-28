// Ported from the verified official source; only dependency/serialization adapters differ.
using System;
using System.Collections.Generic;

namespace Zjcs.Official.ProfessionUI
{
public class SafeAction
{
	private readonly HashSet<Action> mActionSet = new HashSet<Action>();

	private readonly Stack<List<Action>> mStack = new Stack<List<Action>>();

	private List<Action> PopList()
	{
		if (mStack.Count > 0)
		{
			return mStack.Pop();
		}
		return new List<Action>();
	}

	private void PushList(List<Action> list)
	{
		mStack.Push(list);
	}

	public void AddListener(Action action)
	{
		mActionSet.Add(action);
	}

	public void RemoveListener(Action action)
	{
		mActionSet.Remove(action);
	}

	public void ClearListener()
	{
		mActionSet.Clear();
	}

	public void Invoke()
	{
		if (mActionSet.Count == 0)
		{
			return;
		}
		List<Action> list = PopList();
		list.Clear();
		list.AddRange(mActionSet);
		foreach (Action item in list)
		{
			try
			{
				item();
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		list.Clear();
		PushList(list);
	}

	public static SafeAction operator +(SafeAction safeAction, Action action)
	{
		safeAction.AddListener(action);
		return safeAction;
	}

	public static SafeAction operator -(SafeAction safeAction, Action action)
	{
		safeAction.RemoveListener(action);
		return safeAction;
	}
}
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
