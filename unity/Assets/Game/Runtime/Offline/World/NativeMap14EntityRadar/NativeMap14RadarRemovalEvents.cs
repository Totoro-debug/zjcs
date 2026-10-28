using System;
using System.Collections.Generic;
using Zjcs.Official.EntityRadar;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
namespace Zjcs.Offline.World.NativeMap14EntityRadar
{
    // Bind the actual original PlayerMap.RemoveEntityEvent dispatcher. This
    // does not replace removal with destruction or synthesize entity events.
    public sealed class NativeMap14RadarRemovalEvents : IRadarRemovalEvents, IDisposable
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly object dispatcher;
        private readonly Func<object, NativeMap14SharedViewEntity> wrap;
        private readonly List<KeyValuePair<IRadarRemoveEntityReceiver, IDisposable>> subscriptions = new List<KeyValuePair<IRadarRemoveEntityReceiver, IDisposable>>();
        private bool disposed;
        public int ReceiverCount => subscriptions.Count;
        public NativeMap14RadarRemovalEvents(NativeMap14SharedAssemblies shared, object dispatcher, Func<object, NativeMap14SharedViewEntity> wrap)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.wrap = wrap ?? throw new ArgumentNullException(nameof(wrap));
            if (dispatcher.GetType() != shared.CommonType("EcContainerRemoveEntityEventDelegate"))
                throw new ArgumentException("Original entity-removal dispatcher is required.");
        }
        public void Add(IRadarRemoveEntityReceiver receiver)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap14RadarRemovalEvents));
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));
            subscriptions.Add(new KeyValuePair<IRadarRemoveEntityReceiver, IDisposable>(receiver,
                new NativeMap14SharedEventSubscription(dispatcher, shared.CommonType("IEcContainerRemoveEntityEventReceiver"),
                    args => receiver.Container_OnEntityRemove(wrap(args[0]), args[1]))));
        }
        public void Remove(IRadarRemoveEntityReceiver receiver)
        {
            int index = subscriptions.FindIndex(pair => ReferenceEquals(pair.Key, receiver));
            if (index < 0) return;
            subscriptions[index].Value.Dispose(); subscriptions.RemoveAt(index);
        }
        public void Dispose()
        {
            if (disposed) return;
            foreach (var pair in subscriptions) pair.Value.Dispose();
            subscriptions.Clear(); disposed = true;
        }
    }
}
