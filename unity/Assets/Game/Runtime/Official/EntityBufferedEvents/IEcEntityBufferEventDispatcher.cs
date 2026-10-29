// Migrated complete original application buffer source; see Map14 buffered-event receipt.
using System;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;
using System;

namespace Zjcs.Official.EntityBufferedEvents
{
public interface IEcEntityBufferEventDispatcher : IDisposable
{
	public readonly struct EcEntityBufferEventContextEventData { public readonly BufferComponentData EcComponent; public readonly IEcEntityBufferEventContext EcEntityBufferEventContext; public EcEntityBufferEventContextEventData(BufferComponentData component,IEcEntityBufferEventContext context){EcComponent=component;EcEntityBufferEventContext=context;} }

	void Initialize(BufferEventChannel<EcEntityBufferEventContextEventData> EcEntityBufferEventContextEventPublisher);

	void Load(BufferComponentData component);

	void Unload();
}

}
