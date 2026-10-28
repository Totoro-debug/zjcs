using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Zjcs.Official.EntityAwardHook;
using Zjcs.Official.StoryPlayables;
using Zjcs.Offline.World.NativeMap14SharedEntities;
namespace Zjcs.Offline.World.NativeMap14EntityAwardHook
{
    // Adapts an existing original manager. Its unchanged IL owns handler
    // ordering/history/replay. This bridge never applies inventory rewards.
    public sealed class NativeMap14EntityAwardManager : IEntityAwardManager, IDisposable
    {
        public object Original { get; }
        private readonly Type messageType, mapSourceType, delegateType, priorityType;
        private readonly Dictionary<object, EntityAwardMessageData> messages = new Dictionary<object, EntityAwardMessageData>(IdentityComparer.Instance);
        private readonly Dictionary<Func<EntityAwardMessageData, bool>, Handler> handlers = new Dictionary<Func<EntityAwardMessageData, bool>, Handler>();
        private bool disposed;
        public NativeMap14EntityAwardManager(NativeMap14SharedAssemblies shared, object original)
        {
            if (shared == null) throw new ArgumentNullException(nameof(shared));
            Original = original ?? throw new ArgumentNullException(nameof(original));
            var managerType = shared.Shared.GetType("AwardManager", true);
            if (!managerType.IsInstanceOfType(original)) throw new ArgumentException("Original Console AwardManager is required.");
            messageType = shared.CommonType("PlayerAwardMsg"); mapSourceType = shared.CommonType("AsMapEntity");
            delegateType = managerType.GetNestedType("AwardDel"); priorityType = managerType.GetNestedType("AwardHandlerPriority");
        }
        public EntityAwardMessageData Wrap(object original)
        {
            RequireActive();
            if (original == null) return null;
            if (!messageType.IsInstanceOfType(original)) throw new ArgumentException("Original PlayerAwardMsg is required.");
            if (!messages.TryGetValue(original, out var result))
            { result = new Message(original, mapSourceType); messages.Add(original, result); }
            return result;
        }
        public void RegisterHandler(Func<EntityAwardMessageData, bool> callback, EntityAwardHandlerPriority priority)
        {
            RequireActive();
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (!handlers.TryGetValue(callback, out var handler))
            {
                handler = new Handler(this, callback);
                handler.Native = Delegate.CreateDelegate(delegateType, handler, typeof(Handler).GetMethod(nameof(Handler.Invoke)));
                handlers.Add(callback, handler);
            }
            // The original RegisterHandler can synchronously replay history.
            // Count before calling so callbacks observe a registered lifetime.
            handler.Count++;
            Original.GetType().GetMethod("RegisterHandler").Invoke(Original, new object[] { handler.Native, Enum.ToObject(priorityType, (int)priority) });
        }
        public void RemoveHandler(Func<EntityAwardMessageData, bool> callback)
        {
            RequireActive();
            if (!handlers.TryGetValue(callback, out var handler)) return;
            Original.GetType().GetMethod("RemoveHandler").Invoke(Original, new object[] { handler.Native });
            handler.Count--;
            if (handler.Count == 0) handlers.Remove(callback);
        }
        public void ShowAward(EntityAwardMessageData message)
        {
            RequireActive();
            var original = message?.Original;
            if (original != null && !messageType.IsInstanceOfType(original)) throw new ArgumentException("Original PlayerAwardMsg is required.");
            Original.GetType().GetMethod("ShowAward").Invoke(Original, new[] { original });
        }
        public void Dispose()
        {
            if (disposed) return;
            foreach (var handler in handlers.Values)
                for (int index = 0; index < handler.Count; index++)
                    Original.GetType().GetMethod("RemoveHandler").Invoke(Original, new object[] { handler.Native });
            handlers.Clear(); messages.Clear(); disposed = true;
        }
        private void RequireActive() { if (disposed) throw new ObjectDisposedException(nameof(NativeMap14EntityAwardManager)); }
        private sealed class Handler
        {
            private readonly NativeMap14EntityAwardManager owner;
            private readonly Func<EntityAwardMessageData, bool> callback;
            public Delegate Native; public int Count;
            public Handler(NativeMap14EntityAwardManager owner, Func<EntityAwardMessageData, bool> callback) { this.owner = owner; this.callback = callback; }
            public bool Invoke(object message) => callback(owner.Wrap(message));
        }
        private sealed class Message : EntityAwardMessageData
        {
            public override object Original { get; }
            private readonly Type mapSourceType;
            private object previousSource, mappedSource;
            public Message(object original, Type mapSourceType) { Original = original; this.mapSourceType = mapSourceType; }
            public override object SourceData
            {
                get
                {
                    var source = NativeMap14SharedPrefabs.Get(Original, "SourceData");
                    if (!ReferenceEquals(source, previousSource))
                    { previousSource = source; mappedSource = mapSourceType.IsInstanceOfType(source) ? new MapSource(source) : source; }
                    return mappedSource;
                }
            }
        }
        private sealed class MapSource : EntityMapAwardSourceData
        {
            public override object Original { get; }
            public MapSource(object original) { Original = original; }
            public override EcEntityIdentifier EntityId
            {
                get { var id = NativeMap14SharedPrefabs.Get(Original, "EntityId");
                    return new EcEntityIdentifier((long)NativeMap14SharedPrefabs.Get(id, "ContainerId"), (int)NativeMap14SharedPrefabs.Get(id, "EntityId")); }
            }
        }
        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            public static readonly IdentityComparer Instance = new IdentityComparer();
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
