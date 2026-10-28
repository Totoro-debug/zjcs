using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Offline.World.NativeMap13MiniMapResources;
using Object=UnityEngine.Object;
using Status=Zjcs.Official.World.NativeMap13Labels.MapRegionStatus;
using CommonStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;

namespace Zjcs.Editor.Map13MiniMap
{
    public static class OfficialMap13MiniMapResourcePointsAcceptance
    {
        private const string Key="Zjcs.Map13.MiniMapResourcePoints.Running";
        private static IEnumerator routine;private static int errors;
        [Serializable] private sealed class Point{public int Area;public Vector2Int PointPos;}
        [Serializable] private sealed class Group{public int Id;public string Icon;}
        [Serializable] private sealed class Region{public int ClassId;public int RegionId;public int[] Areas;}
        [Serializable] private sealed class ItemData{public int[] RuleIds;}
        [Serializable] private sealed class Item{public int ItemId;public ItemData Data;}
        [Serializable] private sealed class Location{public int Id,Type;public int[] RefreshGroupSet;}
        [Serializable] private sealed class Data{public Group[] Groups;public Item[] ItemLocations;public Location[] WorldLocations;}
        [Serializable] private sealed class RectData{public float x,y,width,height;}
        [Serializable] private sealed class Asset{public string path;}
        [Serializable] private sealed class ExpectedIcon{public string path;public Vector2[] vertices,uv;public int[] triangles;public float ppu;public Vector2 pivot;public RectData rect;public Asset asset;}
        [Serializable] private sealed class Receipt{public ExpectedIcon[] icons,static_sprites;}
        [Serializable] private sealed class Proof{public string editor;public int boards,points,icons,item_lookups,monster_lookups,prefab_loads,sprite_loads,leases_disposed,rendered_pixels;public bool original_prefabs,original_geometry,selection_and_guide,public_only,event_refresh,cached_reentry,custom_icon,asset_lifecycle;}
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private static string Repository=>Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        private static void Require(bool condition,string detail){if(!condition)throw new InvalidDataException(detail);}
        private static SpriteRenderer MarkerRenderer(MiniMapResourcePointTapItem point)=>(SpriteRenderer)typeof(MiniMapResourcePointTapItem).GetField("mSprite",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(point);
        private static void Observe(string condition,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private sealed class Player:IMap13LabelPlayer
        {
            public int Level=>100;public SubRank SubRank=>default;public long CombatRating=>0;
            public event Action<int> OnLevelUpdate;public event Action<Rank> OnPlanesRankUnlock;public event Action<long,long> OnCombatRatingChanged;public event Action<int,List<int>> OnWorldMapProgressUpdate;public event Action<int,int,Status,Status> OnMapStatusUpdate;public event Action<long> OnDebugTimeOffset;
            public Status Current=Status.Public;
            public bool RegionIsActived(int map,int region)=>Current==Status.Public;
            public Status GetMapRegionStatus(int map,int region)=>Current;
            public int GetWorldMapRegionProgress(int map,int region)=>0;
            public void Set(Status value){var previous=Current;Current=value;OnMapStatusUpdate?.Invoke(13,0,previous,value);}
        }
        private sealed class Labels:IMap13LabelServices
        {
            public readonly Player Actor=new Player();public int MapClassId=>13;public object Map{get;}=new object();public event Action Initialized;public IMap13LabelPlayer Player=>Actor;public bool IsTeamMember=>false;public int QueryRegion(Vector3 position)=>0;public MapRegionInfo GetRegion(int map,int region)=>null;public Map13LabelData Data=>null;public CombatRatingConfig Colors=>null;public DateTime Now=>DateTime.UtcNow;public double CalcRemainSecond(DateTime end)=>(end-Now).TotalSeconds;
        }
        private sealed class Observed:MiniMapResourcePointContext,IDisposable
        {
            private readonly LocalMiniMapResourcePointContext inner;public int PrefabLoads,SpriteLoads,Disposed,Created;
            private sealed class Lease<T>:IResourcePointAssetLease<T> where T:UnityEngine.Object
            {
                private readonly IResourcePointAssetLease<T> inner;private readonly Action disposed;private bool released;public T Result=>inner.Result;public Lease(IResourcePointAssetLease<T> inner,Action disposed){this.inner=inner;this.disposed=disposed;}public void Dispose(){Require(!released,"Original lease must release exactly once");released=true;inner.Dispose();disposed();}
            }
            public Observed(LocalMiniMapResourcePointContext value){inner=value;}public int MapClassId=>inner.MapClassId;public bool HasMainPlayer=>inner.HasMainPlayer;public IMap13LabelServices LabelServices=>inner.LabelServices;
            public event Action<int,int,CommonStatus,CommonStatus> OnMapStatusUpdate{add=>inner.OnMapStatusUpdate+=value;remove=>inner.OnMapStatusUpdate-=value;}
            public MiniMapResourcePointEntityGroupInfo GetRefreshGroupInfo(int group)=>inner.GetRefreshGroupInfo(group);public int AreaToRegion(int map,int area)=>inner.AreaToRegion(map,area);public CommonStatus GetMapRegionStatus(int map,int region)=>inner.GetMapRegionStatus(map,region);public MiniMapResourcePointItemLocationData GetItemLocationInfo(int item)=>inner.GetItemLocationInfo(item);public MiniMapResourcePointRefreshGroupInfo GetRefreshGroupsByAwardRule(int rule)=>inner.GetRefreshGroupsByAwardRule(rule);public MiniMapResourcePointRefreshGroupInfo GetRefreshGroupsByMonsterId(int monster)=>inner.GetRefreshGroupsByMonsterId(monster);
            public IResourcePointAssetLease<GameObject> LoadPrefabByGuid(string guid){PrefabLoads++;return new Lease<GameObject>(inner.LoadPrefabByGuid(guid),()=>Disposed++);}public IResourcePointAssetLease<Sprite> LoadSprite(string path){SpriteLoads++;return new Lease<Sprite>(inner.LoadSprite(path),()=>Disposed++);}public GameObject InstantiatePrefab(GameObject prefab,Transform parent,bool world){Created++;return inner.InstantiatePrefab(prefab,parent,world);}public void Dispose(){inner.Dispose();}
        }
        private static IEnumerator Verify()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");Require(pipeline,"Original URP pipeline");GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            var assets=Resources.Load<MiniMapResourcePointAssets>("Map13/Native/minimap-resource-point-assets");Require(assets&&assets.Boards.Count==23&&assets.Icons.Count==23&&assets.SourceData&&assets.ManagerFields,"Complete native resource-board catalog");
            var expected=JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(Repository,"assets/reference/official-map/map13-native-minimap-resource-points-source-manifest.json")));foreach(var e in expected.icons)
            {
                var sprite=assets.Icons.Single(v=>v.Path==e.path).Sprite;Require(sprite&&sprite.vertices.Length==e.vertices.Length&&sprite.triangles.Select(v=>(int)v).SequenceEqual(e.triangles)&&sprite.pixelsPerUnit==e.ppu&&Vector2.Distance(sprite.pivot,e.pivot)<.0001f,"Original dynamic icon geometry: "+e.path);var vertices=sprite.vertices;for(int i=0;i<vertices.Length;i++)Require(Vector2.Distance(vertices[i],e.vertices[i])<.00001f,"Exact original icon vertex: "+e.path);
            }
            foreach(var e in expected.static_sprites)
            {
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(e.asset.path.Substring("unity/".Length));Require(sprite&&sprite.rect==new Rect(e.rect.x,e.rect.y,e.rect.width,e.rect.height)&&sprite.pixelsPerUnit==e.ppu&&Vector2.Distance(sprite.pivot,e.pivot)<.0001f&&sprite.triangles.Select(v=>(int)v).SequenceEqual(e.triangles),"Original static marker logical Sprite fields: "+e.asset.path);var vertices=sprite.vertices;var uv=sprite.uv;Require(vertices.Length==e.vertices.Length&&uv.Length==e.uv.Length,"Original static marker mesh dimensions");for(int i=0;i<vertices.Length;i++)Require(Vector2.Distance(vertices[i],e.vertices[i])<.00001f&&Vector2.Distance(uv[i],e.uv[i])<.00001f,"Original static marker effective Atlas UVs/geometry: "+e.asset.path);
            }
            int nativePoints=0;foreach(var asset in assets.Boards)
            {
                Require(asset.Prefab&&asset.Prefab.GetComponent<MiniMapResourcePointBoard>().RefreshGroup==asset.RefreshGroup,"Original group-to-prefab binding");foreach(var t in asset.Prefab.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Original board scripts: "+t.name);nativePoints+=asset.Prefab.GetComponentsInChildren<MiniMapResourcePointTapItem>(true).Length;
            }
            Require(nativePoints==178,"Complete 178 original resource-point marker components");
            var labels=new Labels();var context=new Observed(new LocalMiniMapResourcePointContext(assets,labels));var host=new GameObject("OriginalMap13ResourcePointManager");host.SetActive(false);Require(assets.ManagerPrefab,"Original extracted manager component prefab");var manager=Object.Instantiate(assets.ManagerPrefab,host.transform,false).GetComponent<MiniMapResourcePointManager>();
            manager.Init(context);host.SetActive(true);yield return null;Require(manager.ForeachLayer().Count()==23,"Original layer dictionary order/count: "+manager.ForeachLayer().Count());
            var data=JsonUtility.FromJson<Data>(assets.SourceData.text);var available=new HashSet<int>(assets.Boards.Select(v=>v.RefreshGroup));var actual=new HashSet<int>();int itemLookups=0,monsterLookups=0;
            foreach(var item in data.ItemLocations)
            {
                if(item.Data==null)continue;var wanted=new HashSet<int>();if(item.Data.RuleIds!=null)foreach(int rule in item.Data.RuleIds){var row=data.WorldLocations.FirstOrDefault(v=>v.Type==2&&v.Id==rule);if(row?.RefreshGroupSet!=null)foreach(int group in row.RefreshGroupSet)if(available.Contains(group))wanted.Add(group);}actual.Add(-1);manager.GetRefreshGroupSetByItemId(item.ItemId,actual);Require(actual.SetEquals(wanted),"Original item rule-to-refresh-group filter: "+item.ItemId);itemLookups++;
            }
            foreach(var location in data.WorldLocations.Where(v=>v.Type==1))
            {actual.Add(-1);manager.GetRefreshGroupSetByMonsterId(location.Id,actual);Require(actual.SetEquals(location.RefreshGroupSet.Where(available.Contains)),"Original monster refresh-group filter: "+location.Id);monsterLookups++;}
            manager.GetRefreshGroupSetByMonsterId(-98765,actual);Require(actual.Count==0,"Original missing monster clears destination set");
            foreach(var board in assets.Boards){manager.Select(board.RefreshGroup);yield return null;yield return null;Require(manager.IsBoardActive(board.RefreshGroup),"Original select: "+board.RefreshGroup);}
            Require(context.PrefabLoads==23&&context.Created==23&&host.GetComponentsInChildren<MiniMapResourcePointTapItem>().Length==178,"All boards loaded once and all original public-region markers visible");
            foreach(var board in host.GetComponentsInChildren<MiniMapResourcePointBoard>())
            {
                string path=data.Groups.Single(v=>v.Id==board.RefreshGroup).Icon;var sprite=assets.Icons.Single(v=>v.Path==path).Sprite;foreach(var point in board.GetComponentsInChildren<MiniMapResourcePointTapItem>())Require(MarkerRenderer(point).sprite==sprite,"Original configured icon binding: "+path);
                var serialized=new SerializedObject(board);var points=serialized.FindProperty("mPointDatas");for(int i=0;i<points.arraySize;i++){var p=points.GetArrayElementAtIndex(i);int area=p.FindPropertyRelative("Area").intValue;var areaData=manager.GetSelectResourcePointDatas(board.RefreshGroup,area);Require(areaData!=null&&areaData.Active&&areaData.Datas.Any(v=>v.Item==p.FindPropertyRelative("Item").objectReferenceValue),"Original area-grouped point identity");}
            }
            labels.Actor.Set(Status.Private);yield return null;Require(host.GetComponentsInChildren<MiniMapResourcePointTapItem>().Length==0,"Private regions hide original resource points");labels.Actor.Set(Status.None);yield return null;Require(host.GetComponentsInChildren<MiniMapResourcePointTapItem>().Length==0,"Unknown regions hide original resource points");labels.Actor.Set(Status.Public);yield return null;Require(host.GetComponentsInChildren<MiniMapResourcePointTapItem>().Length==178,"Public status event restores all points");
            int selected=assets.Boards[0].RefreshGroup;var identity=host.GetComponentsInChildren<MiniMapResourcePointBoard>().Single(v=>v.RefreshGroup==selected);manager.CancelSelect(selected,true);Require(manager.IsBoardActive(selected),"Guide cancel preserves an explicitly selected board");manager.CancelSelect(selected);yield return null;Require(!manager.IsBoardActive(selected),"Explicit cancel hides board");int loads=context.PrefabLoads,spriteLoads=context.SpriteLoads;manager.Select(selected);yield return null;Require(context.PrefabLoads==loads&&context.SpriteLoads==spriteLoads&&host.GetComponentsInChildren<MiniMapResourcePointBoard>().Single(v=>v.RefreshGroup==selected)==identity,"Original board and unchanged-icon cache identity");manager.CancelSelect(selected);yield return null;
            string custom=assets.Icons.First(v=>v.Path!=context.GetRefreshGroupInfo(selected).Icon).Path;var customSprite=assets.Icons.Single(v=>v.Path==custom).Sprite;manager.Select(selected,true,custom);yield return null;Require(identity.GetIconPath()==custom&&identity.GetComponentsInChildren<MiniMapResourcePointTapItem>().All(v=>MarkerRenderer(v).sprite==customSprite),"Original custom icon set before activation");manager.CancelSelect(selected,true);yield return null;Require(!manager.IsBoardActive(selected)&&identity.GetIconPath()==context.GetRefreshGroupInfo(selected).Icon,"Guide selection and cancellation restore configured icon path");manager.Select(selected);yield return null;
            int beforeUnknown=context.PrefabLoads;manager.Select(-98765);manager.CancelSelect(-98765);Require(context.PrefabLoads==beforeUnknown,"Original unknown group does not load a board");
            var grid=new GameObject("OriginalMap13RegionalTilemaps",typeof(Grid));foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Official/World/Map/13/NativeMiniMapRegions"}))Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)),grid.transform,false);
            var renderers=grid.GetComponentsInChildren<TilemapRenderer>();Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);foreach(var r in host.GetComponentsInChildren<SpriteRenderer>())bounds.Encapsulate(r.bounds);
            var camera=new GameObject("DiagnosticOriginalResourceBoardCamera",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.aspect=.75f;camera.orthographicSize=Math.Max(bounds.extents.y,bounds.extents.x/.75f)*1.04f;camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,-250);camera.nearClipPlane=.3f;camera.farClipPlane=500;camera.cullingMask=(1<<16)|(1<<20);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.06f,.06f,1);var target=new RenderTexture(768,1024,24);camera.targetTexture=target;
            for(int i=0;i<8;i++)yield return null;camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(768,1024,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,768,1024),0,0);image.Apply(false,false);RenderTexture.active=old;int pixels=image.GetPixels32().Count(p=>p.r>100&&p.g>50&&p.b<150);Require(pixels>100,"Actual original resource board/icon rendering");File.WriteAllBytes(Path.Combine(Workspace,"minimap-resource-points-render.png"),image.EncodeToPNG());Object.Destroy(image);
            int acquired=context.PrefabLoads+context.SpriteLoads;Object.Destroy(host);yield return null;Require(context.Disposed==acquired,"Original manager and marker leases all released on disable/destruction");labels.Actor.Set(Status.Private);context.Dispose();
            File.WriteAllText(Path.Combine(Workspace,"minimap-resource-points-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,boards=23,points=178,icons=23,item_lookups=itemLookups,monster_lookups=monsterLookups,prefab_loads=context.PrefabLoads,sprite_loads=context.SpriteLoads,leases_disposed=context.Disposed,rendered_pixels=pixels,original_prefabs=true,original_geometry=true,selection_and_guide=true,public_only=true,event_refresh=true,cached_reentry=true,custom_icon=true,asset_lifecycle=true},true)+"\n");camera.targetTexture=null;Object.Destroy(target);Object.Destroy(camera.gameObject);Object.Destroy(grid);yield return null;
            Debug.Log("Map13 original minimap resource-point Play Mode passed: complete 23 boards/178 markers/23 original icon geometries, exact selected local item/monster lookup, original selection versus guide lifecycle, Public-only visibility/events, custom icons/cache and asset release; actual original URP rendering. Formal map mouse/save and Windows Player remain required.");
        }
    }
}
