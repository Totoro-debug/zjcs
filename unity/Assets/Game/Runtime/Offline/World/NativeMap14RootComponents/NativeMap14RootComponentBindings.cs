using System;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityRootComponents;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
namespace Zjcs.Offline.World.NativeMap14RootComponents
{
    // No shared values or rules are reconstructed. Each projection retains the
    // original component and returns the original live _Info object.
    public sealed class NativeMap14RootComponentBindings
    {
        private readonly NativeMap14SharedAssemblies shared;
        public NativeMap14RootComponentBindings(NativeMap14SharedAssemblies shared)
        {this.shared=shared??throw new ArgumentNullException(nameof(shared));}
        public void Bind(NativeMap14SharedViewEntity view)
        {
            if(view==null)throw new ArgumentNullException(nameof(view));
            Bind<MapComponentData>(view,"MapComponent",value=>new MapComponentProjection(value));
            Bind<WorldComponentData>(view,"WorldComponent",value=>new WorldComponentProjection(value));
            Bind<MapCharacterSpawnerComponentData>(view,"MapCharacterSpawnerComponent",value=>new MapCharacterSpawnerComponentProjection(value));
            Bind<GridGroundComponentData>(view,"GridGroundComponent",value=>new GridGroundComponentProjection(value));
            Bind<AreaAgentComponentData>(view,"AreaAgentComponent",value=>new AreaAgentComponentProjection(value));
            Bind<EntityVisibilityAgentComponentData>(view,"EntityVisibilityAgentComponent",value=>new EntityVisibilityAgentComponentProjection(value));
            Bind<AreaExploreSpawnerComponentData>(view,"AreaExploreSpawnerComponent",value=>new AreaExploreSpawnerComponentProjection(value));
            Bind<MapStoryEntitySpawnerComponentData>(view,"MapStoryEntitySpawnerComponent",value=>new MapStoryEntitySpawnerComponentProjection(value));
            var ground=view.GetComponent<GridGroundComponentData>();
            if(ground!=null)view.BindAdditionalComponent<EntityGridGroundData>(ground);
        }
        private void Bind<T>(NativeMap14SharedViewEntity view,string name,Func<object,T> create) where T:class,IRootClientComponentData
        {
            var original=view.Original.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(view.Original,new object[]{shared.CommonType(name)});
            if(original==null)return;
            var previous=view.GetComponent<T>();
            if(previous!=null)
            {if(!ReferenceEquals(previous.Original,original))throw new InvalidOperationException("Original map root component identity differs.");return;}
            view.BindAdditionalComponent<T>(create(original));
        }
        private sealed class MapComponentProjection:MapComponentData
        {
            public override object Original {get;}
            public override object ComponentInfo=>NativeMap14SharedPrefabs.Get(Original,"_Info");
            public MapComponentProjection(object original){Original=original;}
        }
        private sealed class WorldComponentProjection:WorldComponentData
        {
            public override object Original {get;}
            public override object ComponentInfo=>NativeMap14SharedPrefabs.Get(Original,"_Info");
            public WorldComponentProjection(object original){Original=original;}
        }
        private sealed class MapCharacterSpawnerComponentProjection:MapCharacterSpawnerComponentData
        {
            public override object Original {get;}
            public override object ComponentInfo=>NativeMap14SharedPrefabs.Get(Original,"_Info");
            public MapCharacterSpawnerComponentProjection(object original){Original=original;}
        }
        private sealed class GridGroundComponentProjection:GridGroundComponentData
        {
            public override object Original {get;}
            public override object ComponentInfo=>NativeMap14SharedPrefabs.Get(Original,"_Info");
            public GridGroundComponentProjection(object original){Original=original;}
        }
        private sealed class AreaAgentComponentProjection:AreaAgentComponentData
        {
            public override object Original {get;}
            public override object ComponentInfo=>NativeMap14SharedPrefabs.Get(Original,"_Info");
            public AreaAgentComponentProjection(object original){Original=original;}
        }
        private sealed class EntityVisibilityAgentComponentProjection:EntityVisibilityAgentComponentData
        {
            public override object Original {get;}
            public override object ComponentInfo=>NativeMap14SharedPrefabs.Get(Original,"_Info");
            public EntityVisibilityAgentComponentProjection(object original){Original=original;}
        }
        private sealed class AreaExploreSpawnerComponentProjection:AreaExploreSpawnerComponentData
        {
            public override object Original {get;}
            public override object ComponentInfo=>NativeMap14SharedPrefabs.Get(Original,"_Info");
            public AreaExploreSpawnerComponentProjection(object original){Original=original;}
        }
        private sealed class MapStoryEntitySpawnerComponentProjection:MapStoryEntitySpawnerComponentData
        {
            public override object Original {get;}
            public override object ComponentInfo=>NativeMap14SharedPrefabs.Get(Original,"_Info");
            public MapStoryEntitySpawnerComponentProjection(object original){Original=original;}
        }
    }
}
