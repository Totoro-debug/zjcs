// Migrated complete original application buffer source; see Map14 buffered-event receipt.
using System;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;
using System;
using System.Collections.Generic;

namespace Zjcs.Official.EntityBufferedEvents
{
public class EcEntityBufferEventHandleModule : IEcEntityBufferEventHandleModule, IDisposable
{
	private readonly struct UnsubscribeEventData { public readonly IEcEntityBufferEventHandle Handle; public UnsubscribeEventData(IEcEntityBufferEventHandle handle){Handle=handle;} }

	private interface IEcEntityBufferEventHandle : IDisposable
	{
		long Signature { get; }

		void Invoke(IEcEntityBufferEventContext ecEntityBufferEventContext);
	}

	private class EcEntityBufferEventHandle<T> : IEcEntityBufferEventHandle, IDisposable where T : IEcEntityBufferEventContext
	{
		private BufferEventChannel<UnsubscribeEventData> mUnsubscribeEventPublisher;

		private Action<T> mCallback;

		public long Signature { get; private set; }

		public EcEntityBufferEventHandle(BufferEventChannel<UnsubscribeEventData> unsubscribeEventPublisher, long signature, Action<T> callback)
		{
			mUnsubscribeEventPublisher = unsubscribeEventPublisher;
			Signature = signature;
			mCallback = callback;
		}

		void IEcEntityBufferEventHandle.Invoke(IEcEntityBufferEventContext ecEntityBufferEventContext)
		{
			try
			{
				mCallback((T)ecEntityBufferEventContext);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}

		void IDisposable.Dispose()
		{
			if (mUnsubscribeEventPublisher != null)
			{
				mUnsubscribeEventPublisher.Publish(new UnsubscribeEventData(this));
				mUnsubscribeEventPublisher = null;
			}
			mCallback = null;
			Signature = 0L;
		}
	}

	private Dictionary<Type, Type> mDispatcherTypes;

	private Dictionary<Type, Queue<IEcEntityBufferEventDispatcher>> mDispatcherPool;

	private Dictionary<long, IEcEntityBufferEventDispatcher> mDispatchers;

	private Dictionary<long, IEcEntityBufferEventContext> mBufferEventContexts;

	private HashSet<IEcEntityBufferEventHandle> mEcEntityBufferEventHandles;

	private IDisposable unsubscribeSubscription,contextSubscription;

    private BufferEventChannel<UnsubscribeEventData> UnsubscribeEventPublisher { get; } = new BufferEventChannel<UnsubscribeEventData>();
    private BufferEventChannel<UnsubscribeEventData> UnsubscribeEventSubscriber => UnsubscribeEventPublisher;
    private BufferEventChannel<IEcEntityBufferEventDispatcher.EcEntityBufferEventContextEventData> EcEntityBufferEventContextEventPublisher { get; } = new BufferEventChannel<IEcEntityBufferEventDispatcher.EcEntityBufferEventContextEventData>();
    private BufferEventChannel<IEcEntityBufferEventDispatcher.EcEntityBufferEventContextEventData> EcEntityBufferEventContextEventSubscriber => EcEntityBufferEventContextEventPublisher;

    public IBufferEventMapInputs Inputs { get; set; }
    private bool initialized;
    public bool Active { get; private set; }
    public void Initialize()
    {
        if(initialized)return;initialized=true;
        try{InitializeCore();}catch(Exception error){UnityEngine.Debug.LogException(error);}
    }
    public void Dispose()
    {
        if(!initialized)return;
        try{DisposeCore();}catch(Exception error){UnityEngine.Debug.LogException(error);}
        initialized=false;
    }
    public void Activate(){Active=true;try{OnActivate();}catch(Exception error){UnityEngine.Debug.LogException(error);}}
    public void Deactivate(){Active=false;}

	private void InitializeCore()
	{
		mDispatcherTypes = new Dictionary<Type,Type>{{Inputs.PlayerInteractionType,typeof(EntityPlayerInteractionBufferEventDispatcher)},{Inputs.WorldStageType,typeof(WorldStageBufferEventDispatcher)}};
		mDispatcherPool = new Dictionary<Type, Queue<IEcEntityBufferEventDispatcher>>();
		foreach (KeyValuePair<Type, Type> mDispatcherType in mDispatcherTypes)
		{
			mDispatcherPool.Add(mDispatcherType.Key, new Queue<IEcEntityBufferEventDispatcher>());
		}
		mDispatchers = new Dictionary<long, IEcEntityBufferEventDispatcher>();
		mBufferEventContexts = new Dictionary<long, IEcEntityBufferEventContext>();
		mEcEntityBufferEventHandles = new HashSet<IEcEntityBufferEventHandle>();
		unsubscribeSubscription = UnsubscribeEventSubscriber.Subscribe(OnUnsubscribe);
		contextSubscription = EcEntityBufferEventContextEventSubscriber.Subscribe(OnEcEntityBufferEventContextCallback);
		Inputs.AddEntityEvent += Container_OnEntityAdd;
		Inputs.RemoveEntityEvent += Container_OnEntityRemove;
		BufferContainerData mapContainer = Inputs.MapContainer;
		if (mapContainer == null)
		{
			Inputs.OnContainerUpdate += new Action<BufferContainerData, BufferContainerData>(OnContainerUpdate);
		}
		else
		{
			OnContainerUpdate(null, mapContainer);
		}
	}

	private void DisposeCore()
	{
		unsubscribeSubscription.Dispose();
		contextSubscription.Dispose();
		if (Inputs != null)
		{
			Inputs.OnContainerUpdate -= new Action<BufferContainerData, BufferContainerData>(OnContainerUpdate);
			Inputs.AddEntityEvent -= Container_OnEntityAdd;
			Inputs.RemoveEntityEvent -= Container_OnEntityRemove;
		}
		foreach (KeyValuePair<Type, Queue<IEcEntityBufferEventDispatcher>> item in mDispatcherPool)
		{
			foreach (IEcEntityBufferEventDispatcher item2 in item.Value)
			{
				item2.Dispose();
			}
		}
		foreach (KeyValuePair<long, IEcEntityBufferEventDispatcher> mDispatcher in mDispatchers)
		{
			mDispatcher.Value.Dispose();
		}
		foreach (IEcEntityBufferEventHandle mEcEntityBufferEventHandle in mEcEntityBufferEventHandles)
		{
			mEcEntityBufferEventHandle.Dispose();
		}
		mDispatchers.Clear();
		mBufferEventContexts.Clear();
		mEcEntityBufferEventHandles.Clear();
		mDispatcherPool.Clear();
	}

	private void OnActivate()
	{
		foreach (IEcEntityBufferEventHandle mEcEntityBufferEventHandle in mEcEntityBufferEventHandles)
		{
			if (mBufferEventContexts.TryGetValue(mEcEntityBufferEventHandle.Signature, out var value))
			{
				mEcEntityBufferEventHandle.Invoke(value);
			}
		}
		mBufferEventContexts.Clear();
	}

	private void OnContainerUpdate(BufferContainerData preContainer, BufferContainerData container)
	{
		Inputs.OnContainerUpdate -= new Action<BufferContainerData, BufferContainerData>(OnContainerUpdate);
		if (container != null)
		{
			foreach (BufferEntityData value in container.EntityDict.Values)
			{
				Container_OnEntityAdd(value);
			}
		}
		if (preContainer == null)
		{
			return;
		}
		foreach (BufferEntityData value2 in preContainer.EntityDict.Values)
		{
			Container_OnEntityRemove(value2, (object)null);
		}
	}

	private void OnEcEntityBufferEventContextCallback(IEcEntityBufferEventDispatcher.EcEntityBufferEventContextEventData data)
	{
		long subscriptionSignature = GetSubscriptionSignature(data.EcComponent.Entity.Id, data.EcEntityBufferEventContext.GetType());
		mBufferEventContexts[subscriptionSignature] = data.EcEntityBufferEventContext;
		Dispatch(subscriptionSignature);
	}

	private void Dispatch(long signature)
	{
		IEcEntityBufferEventContext ecEntityBufferEventContext = default;
		if (!Active || !mBufferEventContexts.Remove(signature, out ecEntityBufferEventContext))
		{
			return;
		}
		foreach (IEcEntityBufferEventHandle mEcEntityBufferEventHandle in mEcEntityBufferEventHandles)
		{
			if (mEcEntityBufferEventHandle.Signature == signature)
			{
				mEcEntityBufferEventHandle.Invoke(ecEntityBufferEventContext);
			}
		}
	}

	IDisposable IEcEntityBufferEventHandleModule.Subscribe<T>(ECViewEntityData entity, Action<T> callback)
	{
		if (entity == null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return null;
		}
		if (callback == null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return null;
		}
		long subscriptionSignature = GetSubscriptionSignature(entity.Id, typeof(T));
		EcEntityBufferEventHandle<T> ecEntityBufferEventHandle = new EcEntityBufferEventHandle<T>(UnsubscribeEventPublisher, subscriptionSignature, callback);
		mEcEntityBufferEventHandles.Add(ecEntityBufferEventHandle);
		Dispatch(subscriptionSignature);
		return ecEntityBufferEventHandle;
	}

	private void OnUnsubscribe(UnsubscribeEventData data)
	{
		mEcEntityBufferEventHandles.Remove(data.Handle);
	}

	public void Container_OnEntityAdd(BufferEntityData entity)
	{
		foreach (BufferComponentData component in entity.ComponentList)
		{
			Type type = component.ComponentType;
			IEcEntityBufferEventDispatcher ecEntityBufferEventDispatcher = Acquire(type);
			if (ecEntityBufferEventDispatcher != null)
			{
				ecEntityBufferEventDispatcher.Load(component);
				mDispatchers.Add(GetSubscriptionSignature(entity.Id, type), ecEntityBufferEventDispatcher);
			}
		}
	}

	public void Container_OnEntityRemove(BufferEntityData entity, object data)
	{
		IEcEntityBufferEventDispatcher ecEntityBufferEventDispatcher = default;
		foreach (BufferComponentData component in entity.ComponentList)
		{
			Type type = component.ComponentType;
			long subscriptionSignature = GetSubscriptionSignature(entity.Id, type);
			if (mDispatchers.Remove(subscriptionSignature, out ecEntityBufferEventDispatcher))
			{
				ecEntityBufferEventDispatcher.Unload();
				Recycle(type, ecEntityBufferEventDispatcher);
			}
		}
	}

	private IEcEntityBufferEventDispatcher Acquire(Type type)
	{
		if (!mDispatcherPool.TryGetValue(type, out var value))
		{
			return null;
		}
		IEcEntityBufferEventDispatcher ecEntityBufferEventDispatcher = default;
		if (!value.TryDequeue(out ecEntityBufferEventDispatcher))
		{
			ecEntityBufferEventDispatcher = Activator.CreateInstance(mDispatcherTypes[type]) as IEcEntityBufferEventDispatcher;
			ecEntityBufferEventDispatcher.Initialize(EcEntityBufferEventContextEventPublisher);
		}
		return ecEntityBufferEventDispatcher;
	}

	private void Recycle(Type type, IEcEntityBufferEventDispatcher dispatcher)
	{
		if (mDispatcherPool.TryGetValue(type, out var value))
		{
			value.Enqueue(dispatcher);
		}
	}

	private long GetSubscriptionSignature(EcEntityIdentifier ecEntityIdentifier, Type contextType)
	{
		return ((long)ecEntityIdentifier.GetHashCode() << 32) | (uint)contextType.GetHashCode();
	}
}

}
