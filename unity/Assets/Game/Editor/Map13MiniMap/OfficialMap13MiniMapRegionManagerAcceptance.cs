using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.Map13MiniMap
{
    public static class OfficialMap13MiniMapRegionManagerAcceptance
    {
        private const string Key="Zjcs.Map13.MiniMapRegionManager.Running";
        private static IEnumerator routine;private static int errors;
        [Serializable] internal sealed class GuidData{public string mAssetGuid;}
        [Serializable] internal sealed class SourceRegion{public Pos2d RegionMinPos,RegionMaxPos;public GuidData RegionTilemapAssetGuidProxy;}
        [Serializable] private sealed class Fields{public SourceRegion[] mRegionTilemapDatas;}
        [Serializable] private sealed class SourceComponent{public string type;public Fields fields;}
        [Serializable] private sealed class Sources{public SourceComponent[] components;}
        [Serializable] private sealed class Region{public int region;public string path;}
        [Serializable] private sealed class Assets{public Region[] regions;}
        [Serializable] private sealed class Proof{public string editor;public int regions,initial_loads,final_loads,disposed_leases;public bool original_reverse_load_order,original_public_z,cache_reuse,ten_second_delay,delay_reset,active_preserved,controller_reentry,other_map_unsubscribed,cancel_on_destroy;}
        private static string Repository=>Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private static void Require(bool condition,string detail){if(!condition)throw new InvalidDataException(detail);}
        private static void Observe(string condition,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private sealed class Controller:IMiniMapCharacterController{public MyGridTransform MyGridTransform=>null;}
        private sealed class Characters:IMiniMapCharacterManager
        {
            public IMiniMapCharacterController MyMeUiCharacter{get;private set;}=new Controller();
            public event Action<IMiniMapCharacterController,IMiniMapCharacterController> OnMeMapControllerUpdated;
            public void Switch(IMiniMapCharacterController value){var before=MyMeUiCharacter;MyMeUiCharacter=value;OnMeMapControllerUpdated?.Invoke(before,value);}
        }
        internal sealed class Range:IMapViewRangeHandler
        {
            public readonly HashSet<Pos2d> Visible=new HashSet<Pos2d>();
            public GAction<IMapViewRangeHandler> OnVisibleRangeUpdate{get;set;}=new GAction<IMapViewRangeHandler>();
            public IReadOnlyList<Pos2d> VisiblePosList=>null;
            public bool IsRangeInViewRange(Pos2d min,Pos2d max)=>Visible.Contains(min);
            public bool IsEntityInViewRange(IMiniMapEntityBody body)=>IsPosInViewRange(body.Pos);
            public bool IsPosInViewRange(Pos2d pos)=>Visible.Contains(pos);
            public void Dispose(){}
        }
        private sealed class Lease:IMiniMapRegionAssetLease{public GameObject Result{get;set;}public Action Release;public void Dispose(){Release();}}
        public sealed class Context:MiniMapRegionContext
        {
            public readonly Dictionary<string,GameObject> Prefabs=new Dictionary<string,GameObject>();
            public readonly List<string> Loads=new List<string>();public readonly List<GameObject> Instances=new List<GameObject>();
            internal readonly Range Range=new Range();public int Disposed,Current=13,StatusSubscribers;
            public List<MapRegionStatus> Statuses;
            public override int MapClassId=>13;public override int CurrentMapClassId=>Current;public override bool HasMainPlayer=>true;
            public override IMapViewRangeHandler ViewRangeHandler=>Range;
            public override event Action<IMapViewRangeHandler> OnVisibleRangeUpdate;
            private Action<int,int,MapRegionStatus,MapRegionStatus> status;
            public override event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate{add{status+=value;StatusSubscribers++;}remove{status-=value;StatusSubscribers--;}}
            public override List<MapRegionStatus> GetMapStatusListInTeam(int id)=>Statuses;
            public override bool IsRangeInViewRange(Pos2d min,Pos2d max)=>Range.IsRangeInViewRange(min,max);
            public override IMiniMapRegionAssetLease LoadAssetByGuid(string guid){Loads.Add(guid);return new Lease{Result=Prefabs[guid],Release=()=>Disposed++};}
            public override GameObject InstantiateRegion(GameObject prefab,Transform parent,bool worldPositionStays){var go=Object.Instantiate(prefab,parent,worldPositionStays);Instances.Add(go);return go;}
            internal void View(params SourceRegion[] regions){Range.Visible.Clear();foreach(var r in regions)Range.Visible.Add(r.RegionMinPos);OnVisibleRangeUpdate?.Invoke(Range);}
            public void Status(int map,int region,MapRegionStatus value){status?.Invoke(map,region,MapRegionStatus.Private,value);}
        }
        private static MiniMapPrivateRegionMaskManager Make(Context context,Characters characters,SourceRegion[] source,out Transform[] slots)
        {
            var root=new GameObject("OriginalRegionManagerDiagnostic",typeof(Grid));root.SetActive(false);slots=new Transform[source.Length];var rows=new MiniMapPrivateRegionMaskManager.RegionTilemapData[source.Length];
            for(int i=0;i<rows.Length;i++)
            {
                var slot=new GameObject("RegionGround_"+i+"_Slot").transform;slot.SetParent(root.transform,false);slots[i]=slot;
                var proxy=JsonUtility.FromJson<AssetGuidProxy<GameObject>>(JsonUtility.ToJson(source[i].RegionTilemapAssetGuidProxy));
                rows[i]=new MiniMapPrivateRegionMaskManager.RegionTilemapData{RegionMinPos=source[i].RegionMinPos,RegionMaxPos=source[i].RegionMaxPos,RegionTilemapAssetGuidProxy=proxy,RegionTilemapSlot=slot};
            }
            var manager=root.AddComponent<MiniMapPrivateRegionMaskManager>();manager.MiniMap=context;manager.MapMyCharacterManager=characters;
            typeof(MiniMapPrivateRegionMaskManager).GetField("mRegionTilemapSlots",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(manager,slots);
            typeof(MiniMapPrivateRegionMaskManager).GetField("mRegionTilemapDatas",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(manager,rows);root.SetActive(true);return manager;
        }
        private static IEnumerator Verify()
        {
            var source=JsonUtility.FromJson<Sources>(File.ReadAllText(Path.Combine(Repository,"unity/Assets/Official/World/Map/13/Source/NativeMiniMapSource/native-minimap-components.json"))).components.Single(c=>c.type=="MiniMapPrivateRegionMaskManager").fields.mRegionTilemapDatas;
            var assets=JsonUtility.FromJson<Assets>(File.ReadAllText(Path.Combine(Repository,"assets/reference/official-map/map13-native-minimap-regions-source-manifest.json"))).regions;
            Require(source.Length==106&&assets.Length==106,"Complete original source region bindings");
            var context=new GameObject("OriginalRegionManagerConsumedServices").AddComponent<Context>();context.Statuses=Enumerable.Repeat(MapRegionStatus.Private,106).ToList();context.Statuses[0]=MapRegionStatus.Public;
            foreach(var r in assets)context.Prefabs.Add(source[r.region].RegionTilemapAssetGuidProxy.mAssetGuid,AssetDatabase.LoadAssetAtPath<GameObject>(r.path.Substring("unity/".Length)));
            context.View(source[0],source[1],source[105]);var characters=new Characters();var manager=Make(context,characters,source,out var slots);yield return null;yield return null;
            Require(context.StatusSubscribers==1&&slots[0].localPosition.z==0&&slots[1].localPosition.z==-1,"Original initialization/public status Z");
            var expected=new[]{105,1,0}.Select(i=>source[i].RegionTilemapAssetGuidProxy.mAssetGuid).ToArray();Require(context.Loads.SequenceEqual(expected),"Original reverse index load order");
            Require(context.Instances.Count==3&&context.Instances.All(g=>g.activeSelf)&&context.Disposed==0,"Original resource lease retention");
            context.Status(12,1,MapRegionStatus.Public);Require(slots[1].localPosition.z==-1,"Other-map status ignored");context.Status(13,1,MapRegionStatus.Public);Require(slots[1].localPosition.z==0,"Original public Z event");context.Status(13,1,MapRegionStatus.Private);Require(slots[1].localPosition.z==-1,"Original private Z event");
            var cached=context.Instances.ToArray();context.View(source[0]);Require(!cached[0].activeSelf&&!cached[1].activeSelf&&cached[2].activeSelf,"Offscreen entries deactivate");
            context.View(source[0],source[1],source[105]);Require(context.Loads.Count==3&&cached.All(g=>g.activeSelf),"Reentry before delay reuses exact objects");
            context.View(source[0]);float start=Time.time;while(Time.time-start<5f)yield return null;
            Require(cached.All(g=>g),"No destruction before original ten seconds");
            context.View(source[0]);start=Time.time;while(Time.time-start<6f)yield return null;
            Require(cached.All(g=>g),"An inactive refresh resets the original clear timer");
            while(Time.time-start<10.3f)yield return null;yield return null;
            Require(!cached[0]&&!cached[1]&&cached[2]&&cached[2].activeSelf&&context.Disposed==0,"Only inactive objects destroyed after exact delay; leases retained");
            context.View(source[0],source[1],source[105]);Require(context.Loads.Count==5,"After cleanup regions reload original assets");
            characters.Switch(null);Require(context.StatusSubscribers==0,"Null controller disables status subscription");characters.Switch(new Controller());Require(context.StatusSubscribers==1,"Controller reentry restores one subscription");
            manager.enabled=false;Require(context.StatusSubscribers==0,"Disable unsubscribes once");context.Current=12;manager.enabled=true;Require(context.StatusSubscribers==0,"Browsing another map does not subscribe to live player status");
            context.View(source[0]);Object.Destroy(manager.gameObject);yield return null;Require(context.Disposed==5,"Destroy releases every retained original asset lease");
            int loads=context.Loads.Count;context.View(source[1]);Require(context.Loads.Count==loads,"Destroyed manager unsubscribes view events");
            start=Time.time;while(Time.time-start<10.3f)yield return null;Require(errors==0,"Destroyed manager must cancel pending cleanup");
            File.WriteAllText(Path.Combine(Workspace,"minimap-region-manager-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,regions=106,initial_loads=3,final_loads=5,disposed_leases=context.Disposed,original_reverse_load_order=true,original_public_z=true,cache_reuse=true,ten_second_delay=true,delay_reset=true,active_preserved=true,controller_reentry=true,other_map_unsubscribed=true,cancel_on_destroy=true},true)+"\n");Object.Destroy(context.gameObject);
            Debug.Log("Map13 original region manager Play Mode passed: all 106 source region/GUID bindings; reverse loads, public/private Z events, inactive reuse, exact ten-second reset/cancel, active preservation, controller lifecycle and all five retained asset leases released. Formal minimap input/save and Windows Player remain pending.");
        }
    }
}
