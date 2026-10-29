// Migrated complete original application buffer source; see Map14 buffered-event receipt.
using System;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;
using System;

namespace Zjcs.Official.EntityBufferedEvents
{
public abstract class EcEntityBufferEventDispatcherBase<T> : IEcEntityBufferEventDispatcher, IDisposable where T : BufferComponentData
{
	private BufferEventChannel<IEcEntityBufferEventDispatcher.EcEntityBufferEventContextEventData> mEcEntityBufferEventContextEventPublisher;

	protected T mEcComponent { get; private set; }

	void IEcEntityBufferEventDispatcher.Initialize(BufferEventChannel<IEcEntityBufferEventDispatcher.EcEntityBufferEventContextEventData> EcEntityBufferEventContextEventPublisher)
	{
		mEcEntityBufferEventContextEventPublisher = EcEntityBufferEventContextEventPublisher;
	}

	void IEcEntityBufferEventDispatcher.Load(BufferComponentData component)
	{
		mEcComponent = (T)component;
		try
		{
			Load();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	void IEcEntityBufferEventDispatcher.Unload()
	{
		try
		{
			Unload();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		mEcComponent = null;
	}

	void IDisposable.Dispose()
	{
		if (mEcComponent != null)
		{
			((IEcEntityBufferEventDispatcher)this).Unload();
		}
		mEcEntityBufferEventContextEventPublisher = null;
	}

	protected virtual void Load()
	{
	}

	protected virtual void Unload()
	{
	}

	protected void Dispatch(IEcEntityBufferEventContext entityEventMessage)
	{
		mEcEntityBufferEventContextEventPublisher.Publish(new IEcEntityBufferEventDispatcher.EcEntityBufferEventContextEventData(mEcComponent, entityEventMessage));
	}
}

}
