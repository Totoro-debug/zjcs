using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Offline.World.MapGridVisibility
{
    // Local state/resources supply inputs to the complete original shared component.
    public sealed class OfflineMapGridVisibility : IDisposable
    {
        [Serializable] public sealed class Configuration
        { public string manifest; public int mapClassId,viewRange; public int characterClassId = 3001; public int[] size; public Region[] regions; }
        [Serializable] public sealed class Region
        { public int region; public Pos2d[] RegionActivedGrids,RegionExtendVisiableGrids; }
        [Serializable] public sealed class BodyConfiguration
        { public string manifest; public int mapClassId; public BodyTemplate[] templates; public BodyPlacement[] entities; }
        [Serializable] public sealed class BodyTemplate { public int classId; public GridTransformComponentInfo body; }
        [Serializable] public sealed class BodyPlacement
        { public int placementId,classId,campOwner; public PlacementTransform transform; public List<int> campMembers; }
        [Serializable] public sealed class PlacementTransform { public Pos2d Pos; public Direction Dir; public bool FlipX,IgnoreEntitySize; }

        private readonly Configuration configuration;
        private readonly Inputs inputs;
        private readonly Dictionary<int,MapActivedRegionGridInfo> regions;
        private bool disposed;
        public GridVisibilityAgentComponent Component { get; }

        public OfflineMapGridVisibility(Configuration configuration,BodyConfiguration bodies,Pos2d position,
            byte[] savedVisibility,IEnumerable<int> publicRegions,Func<Pos2d,int> regionAt,Func<int,bool> entityActive)
        {
            this.configuration=configuration??throw new ArgumentNullException(nameof(configuration));
            if(string.IsNullOrWhiteSpace(configuration.manifest)||configuration.mapClassId<=0||configuration.size?.Length!=2
                ||configuration.size[0]<=0||configuration.size[1]<=0||(long)configuration.size[0]*configuration.size[1]>int.MaxValue
                ||configuration.viewRange<0||configuration.characterClassId<=0||configuration.regions==null
                ||bodies?.manifest!=configuration.manifest||bodies.mapClassId!=configuration.mapClassId
                ||bodies.templates==null||bodies.entities==null)
                throw new InvalidDataException("Official map visibility/body configuration is unbound.");
            if(savedVisibility!=null&&savedVisibility.Length>0&&!IsValidSnapshot(savedVisibility,configuration.size[0],configuration.size[1]))
                throw new InvalidDataException("Map visibility save differs from the configured dimensions or contains an invalid run.");
            regions=configuration.regions.ToDictionary(r=>r.region,r=>new MapActivedRegionGridInfo
                {RegionActivedGrids=r.RegionActivedGrids,RegionExtendVisiableGrids=r.RegionExtendVisiableGrids});
            inputs=new Inputs(configuration,bodies,position,regionAt,entityActive);
            // Confirmed offline inference: seed a new map from the local player
            // and activated goddesses. Never change the original header-only codec.
            if(savedVisibility==null||savedVisibility.Length==0)
            {
                var initial=new MapGroundGridBoolData(configuration.size[1],configuration.size[0]);
                foreach(var point in VisibilityRangeQuery.GetAllVisiblePosEnumerator(inputs.Ground,position,configuration.viewRange))
                    initial.SetPosValue(point.X,point.Y,true);
                foreach(int region in publicRegions??Array.Empty<int>())
                    foreach(var point in RegionPositions(region))
                        if(inputs.Ground.InGround(point))initial.SetPosValue(point.X,point.Y,true);
                savedVisibility=GridVisibilityCompressUtils.DoMyZip(initial);
            }
            Component=new GridVisibilityAgentComponent{Host=inputs,ZipedData=(byte[])savedVisibility.Clone()};
            Component.OnAfterDeserialize();
            if(Component.Width!=configuration.size[0]||Component.Height!=configuration.size[1])
                throw new InvalidDataException("Map visibility save dimensions differ from the original map.");
            Component.Start();
        }
        // Keep the official height/width header and header-only all-visible branch.
        // Validate before invoking its recovering decoder: a corrupt local save must
        // not silently become a partially explored map.
        public static bool IsValidSnapshot(byte[] bytes,int width,int height)
        {
            if(bytes==null||bytes.Length<8||bytes.Length>65536||(bytes.Length-8)%8!=0
                ||width<=0||height<=0||BitConverter.ToInt32(bytes,0)!=height||BitConverter.ToInt32(bytes,4)!=width)return false;
            long cells=(long)width*height;
            for(int i=8;i<bytes.Length;i+=8)
            {
                int start=BitConverter.ToInt32(bytes,i),count=BitConverter.ToInt32(bytes,i+4);
                if(start<0||count<0||(long)start+count>cells)return false;
            }
            return true;
        }
        public bool IsVisible(Pos2d position)=>Component.GetPosVisibility(position);
        public void MovePlayer(Pos2d position)
        {
            if(disposed)throw new ObjectDisposedException(nameof(OfflineMapGridVisibility));
            var grid=inputs.Player.Grid;var previous=grid.Pos;
            if(previous==position)return;
            grid.Pos=position;grid.PosUpdatedEvent.FireEvent(grid,previous);
        }
        public IEnumerable<Pos2d> RegionPositions(int region)=>new MapActivedRegionGridInfoEnumerator(regions.TryGetValue(region,out var info)?info:null);
        public void UnlockRegion(int region)
        {
            foreach(var point in RegionPositions(region))Component.SetPosVisibility(point,true);
        }
        public void SetEntityPosition(int placementId,Pos2d position)
        {
            if(!inputs.Entities.TryGetValue(placementId,out var entity))return;
            inputs.Ground.Remove(entity);entity.SetPosition(position);inputs.Ground.Add(entity);
        }
        public void NotifyEntityStarted(int placementId,bool newEntity)
        { if(inputs.Entities.TryGetValue(placementId,out var entity))inputs.ContainerValue.StartEntityEvent.FireEvent(entity,newEntity); }
        public byte[] Snapshot(){Component.OnBeforeSerialize();return (byte[])Component.ZipedData.Clone();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            inputs.Map.NotifyCharacterRemoved(inputs.Character);inputs.Map.MapCharacterDict.Clear();Component.End(null);
        }
        private sealed class Inputs:GridVisibilitySourceContext
        {
            public readonly ContainerInput ContainerValue;public readonly GroundInput Ground;public readonly MapComponent Map=new MapComponent();
            public readonly AreaInput Area;public readonly EntityInput Root,Player;public readonly CharacterInput Character;
            public readonly Dictionary<int,EntityInput> Entities=new Dictionary<int,EntityInput>();
            private readonly Dictionary<int,BodyTemplate> templates;private readonly int viewRange; public readonly int MapClassId;
            public Inputs(Configuration config,BodyConfiguration bodies,Pos2d position,Func<Pos2d,int> regionAt,Func<int,bool> active)
            {
                MapClassId=config.mapClassId;viewRange=config.viewRange;ContainerValue=new ContainerInput(this);Ground=new GroundInput(config.size,active);Area=new AreaInput(MapClassId,regionAt);
                templates=bodies.templates.ToDictionary(t=>t.classId);
                foreach(var template in templates.Values)template.body.Init();
                Root=new EntityInput(this,0,MapClassId,null,null);Player=new EntityInput(this,-1,config.characterClassId,templates[config.characterClassId].body,null);Player.SetPosition(position);
                Character=new CharacterInput(Player,config.viewRange);Map.MapCharacterDict.Add(1,Character);
                foreach(var placement in bodies.entities)
                {
                    templates.TryGetValue(placement.classId,out var template);
                    var entity=new EntityInput(this,placement.placementId,placement.classId,template?.body,placement);
                    entity.SetPosition(placement.transform.Pos);Entities.Add(placement.placementId,entity);Ground.Add(entity);
                }
            }
            public override VisibilityEntity Entity=>Root;public override EcContainer Container=>ContainerValue;
            public override long? MainPlayerId=>1;public override IMapCharacter MainMapCharacter=>Character;
            public override List<Pos2d> CalcAllVisiblePos(GridGroundComponent ground,Pos2d position,int range)=>VisibilityRangeQuery.CalcAllVisiblePos(ground,position,range);
            public override bool TryGetCampOwnerEntity(VisibilityEntity entity,out VisibilityEntity owner)
            {
                // EntitySource ownership resolves back to the original placed
                // LoEntityCampMonsterComponent, then to its camp placement.
                owner=null;
                if(!(entity is EntityInput input)||input.Placement?.campOwner==null||input.Placement.campOwner==0)return false;
                if(Entities.TryGetValue(input.Placement.campOwner,out var camp))owner=camp;
                return owner!=null;
            }
            public override HashSet<Pos2d> GetLoEntityIncludePosList(EcContainer container,List<int> identifiers)
            {
                // Original EntityHandler.GetLoEntityIncludePosList iteration;
                // use default placement positions and common prefab body info.
                var result=new HashSet<Pos2d>();
                foreach(int id in identifiers)
                {
                    if(!Entities.TryGetValue(id,out var entity))throw new InvalidDataException("Original map camp member is missing: "+id);
                    entity.Body?.GetBodyAddOneRange(entity.Placement.transform.Pos,result);
                }
                return result;
            }
            public override int GetSpawnCharacterViewRange(int mapClassId)=>mapClassId==MapClassId?viewRange:throw new InvalidDataException("Unexpected visibility map.");
        }
        private sealed class ContainerInput:EcContainer
        { private readonly Inputs inputs;public ContainerInput(Inputs inputs){this.inputs=inputs;}public override object Id=>inputs.MapClassId;public override int ClassId=>inputs.MapClassId;public override VisibilityEntity RootEntity=>inputs.Root; }
        private sealed class EntityInput:VisibilityEntity
        {
            private readonly Inputs inputs;private readonly int id,classId;public readonly GridInput Grid;public readonly GridTransformComponentInfo Body;public readonly BodyPlacement Placement;
            public EntityInput(Inputs inputs,int id,int classId,GridTransformComponentInfo body,BodyPlacement placement)
            {
                this.inputs=inputs;this.id=id;this.classId=classId;Body=body;Placement=placement;
                Grid=new GridInput(this){GridGround=inputs.Ground,Info=new VisibilityBodyInfo{UnlockWholeRequestCount=body?.UnlockWholeRequestCount??1},RangeList=new List<Pos2d>(),AddOneRangeList=new HashSet<Pos2d>()};
            }
            public void SetPosition(Pos2d position)
            {
                Grid.Pos=position;Grid.RangeList.Clear();
                if(Body==null)return;
                foreach(var body in Body.BodyRangeInfos)Grid.RangeList.Add(VisibilityBodyGeometry.CalcLocalToWorldPos(body.Pos,position,Body.RealCenter,Placement?.transform.Dir??Direction.Up,Placement?.transform.FlipX??false));
                Grid.AddOneRangeList=new HashSet<Pos2d>(Body.GetBodyAddOneRange(position));
            }
            public override int ClassId=>classId;public override object Id=>id;public override EcContainer Container=>inputs.ContainerValue;public override GridTransformComponent GridTransform=>Body==null?null:Grid;
            public override T GetComponent<T>()
            {
                object value=typeof(T)==typeof(MapComponent)?inputs.Map:typeof(T)==typeof(GridGroundComponent)?inputs.Ground:typeof(T)==typeof(AreaAgentComponent)?inputs.Area:typeof(T)==typeof(IMapCharacter)&&id==-1?inputs.Character:typeof(T)==typeof(LoEntityCampComponent)&&Placement?.campMembers!=null?new LoEntityCampComponent{Info=new LoEntityCampComponentInfo{CampMonsterEntities=Placement.campMembers}}:null;
                return value==null?default:(T)value;
            }
        }
        private sealed class GridInput:GridTransformComponent
        { private readonly EntityInput entity;public GridInput(EntityInput entity){this.entity=entity;}public override T GetComponent<T>()=>entity.GetComponent<T>(); }
        private sealed class CharacterInput:IMapCharacter
        { public CharacterInput(EntityInput entity,int range){Entity=entity;ViewRange=range;}public VisibilityEntity Entity{get;}public GridTransformComponent GridTransform=>Entity.GridTransform;public VisibilityCharacterData Character{get;}=new VisibilityCharacterData{PlayerId=1};public int ViewRange{get;} }
        private sealed class AreaInput:AreaAgentComponent
        { private readonly int mapId;private readonly Func<Pos2d,int> regionAt;public AreaInput(int mapId,Func<Pos2d,int> regionAt){this.mapId=mapId;this.regionAt=regionAt??throw new ArgumentNullException(nameof(regionAt));}public override int GridToRegion(int mapClassId,Pos2d position)=>mapClassId==mapId?regionAt(position):-1; }
        private sealed class GroundInput:GridGroundComponent
        {
            private readonly int[] size;private readonly Func<int,bool> active;private readonly Dictionary<Pos2d,List<EntityInput>> cells=new Dictionary<Pos2d,List<EntityInput>>();
            public GroundInput(int[] size,Func<int,bool> active){this.size=size;this.active=active??throw new ArgumentNullException(nameof(active));}
            public override DefaultAStarMovableHandler CheckStandHandler{get;}=new DefaultAStarMovableHandler();
            public override bool InGround(Pos2d p)=>p.X>=0&&p.Y>=0&&p.X<size[0]&&p.Y<size[1];
            public void Add(EntityInput entity){foreach(var p in entity.Grid.RangeList){if(!cells.TryGetValue(p,out var list))cells.Add(p,list=new List<EntityInput>());list.Add(entity);}}
            public void Remove(EntityInput entity){foreach(var p in entity.Grid.RangeList)if(cells.TryGetValue(p,out var list)){list.Remove(entity);if(list.Count==0)cells.Remove(p);}}
            public override GamePosData GetPosData(Pos2d p)=>cells.TryGetValue(p,out var list)?new CellInput(list,active):null;
        }
        private sealed class CellInput:GamePosData
        { private readonly List<EntityInput> entities;private readonly Func<int,bool> active;public CellInput(List<EntityInput> entities,Func<int,bool> active){this.entities=entities;this.active=active;}public override bool ExistEntitys()=>Entitys.Any();public override IEnumerable<VisibilityEntity> Entitys=>entities.Where(e=>active(e.Placement.placementId)); }
    }
}
