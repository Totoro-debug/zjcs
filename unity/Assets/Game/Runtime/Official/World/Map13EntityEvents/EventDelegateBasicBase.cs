// Migrated original entity event dispatch; see Map13 source receipt.
using System;
using System.Collections.Generic;

namespace Zjcs.Official.World.Map13EntityEvents
{

public abstract class EventDelegateBasicBase<TEventDelegate, TReceiver> : IEventDelegate where TEventDelegate : EventDelegateBasicBase<TEventDelegate, TReceiver> where TReceiver : IEventReceiver
{
	private List<TReceiver> mRecordEventReceivers = new List<TReceiver>();

	[ThreadStatic]
	private static Stack<List<TReceiver>> sCacheStack;

	public static Stack<List<TReceiver>> CacheStack
	{
		get
		{
			if (sCacheStack == null)
			{
				sCacheStack = new Stack<List<TReceiver>>();
			}
			return sCacheStack;
		}
	}

	protected void RegisterEventReceiver(TReceiver receiver)
	{
		mRecordEventReceivers.Add(receiver);
	}

	protected void UnRegisterEventReceiver(TReceiver receiver)
	{
		mRecordEventReceivers.Remove(receiver);
	}

	protected List<TReceiver> OnBeganFireEventReceivers()
	{
		List<TReceiver> list = ((CacheStack.Count <= 0) ? new List<TReceiver>() : sCacheStack.Pop());
		int count = mRecordEventReceivers.Count;
		for (int i = 0; i < count; i++)
		{
			list.Add(mRecordEventReceivers[i]);
		}
		return list;
	}

	protected void OnEndedFireEventReceivers(List<TReceiver> tempReceivers)
	{
		tempReceivers.Clear();
		CacheStack.Push(tempReceivers);
	}

	public static TEventDelegate operator +(EventDelegateBasicBase<TEventDelegate, TReceiver> eventDelegate, TReceiver receiver)
	{
		if (eventDelegate != null)
		{
			eventDelegate.RegisterEventReceiver(receiver);
			return eventDelegate as TEventDelegate;
		}
		UnityEngine.Debug.LogError("EventDelegate.operator+ -> eventDelegate is null!");
		return null;
	}

	public static TEventDelegate operator -(EventDelegateBasicBase<TEventDelegate, TReceiver> eventDelegate, TReceiver receiver)
	{
		if (eventDelegate != null)
		{
			eventDelegate.UnRegisterEventReceiver(receiver);
			return eventDelegate as TEventDelegate;
		}
		UnityEngine.Debug.LogError("EventDelegate.operator- -> eventDelegate is null!");
		return null;
	}

	public void Clear()
	{
		mRecordEventReceivers.Clear();
	}

	public void Merge(TEventDelegate eventDelegate)
	{
		foreach (TReceiver mRecordEventReceiver in eventDelegate.mRecordEventReceivers)
		{
			RegisterEventReceiver(mRecordEventReceiver);
		}
	}
}
}
