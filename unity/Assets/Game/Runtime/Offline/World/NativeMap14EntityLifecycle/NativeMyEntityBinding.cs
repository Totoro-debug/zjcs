using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.LegacyWorldStages;
using Zjcs.Official.MapDisplays;
using Zjcs.Official.StoryPlayables;
using Native = Zjcs.Official.EntityLifecycle;

namespace Zjcs.Offline.World.NativeMap14EntityLifecycle
{
    // Interface adapters only. Dispatch, mutation snapshots and exception recovery
    // remain in the original MyEntity delegates and original dispatcher.
    public class NativeMyEntityBinding : ECViewEntityBinding, IECWorldStageReplacementSource,
        ILegacyWorldStageVisibilitySource, IMapDisplayEntityEvents
    {
        [SerializeField] private Native.MyEntity owner;
        private readonly Dictionary<object, Receiver> receivers = new Dictionary<object, Receiver>(new IdentityComparer());
        public Native.MyEntity Owner => owner ? owner : (owner = GetComponent<Native.MyEntity>());
        public override ECViewEntityData Entity => Owner.Entity;
        public IClientVisibilitySupporter ClientVisibilitySupporter => Owner.ClientVisibilitySupporter;
        public override void SetClientVisibilitySupporter(IClientVisibilitySupporter value) => Owner.SetClientVisibilitySupporter(value);

        private Receiver Acquire(object target, int kind)
        {
            if (ReferenceEquals(target, null)) return null;
            if (!receivers.TryGetValue(target, out var receiver)) receivers.Add(target, receiver = new Receiver(this, target));
            receiver.Counts[kind]++; receiver.References++;
            return receiver;
        }
        private Receiver Release(object target, int kind)
        {
            if (ReferenceEquals(target, null)) return null;
            if (!receivers.TryGetValue(target, out var receiver)) return new Receiver(this, target);
            if (receiver.Counts[kind] == 0) return receiver;
            receiver.Counts[kind]--;
            if (--receiver.References == 0) receivers.Remove(target);
            return receiver;
        }
        public override void AddStartReceiver(IMyEntityStartEventReceiver v) => Owner.StartEvent += (Native.IMyEntityStartEventReceiver)Acquire(v, 0);
        public override void RemoveStartReceiver(IMyEntityStartEventReceiver v) => Owner.StartEvent -= (Native.IMyEntityStartEventReceiver)Release(v, 0);
        public override void AddRemoveReceiver(IMyEntityRemoveEventReceiver v) => Owner.RemoveEvent += (Native.IMyEntityRemoveEventReceiver)Acquire(v, 1);
        public override void RemoveRemoveReceiver(IMyEntityRemoveEventReceiver v) => Owner.RemoveEvent -= (Native.IMyEntityRemoveEventReceiver)Release(v, 1);
        public override void AddStartPostReceiver(IMyEntityStartPostEventReceiver v) => Owner.StartPostEvent += (Native.IMyEntityStartPostEventReceiver)Acquire(v, 2);
        public override void RemoveStartPostReceiver(IMyEntityStartPostEventReceiver v) => Owner.StartPostEvent -= (Native.IMyEntityStartPostEventReceiver)Release(v, 2);
        public override void AddRemovePreReceiver(IMyEntityRemovePreEventReceiver v) => Owner.RemovePreEvent += (Native.IMyEntityRemovePreEventReceiver)Acquire(v, 3);
        public override void RemoveRemovePreReceiver(IMyEntityRemovePreEventReceiver v) => Owner.RemovePreEvent -= (Native.IMyEntityRemovePreEventReceiver)Release(v, 3);
        public void AddReplaceReceiver(IMyEntityReplaceEventReceiver v) => Owner.ReplaceEvent += (Native.IMyEntityReplaceEventReceiver)Acquire(v, 4);
        public void RemoveReplaceReceiver(IMyEntityReplaceEventReceiver v) => Owner.ReplaceEvent -= (Native.IMyEntityReplaceEventReceiver)Release(v, 4);
        public void AddBindPreReceiver(Zjcs.Official.MapDisplays.IMyEntityBindPreEventReceiver v) => Owner.BindPreEvent += (Native.IMyEntityBindPreEventReceiver)Acquire(v, 5);
        public void RemoveBindPreReceiver(Zjcs.Official.MapDisplays.IMyEntityBindPreEventReceiver v) => Owner.BindPreEvent -= (Native.IMyEntityBindPreEventReceiver)Release(v, 5);

        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
        private sealed class Receiver : Native.IMyEntityStartEventReceiver, Native.IMyEntityStartPostEventReceiver,
            Native.IMyEntityRemoveEventReceiver, Native.IMyEntityRemovePreEventReceiver,
            Native.IMyEntityReplaceEventReceiver, Native.IMyEntityBindPreEventReceiver
        {
            private readonly NativeMyEntityBinding binding;
            private readonly object target;
            public int References;
            public readonly int[] Counts = new int[6];
            public Receiver(NativeMyEntityBinding binding, object target) { this.binding = binding; this.target = target; }
            public void MyEntity_OnStart(Native.MyEntity entity, bool isNew) => ((IMyEntityStartEventReceiver)target).MyEntity_OnStart(binding, isNew);
            public void MyEntity_OnStartPost(Native.MyEntity entity) => ((IMyEntityStartPostEventReceiver)target).MyEntity_OnStartPost(binding);
            public void MyEntity_OnRemove(Native.MyEntity entity) => ((IMyEntityRemoveEventReceiver)target).MyEntity_OnRemove(binding);
            public void MyEntity_RemovePre(Native.MyEntity entity) => ((IMyEntityRemovePreEventReceiver)target).MyEntity_RemovePre(binding);
            public void MyEntity_OnReplace(Native.MyEntity entity, EcEntityIdentifier previous) => ((IMyEntityReplaceEventReceiver)target).MyEntity_OnReplace(binding, previous);
            public void MyEntity_OnBindPre(ECViewEntityData entity) => ((Zjcs.Official.MapDisplays.IMyEntityBindPreEventReceiver)target).MyEntity_OnBindPre(entity);
        }
    }
}
