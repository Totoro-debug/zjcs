using System;
using System.Collections;
using System.Collections.Generic;
using Zjcs.Official.EntityBufferedEvents;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Offline.World.NativeMap14ClientEntities;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;

namespace Zjcs.Offline.World.NativeMap14BufferedEvents
{
    public sealed class NativeMap14BufferEventInputs:IBufferEventMapInputs,IDisposable
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly NativeMap14ClientEntityState state;
        private readonly Dictionary<NativeMap14SharedViewEntity,Entity> entities=new Dictionary<NativeMap14SharedViewEntity,Entity>();
        private readonly IDisposable added,removed;
        public Type WorldStageType { get; }
        public Type PlayerInteractionType { get; }
        public BufferContainerData MapContainer { get; }
        public event Action<BufferEntityData> AddEntityEvent;
        public event Action<BufferEntityData,object> RemoveEntityEvent;
        // One adapter owns one actual map container for its lifetime. A new map
        // creates a new scope; the original module still consumes this input.
        public event Action<BufferContainerData,BufferContainerData> OnContainerUpdate { add{} remove{} }
        public NativeMap14BufferEventInputs(NativeMap14SharedAssemblies shared,NativeMap14SharedRoot root,NativeMap14ClientEntityState state)
        {
            this.shared=shared??throw new ArgumentNullException(nameof(shared));this.state=state??throw new ArgumentNullException(nameof(state));
            if(root==null)throw new ArgumentNullException(nameof(root));
            WorldStageType=shared.CommonType("WorldStageComponent");PlayerInteractionType=shared.CommonType("EntityPlayerInteractionComponent");
            MapContainer=new Container(this);
            added=new NativeMap14SharedEventSubscription(NativeMap14SharedPrefabs.Get(root.Container,"AddEntityEvent"),shared.CommonType("IEcContainerAddEntityEventReceiver"),args=>AddEntityEvent?.Invoke(Wrap(state.Wrap(args[0]))));
            try{removed=new NativeMap14SharedEventSubscription(NativeMap14SharedPrefabs.Get(root.Container,"RemoveEntityEvent"),shared.CommonType("IEcContainerRemoveEntityEventReceiver"),args=>RemoveEntityEvent?.Invoke(Wrap(state.Wrap(args[0])),args[1]));}
            catch{added.Dispose();throw;}
        }
        private Entity Wrap(NativeMap14SharedViewEntity view)
        {
            if(!entities.TryGetValue(view,out var entity))entities.Add(view,entity=new Entity(this,view));return entity;
        }
        public void Dispose(){removed.Dispose();added.Dispose();AddEntityEvent=null;RemoveEntityEvent=null;entities.Clear();}
        private sealed class Container:BufferContainerData
        {
            private readonly NativeMap14BufferEventInputs owner;public Container(NativeMap14BufferEventInputs owner){this.owner=owner;}
            public override IReadOnlyDictionary<EcEntityIdentifier,BufferEntityData> EntityDict
            {
                get{var result=new Dictionary<EcEntityIdentifier,BufferEntityData>();foreach(var pair in owner.state.EntityDict)result.Add(pair.Key,owner.Wrap((NativeMap14SharedViewEntity)pair.Value));return result;}
            }
        }
        private sealed class Entity:BufferEntityData
        {
            private readonly NativeMap14BufferEventInputs owner;private readonly NativeMap14SharedViewEntity view;
            private readonly Dictionary<object,Component> components=new Dictionary<object,Component>();
            public Entity(NativeMap14BufferEventInputs owner,NativeMap14SharedViewEntity view){this.owner=owner;this.view=view;}
            public override EcEntityIdentifier Id=>view.Id;
            public override IEnumerable<BufferComponentData> ComponentList
            {
                get
                {
                    foreach(var original in (IEnumerable)NativeMap14SharedPrefabs.Get(view.Original,"ComponentList"))
                    {if(!components.TryGetValue(original,out var value))components.Add(original,value=new Component(owner,original));yield return value;}
                }
            }
        }
        private sealed class Component:BufferComponentData
        {
            private readonly NativeMap14BufferEventInputs owner;private readonly object original;
            public Component(NativeMap14BufferEventInputs owner,object original){this.owner=owner;this.original=original;}
            public override ECViewEntityData Entity=>owner.state.Wrap(NativeMap14SharedPrefabs.Get(original,"Entity"));
            public override Type ComponentType=>original.GetType();
            public override IDisposable SubscribeStage(Action<ECWorldStageStateData,int,object> callback)
            {
                var view=(NativeMap14SharedViewEntity)Entity;
                if(view.Stage==null||!ReferenceEquals(view.Stage.Original,original))throw new InvalidOperationException("Original buffer stage owner differs.");
                return view.Stage.Subscribe(value=>callback(value.StageComp,value.PreState,value.Param));
            }
            public override IDisposable SubscribePlayerRemove(Action<BufferComponentData,long> callback)=>
                new NativeMap14SharedEventSubscription(NativeMap14SharedPrefabs.Get(original,"OnInteractingPlayerRemove"),owner.shared.CommonType("IEcEntityPlayerInteractionRemoveEventReceiver"),args=>
                {if(!ReferenceEquals(args[0],original))throw new InvalidOperationException("Original player interaction event owner differs.");callback(this,(long)args[1]);});
        }
    }
}
