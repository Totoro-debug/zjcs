using System;
using System.Collections.Generic;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.EntityBufferedEvents
{
    public abstract class BufferComponentData
    {
        public abstract ECViewEntityData Entity { get; }
        public abstract Type ComponentType { get; }
        public abstract IDisposable SubscribeStage(Action<ECWorldStageStateData,int,object> callback);
        public abstract IDisposable SubscribePlayerRemove(Action<BufferComponentData,long> callback);
    }
    public abstract class BufferEntityData
    {
        public abstract EcEntityIdentifier Id { get; }
        public abstract IEnumerable<BufferComponentData> ComponentList { get; }
    }
    public abstract class BufferContainerData
    {
        public abstract IReadOnlyDictionary<EcEntityIdentifier,BufferEntityData> EntityDict { get; }
    }
    public interface IBufferEventMapInputs
    {
        Type WorldStageType { get; }
        Type PlayerInteractionType { get; }
        BufferContainerData MapContainer { get; }
        event Action<BufferEntityData> AddEntityEvent;
        event Action<BufferEntityData,object> RemoveEntityEvent;
        event Action<BufferContainerData,BufferContainerData> OnContainerUpdate;
    }
    // Each original private MessagePipe channel has one module subscriber.
    // This transport preserves synchronous publication and disposable teardown;
    // buffering, subscriber storage, signatures and dispatch remain original code.
    public sealed class BufferEventChannel<T>
    {
        private Action<T> callback;
        public void Publish(T value)=>callback?.Invoke(value);
        public IDisposable Subscribe(Action<T> value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            if(callback!=null)throw new InvalidOperationException("Original private buffer channel is already bound.");
            callback=value;return new Subscription(this,value);
        }
        private sealed class Subscription:IDisposable
        {
            private BufferEventChannel<T> channel;private readonly Action<T> value;
            public Subscription(BufferEventChannel<T> channel,Action<T> value){this.channel=channel;this.value=value;}
            public void Dispose(){if(channel==null)return;if(channel.callback==value)channel.callback=null;channel=null;}
        }
    }
}
