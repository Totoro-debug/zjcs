// Migrated original entity event dispatch; see Map13 source receipt.
using System;
using System.Collections.Generic;

namespace Zjcs.Official.World.Map13EntityEvents
{

public abstract class EventDelegateBase<TEventDelegate, TReceiver> : EventDelegateBasicBase<TEventDelegate, TReceiver> where TEventDelegate : EventDelegateBase<TEventDelegate, TReceiver> where TReceiver : IEventReceiver
{
	public virtual void FireEvent()
	{
		List<TReceiver> list = OnBeganFireEventReceivers();
		int count = list.Count;
		for (int i = 0; i < count; i++)
		{
			try
			{
				FireOnReceiver(list[i]);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		OnEndedFireEventReceivers(list);
	}

	protected abstract void FireOnReceiver(TReceiver receiver);

	protected void RegisterEvent<TTempReceiver>(Dictionary<Delegate, TTempReceiver> tempReceiverDict, Action action, TTempReceiver receiver) where TTempReceiver : TReceiver
	{
		if (!tempReceiverDict.ContainsKey(action))
		{
			tempReceiverDict.Add(action, receiver);
			RegisterEventReceiver((TReceiver)(object)receiver);
			return;
		}
		UnityEngine.Debug.LogError(".operator+ => action = ");
	}

	protected void UnRegisterEvent<TTempReceiver>(Dictionary<Delegate, TTempReceiver> tempReceiverDict, Action action) where TTempReceiver : TReceiver
	{
		if (tempReceiverDict.TryGetValue(action, out var value))
		{
			UnRegisterEventReceiver((TReceiver)(object)value);
			return;
		}
		UnityEngine.Debug.LogError(".operator- => action = ");
	}
}
public abstract class EventDelegateBase<TEventDelegate, TReceiver, TArg1> : EventDelegateBasicBase<TEventDelegate, TReceiver> where TEventDelegate : EventDelegateBase<TEventDelegate, TReceiver, TArg1> where TReceiver : IEventReceiver
{
	public virtual void FireEvent(TArg1 p1)
	{
		List<TReceiver> list = OnBeganFireEventReceivers();
		int count = list.Count;
		for (int i = 0; i < count; i++)
		{
			try
			{
				FireOnReceiver(list[i], p1);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		OnEndedFireEventReceivers(list);
	}

	protected abstract void FireOnReceiver(TReceiver receiver, TArg1 p1);

	protected void RegisterEvent<TTempReceiver>(Dictionary<Delegate, TTempReceiver> tempReceiverDict, Action<TArg1> action, TTempReceiver receiver) where TTempReceiver : TReceiver
	{
		if (!tempReceiverDict.ContainsKey(action))
		{
			tempReceiverDict.Add(action, receiver);
			RegisterEventReceiver((TReceiver)(object)receiver);
			return;
		}
		UnityEngine.Debug.LogError(".operator+ => action = ");
	}

	protected void UnRegisterEvent<TTempReceiver>(Dictionary<Delegate, TTempReceiver> tempReceiverDict, Action<TArg1> action) where TTempReceiver : TReceiver
	{
		if (tempReceiverDict.TryGetValue(action, out var value))
		{
			UnRegisterEventReceiver((TReceiver)(object)value);
			return;
		}
		UnityEngine.Debug.LogError(".operator- => action = ");
	}
}
public abstract class EventDelegateBase<TEventDelegate, TReceiver, TArg1, TArg2> : EventDelegateBasicBase<TEventDelegate, TReceiver> where TEventDelegate : EventDelegateBase<TEventDelegate, TReceiver, TArg1, TArg2> where TReceiver : IEventReceiver
{
	public virtual void FireEvent(TArg1 p1, TArg2 p2)
	{
		List<TReceiver> list = OnBeganFireEventReceivers();
		int count = list.Count;
		for (int i = 0; i < count; i++)
		{
			try
			{
				FireOnReceiver(list[i], p1, p2);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		OnEndedFireEventReceivers(list);
	}

	protected abstract void FireOnReceiver(TReceiver receiver, TArg1 p1, TArg2 p2);

	protected void RegisterEvent<TTempReceiver>(Dictionary<Delegate, TTempReceiver> tempReceiverDict, Action<TArg1, TArg2> action, TTempReceiver receiver) where TTempReceiver : TReceiver
	{
		if (!tempReceiverDict.ContainsKey(action))
		{
			tempReceiverDict.Add(action, receiver);
			RegisterEventReceiver((TReceiver)(object)receiver);
			return;
		}
		UnityEngine.Debug.LogError(".operator+ => action = ");
	}

	protected void UnRegisterEvent<TTempReceiver>(Dictionary<Delegate, TTempReceiver> tempReceiverDict, Action<TArg1, TArg2> action) where TTempReceiver : TReceiver
	{
		if (tempReceiverDict.TryGetValue(action, out var value))
		{
			UnRegisterEventReceiver((TReceiver)(object)value);
			return;
		}
		UnityEngine.Debug.LogError(".operator- => action = ");
	}
}
public abstract class EventDelegateBase<TEventDelegate, TReceiver, TArg1, TArg2, TArg3> : EventDelegateBasicBase<TEventDelegate, TReceiver> where TEventDelegate : EventDelegateBase<TEventDelegate, TReceiver, TArg1, TArg2, TArg3> where TReceiver : IEventReceiver
{
	public virtual void FireEvent(TArg1 p1, TArg2 p2, TArg3 p3)
	{
		List<TReceiver> list = OnBeganFireEventReceivers();
		int count = list.Count;
		for (int i = 0; i < count; i++)
		{
			try
			{
				FireOnReceiver(list[i], p1, p2, p3);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		OnEndedFireEventReceivers(list);
	}

	protected abstract void FireOnReceiver(TReceiver receiver, TArg1 p1, TArg2 p2, TArg3 p3);

	protected void RegisterEvent<TTempReceiver>(Dictionary<Delegate, TTempReceiver> tempReceiverDict, Action<TArg1, TArg2, TArg3> action, TTempReceiver receiver) where TTempReceiver : TReceiver
	{
		if (!tempReceiverDict.ContainsKey(action))
		{
			tempReceiverDict.Add(action, receiver);
			RegisterEventReceiver((TReceiver)(object)receiver);
			return;
		}
		UnityEngine.Debug.LogError(".operator+ => action = ");
	}

	protected void UnRegisterEvent<TTempReceiver>(Dictionary<Delegate, TTempReceiver> tempReceiverDict, Action<TArg1, TArg2, TArg3> action) where TTempReceiver : TReceiver
	{
		if (tempReceiverDict.TryGetValue(action, out var value))
		{
			UnRegisterEventReceiver((TReceiver)(object)value);
			return;
		}
		UnityEngine.Debug.LogError(".operator- => action = ");
	}
}
public abstract class EventDelegateBase<TEventDelegate, TReceiver, TArg1, TArg2, TArg3, TArg4> : EventDelegateBasicBase<TEventDelegate, TReceiver> where TEventDelegate : EventDelegateBase<TEventDelegate, TReceiver, TArg1, TArg2, TArg3, TArg4> where TReceiver : IEventReceiver
{
	public virtual void FireEvent(TArg1 p1, TArg2 p2, TArg3 p3, TArg4 p4)
	{
		List<TReceiver> list = OnBeganFireEventReceivers();
		int count = list.Count;
		for (int i = 0; i < count; i++)
		{
			try
			{
				FireOnReceiver(list[i], p1, p2, p3, p4);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		OnEndedFireEventReceivers(list);
	}

	protected abstract void FireOnReceiver(TReceiver receiver, TArg1 p1, TArg2 p2, TArg3 p3, TArg4 p4);

	protected void RegisterEvent<TTempReceiver>(Dictionary<Delegate, TTempReceiver> tempReceiverDict, Action<TArg1, TArg2, TArg3, TArg4> action, TTempReceiver receiver) where TTempReceiver : TReceiver
	{
		if (!tempReceiverDict.ContainsKey(action))
		{
			tempReceiverDict.Add(action, receiver);
			RegisterEventReceiver((TReceiver)(object)receiver);
			return;
		}
		UnityEngine.Debug.LogError(".operator+ => action = ");
	}

	protected void UnRegisterEvent<TTempReceiver>(Dictionary<Delegate, TTempReceiver> tempReceiverDict, Action<TArg1, TArg2, TArg3, TArg4> action) where TTempReceiver : TReceiver
	{
		if (tempReceiverDict.TryGetValue(action, out var value))
		{
			UnRegisterEventReceiver((TReceiver)(object)value);
			return;
		}
		UnityEngine.Debug.LogError(".operator- => action = ");
	}
}
}
