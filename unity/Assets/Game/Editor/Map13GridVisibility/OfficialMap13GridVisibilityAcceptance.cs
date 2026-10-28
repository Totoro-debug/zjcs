using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor.Map13GridVisibility
{
    public static class OfficialMap13GridVisibilityAcceptance
    {
        public static void Run()
        {
            var data=JsonUtility.FromJson<Config>(Resources.Load<TextAsset>("Map13/grid-visibility").text);
            Require(data.manifest=="871_156598"&&data.mapClassId==13&&data.regions.Length==106&&data.viewRange==5&&data.size.SequenceEqual(new[]{240,240}),"Original Map13 visibility configuration differs.");
            int regionEntries=0;
            foreach(var region in data.regions)
            {
                var info=new MapActivedRegionGridInfo{RegionActivedGrids=region.RegionActivedGrids,RegionExtendVisiableGrids=region.RegionExtendVisiableGrids};
                var actual=new MapActivedRegionGridInfoEnumerator(info).ToArray();
                Require(actual.SequenceEqual((info.RegionActivedGrids??Array.Empty<Pos2d>()).Concat(info.RegionExtendVisiableGrids??Array.Empty<Pos2d>())) ,"Original activation enumeration order differs.");regionEntries+=actual.Length;
            }
            Require(!new MapActivedRegionGridInfoEnumerator(null).Any(),"Original missing region must enumerate no cells.");
            var bits=new MapGroundGridBoolData(2,3);bits.SetPosValue(1,0,true);bits.SetPosValue(2,0,true);bits.SetPosValue(0,1,true);
            Require(GridVisibilityCompressUtils.DoMyZip(bits).SequenceEqual(Ints(2,3,1,3)),"Original row-spanning visibility run bytes differ.");
            var round=GridVisibilityCompressUtils.DoMyUnzip(Ints(2,3,1,3));
            for(int i=0;i<6;i++)Require(round.GetPosValue(i%3,i/3)==(i>=1&&i<=3),"Original compressed visibility restore differs.");
            var empty=new MapGroundGridBoolData(2,3);Require(GridVisibilityCompressUtils.DoMyZip(empty).SequenceEqual(Ints(2,3)),"Original empty compression header differs.");
            var headerOnly=GridVisibilityCompressUtils.DoMyUnzip(Ints(2,3));for(int i=0;i<6;i++)Require(headerOnly.GetPosValue(i%3,i/3),"Original header-only decode must preserve its all-visible branch.");
            bool threw=false;try{bits.GetPosValue(-1,0);}catch(ArgumentOutOfRangeException e){threw=e.ParamName=="x";}Require(threw,"Original negative coordinate guard differs.");
            var host=new Context();var component=new GridVisibilityAgentComponent{Host=host,ZipedData=Ints(240,240,0,0)};host.Component=component;component.OnAfterDeserialize();
            Require(component.Width==240&&component.Height==240&&component.ZipedData==null&&!component.GetPosVisibility(18,18),"Original explicit zero-length run restore differs.");
            var range=VisibilityRangeQuery.CalcAllVisiblePos(host.Ground,new Pos2d(18,18),1);
            Require(range.SequenceEqual(new[]{new Pos2d(18,18),new Pos2d(18,17),new Pos2d(17,18),new Pos2d(18,19),new Pos2d(19,18)}),"Original visible diamond order differs.");
            var changed=new List<Pos2d>();var single=new List<Pos2d>();int multi=0;
            component.OnPosVisibilityUpdate+=(p,before,after,region)=>{Require(!before&&after&&!region,"Original visibility event payload differs.");changed.Add(p);};
            component.OnOneCellUnlock+=(p,animate)=>{Require(animate,"Original character sight unlock must animate.");single.Add(p);};
            component.Handle.OnMultiCellsUnlock+=(positions,animate)=>{Require(animate,"Original entity multi-cell event must animate.");multi++;};
            host.Map.MapCharacterDict.Add(1,host.Character);host.Map.MapCharacterDict.Add(2,host.OtherCharacter);component.Start();
            Require(changed.Count==61&&single.Count==61&&!component.GetPosVisibility(190,190),"Original character-owner filter or radius-five sight differs.");
            Require(host.Ground.CheckStandHandler.CanStand(new Pos2d(18,18))&&!host.Ground.CheckStandHandler.CanStand(new Pos2d(200,200)),"Original appended movement visibility predicate differs.");
            host.Player.Transform.Pos=new Pos2d(19,18);host.Player.Transform.PosUpdatedEvent.FireEvent(host.Player.Transform,new Pos2d(18,18));
            Require(changed.Count==72&&single.Count==72&&component.GetPosVisibility(13,18),"Original enter sight should expand permanently without hiding previous cells.");
            var body=host.AddEntity(3,new Pos2d(80,80));body.Transform.RangeList=new List<Pos2d>{new Pos2d(80,80)};body.Transform.AddOneRangeList=new HashSet<Pos2d>{new Pos2d(80,80),new Pos2d(81,80),new Pos2d(80,81)};
            host.ContainerValue.StartEntityEvent.FireEvent(body,false);Require(multi==0&&!component.GetPosVisibility(80,80),"Original restored-entity start must not unlock new cells.");
            host.ContainerValue.StartEntityEvent.FireEvent(body,true);Require(multi==1&&changed.Count==75&&single.Count==72,"Original body unlock/event channels differ.");
            var foreign=host.AddEntity(4,new Pos2d(200,80));host.ContainerValue.StartEntityEvent.FireEvent(foreign,true);Require(multi==2&&!component.GetPosVisibility(200,80),"Original owner-region filter differs.");
            var camp=host.AddEntity(5,new Pos2d(70,70));host.CampOwner=host.AddEntity(6,new Pos2d(71,70));host.CampEntity=camp;
            host.ContainerValue.StartEntityEvent.FireEvent(camp,true);Require(component.GetPosVisibility(70,70)&&component.GetPosVisibility(71,70)&&multi==3,"Original camp body branch differs.");
            component.OnBeforeSerialize();Require(component.ZipedData!=null,"Original serialization callback did not snapshot visibility.");
            var loaded=new GridVisibilityAgentComponent{Host=host,ZipedData=component.ZipedData};loaded.OnAfterDeserialize();
            for(int y=0;y<240;y++)for(int x=0;x<240;x++)Require(loaded.GetPosVisibility(x,y)==component.GetPosVisibility(x,y),"Original save visibility round trip differs.");
            // The original map removes characters before ending this component.
            // End itself only removes map/container/ground subscriptions.
            host.Map.NotifyCharacterRemoved(host.Character);
            int previous=changed.Count;component.End(null);host.Player.Transform.Pos=new Pos2d(100,100);host.Player.Transform.PosUpdatedEvent.FireEvent(host.Player.Transform,new Pos2d(19,18));host.ContainerValue.StartEntityEvent.FireEvent(body,true);
            Require(changed.Count==previous&&multi==3&&!host.Ground.CheckStandHandler.CanStand(new Pos2d(18,18)),"Original end retained grid/container/predicate listeners.");
            var output=Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidDataException("Explicit validation directory required.");
            File.WriteAllText(Path.Combine(output,"grid-visibility-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,regions=106,region_entries=regionEntries,roundtrip_cells=57600,owner_sight_cells=72,shared_component_lifecycle=true},true));
            Debug.Log("Map13 original visibility rules passed: "+regionEntries+" region entries, exact RLE/header branch, owner sight, body/camp regions, lifecycle and 57,600 saved cells.");
        }
        private static byte[] Ints(params int[] values)=>values.SelectMany(BitConverter.GetBytes).ToArray();
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        private sealed class Context:GridVisibilitySourceContext
        {
            public readonly Container ContainerValue=new Container();public readonly Ground Ground=new Ground();public readonly MapComponent Map=new MapComponent();public readonly Area Area=new Area();
            public readonly Entity Root,Player,Other;public readonly CharacterInput Character,OtherCharacter;public GridVisibilityAgentComponent Component;public Entity CampEntity,CampOwner;
            public Context(){ContainerValue.Context=this;Root=new Entity(this,0,new Pos2d());Player=AddEntity(1,new Pos2d(18,18));Other=AddEntity(2,new Pos2d(190,190));Character=new CharacterInput(Player,1);OtherCharacter=new CharacterInput(Other,2);}
            public Entity AddEntity(int id,Pos2d position)=>new Entity(this,id,position);
            public override VisibilityEntity Entity=>Root;public override EcContainer Container=>ContainerValue;public override long? MainPlayerId=>1;public override IMapCharacter MainMapCharacter=>Character;
            public override List<Pos2d> CalcAllVisiblePos(GridGroundComponent ground,Pos2d position,int range)=>VisibilityRangeQuery.CalcAllVisiblePos(ground,position,range);
            public override bool TryGetCampOwnerEntity(VisibilityEntity entity,out VisibilityEntity owner){owner=ReferenceEquals(entity,CampEntity)?CampOwner:null;return owner!=null;}
            public override HashSet<Pos2d> GetLoEntityIncludePosList(EcContainer container,List<int> entities)=>new HashSet<Pos2d>{new Pos2d(70,70),new Pos2d(71,70)};
            public override int GetSpawnCharacterViewRange(int mapClassId)=>5;
        }
        private sealed class Container:EcContainer{public Context Context;public override object Id=>13;public override int ClassId=>13;public override VisibilityEntity RootEntity=>Context.Root;}
        private sealed class Entity:VisibilityEntity
        {
            private readonly Context context;private readonly int identity;public readonly Grid Transform;public Entity(Context context,int id,Pos2d position){this.context=context;identity=id;Transform=new Grid{Owner=this,Pos=position,GridGround=context.Ground,Info=new VisibilityBodyInfo{UnlockWholeRequestCount=1},RangeList=new List<Pos2d>{position},AddOneRangeList=new HashSet<Pos2d>{position}};}
            public override int ClassId=>8363;public override object Id=>identity;public override EcContainer Container=>context.Container;public override GridTransformComponent GridTransform=>Transform;
            public override T GetComponent<T>(){object value=typeof(T)==typeof(MapComponent)?context.Map:typeof(T)==typeof(GridGroundComponent)?context.Ground:typeof(T)==typeof(AreaAgentComponent)?context.Area:typeof(T)==typeof(LoEntityCampComponent)?new LoEntityCampComponent{Info=new LoEntityCampComponentInfo{CampMonsterEntities=new List<int>{5,6}}}:typeof(T)==typeof(IMapCharacter)?identity==1?context.Character:context.OtherCharacter:null;return value==null?default:(T)value;}
        }
        private sealed class Grid:GridTransformComponent{public Entity Owner;public override T GetComponent<T>()=>Owner.GetComponent<T>();}
        private sealed class Ground:GridGroundComponent{private readonly DefaultAStarMovableHandler stand=new DefaultAStarMovableHandler{CanStand=_=>false};public override DefaultAStarMovableHandler CheckStandHandler=>stand;public override GamePosData GetPosData(Pos2d position)=>null;public override bool InGround(Pos2d position)=>position.X>=0&&position.Y>=0&&position.X<240&&position.Y<240;}
        private sealed class Area:AreaAgentComponent{public override int GridToRegion(int mapClassId,Pos2d position)=>position.X<120?0:1;}
        private sealed class CharacterInput:IMapCharacter{public CharacterInput(Entity entity,long id){Entity=entity;Character=new VisibilityCharacterData{PlayerId=id};}public VisibilityEntity Entity{get;}public GridTransformComponent GridTransform=>Entity.GridTransform;public VisibilityCharacterData Character{get;}public int ViewRange=>5;}
        [Serializable]private sealed class Config{public string manifest;public int mapClassId,viewRange;public int[] size;public Region[] regions;}
        [Serializable]private sealed class Region{public int region;public Pos2d[] RegionActivedGrids,RegionExtendVisiableGrids;}
        [Serializable]private sealed class Proof{public string editor;public int regions,region_entries,roundtrip_cells,owner_sight_cells;public bool shared_component_lifecycle;}
    }
}
