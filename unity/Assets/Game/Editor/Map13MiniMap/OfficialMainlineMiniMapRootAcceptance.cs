using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Offline.World.MapGridVisibility;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using Object = UnityEngine.Object;
using Status = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
using IGridTransformPosUpdatedEventReceiver = Zjcs.Official.EntityTransformViews.IGridTransformPosUpdatedEventReceiver;

namespace Zjcs.Editor.Map13MiniMap
{
    public sealed class MainlineMiniMapRootStateFixture : Map13ManagedFixture
    {
        internal Map13MaskFixture Mask;
        internal int BoundMap=13,BoundRegionCount=106;
        internal MapRootEntityLiteSingleInfo OtherLite;
        public override int MapClassId => BoundMap;
        internal Dictionary<string,GameObject> RegionalPrefabs;
        public override IMap13LabelServices LabelServices => Mask.LabelServices;
        public override bool HasGridVisibilityAgent => true;
        public override MiniMapVisibilityAgent VisibilityAgent => Mask.Agent;
        public override Pos2d CharacterGridPos => Mask.Position;
        public override event Action<Pos2d,bool,bool> OnPosVisibilityUpdate
        { add => Mask.OnPosVisibilityUpdate += value; remove => Mask.OnPosVisibilityUpdate -= value; }
        public override bool TryGetMapLiteData(int map,out MapRootEntityLiteSingleInfo info)
        { if(OtherLite.MapClassId!=0&&map==OtherLite.MapClassId){info=OtherLite;return true;}return Mask.TryGetMapLiteData(map,out info); }
        public override IMiniMapGridMover GetSpawnGridMover(int map) => Mask.GetSpawnGridMover(map);
        public override IReadOnlyDictionary<int,MiniMapRegionInfo> GetMapRegionDict(int map) => Mask.GetMapRegionDict(map);
        public override MiniMapRegionInfo GetRegion(int map,int region) => Mask.GetRegion(map,region);
        public override int AreaToRegion(int map,int area) => Mask.AreaToRegion(map,area);
        public override int QueryRegion(Pos2d pos) => Mask.QueryRegion(pos);
        public override List<int> GetWorldMapProgresses(int map) => Mask.GetWorldMapProgresses(map);
        public override MiniMapRegionDisplayInfo GetRegionDisplay(int map,int region) => Mask.GetRegionDisplay(map,region);
        public override object QueryCommonGridGround(int map) => Mask.QueryCommonGridGround(map);
        public override object QuerySceneContainer(Component component) => Mask.QuerySceneContainer(component);
        public override MiniMapRegionDescItem InstantiateDescription(MiniMapRegionDescItem prefab,Transform parent) => Mask.InstantiateDescription(prefab,parent);
        public override GameObject InstantiateDisplay(GameObject prefab,Transform parent) => Mask.InstantiateDisplay(prefab,parent);
        public override IMiniMapDescriptionAssetLease<GameObject> LoadPrefab(string path) => Mask.LoadPrefab(path);
        public override IEnumerable<Pos2d> ForEachMapRegionActivedAllGrid(int map,int region) => Mask.ForEachMapRegionActivedAllGrid(map,region);
        public override List<Status> GetMapStatusListInTeam(int map) => Enumerable.Repeat(Status.Private,BoundRegionCount).ToList();
        public override IMiniMapRegionAssetLease LoadAssetByGuid(string guid) => new RegionLease(RegionalPrefabs[guid]);
        public override GameObject InstantiateRegion(GameObject prefab,Transform parent,bool world) => Object.Instantiate(prefab,parent,world);
        private sealed class RegionLease : IMiniMapRegionAssetLease
        { public RegionLease(GameObject prefab){Result=prefab;} public GameObject Result{get;} public void Dispose(){} }
    }

    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private const string Key="Zjcs.MainlineMiniMapRoot.Running";
        private static IEnumerator routine;
        private static int errors;
        private static string Workspace => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        private sealed class Data : IMiniMapOfflineDataSource
        {
            public int Requests,ExpectedMap=13; public MiniMapOfflineDataRequest Pending;
            public MiniMapOfflineDataRequest Request(int map){Require(map==ExpectedMap,"Original request identity");Requests++;return Pending=new MiniMapOfflineDataRequest();}
        }
        private sealed class Services : IServiceProvider,IMiniMapMarkPerformance,IMiniMapClickInput,IMiniMapClickCameraControl,IMiniMapClickCameraBrain,IMiniMapClickPublisher
        {
            public int Registered,Removed;
            public object GetService(Type type)=>throw new NotSupportedException(type.FullName);
            public void RegisterSpawnManager(MiniMapMarkSpawnManager manager){Registered++;}
            public void UnregisterSpawnManager(MiniMapMarkSpawnManager manager){Removed++;}
            public int CanTeamClickPosCheck(int map,Pos2d position)=>0;
            public IMiniMapClickCameraBrain ControlBrain=>this;
            public void SetViewPos(Vector3 position){}
            public void Publish(MiniMapMarkClick.OnClickEventData value){}
        }
        private sealed class ResourcePoints : MiniMapResourcePointContext
        {
            public IMap13LabelServices LabelServices{get;set;}
            public int BoundMap=13;public int MapClassId=>BoundMap;public bool HasMainPlayer=>true;
            public event Action<int,int,Status,Status> OnMapStatusUpdate;
            public MiniMapResourcePointEntityGroupInfo GetRefreshGroupInfo(int group)=>throw new NotSupportedException();
            public int AreaToRegion(int map,int area)=>throw new NotSupportedException();
            public Status GetMapRegionStatus(int map,int region)=>Status.Private;
            public MiniMapResourcePointItemLocationData GetItemLocationInfo(int item)=>throw new NotSupportedException();
            public MiniMapResourcePointRefreshGroupInfo GetRefreshGroupsByAwardRule(int rule)=>throw new NotSupportedException();
            public MiniMapResourcePointRefreshGroupInfo GetRefreshGroupsByMonsterId(int monster)=>throw new NotSupportedException();
            public IResourcePointAssetLease<GameObject> LoadPrefabByGuid(string guid)=>throw new NotSupportedException();
            public IResourcePointAssetLease<Sprite> LoadSprite(string path)=>throw new NotSupportedException();
            public GameObject InstantiatePrefab(GameObject prefab,Transform parent,bool world)=>throw new NotSupportedException();
        }
        private sealed class Queries : IMiniMapManagedQueries,IMiniMapManagedAreaAgent,IMiniMapMarkGridGround
        {
            public Func<Pos2d,int> RegionAt;
            public long MainPlayerId=>1;
            public MiniMapManagedMapPartInfo GetMapPartInfo(int map)=>new MiniMapManagedMapPartInfo{SupportPrivateRegion=true,SupportPublicRegion=true};
            public List<Status> GetCurrentMapStatus()=>Enumerable.Repeat(Status.None,106).ToList();
            public bool TryGetStatusByContainerId(long id,out Status status){status=Status.None;return false;}
            public IMiniMapSpawnEntity GetEntity(Zjcs.Official.StoryPlayables.EcEntityIdentifier id)=>null;
            public IEnumerable<MiniMapMarkLoEntityInfo> GetEntityByMapRegion(int map,int region)=>Array.Empty<MiniMapMarkLoEntityInfo>();
            public Dictionary<Zjcs.Official.StoryPlayables.EcEntityIdentifier,IMiniMapSpawnEntity> GetAreaEntity(int area)=>null;
            public int GridToRegion(int map,Pos2d position)=>RegionAt?.Invoke(position)??0;
            public IMiniMapMarkCell GetPosData(Pos2d position)=>null;
        }
        private sealed class Characters : IMiniMapCharacterManager,IMiniMapCharacterController
        {
            public IMiniMapCharacterController MyMeUiCharacter=>this;
            public MyGridTransform MyGridTransform{get;set;}
            public event Action<IMiniMapCharacterController,IMiniMapCharacterController> OnMeMapControllerUpdated;
            public int Subscribers=>OnMeMapControllerUpdated?.GetInvocationList().Length??0;
        }
        private sealed class GridData : ECGridTransformData
        {
            internal readonly HashSet<IGridTransformPosUpdatedEventReceiver> Receivers=new HashSet<IGridTransformPosUpdatedEventReceiver>();
            internal void Move(Pos2d position){var previous=Position;Position=position;foreach(var receiver in Receivers.ToArray())receiver.GridTransform_OnPosUpdated(this,previous);}
            public Pos2d Position;public MapRootEntityLiteSingleInfo Lite;
            public override Pos2d Pos=>Position;public override Direction Direction=>Direction.Up;public override bool FlipX=>false;
            public override ECGridTransformInfo Info=>null;
            public override Vector3 GridToPosition(Pos2d grid)=>Lite.GridToPos(grid);
            public override Pos2d PositionToGrid(Vector3 pos)=>Lite.PosToGrid(pos);
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver)=>Receivers.Add(receiver);
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver)=>Receivers.Remove(receiver);
        }
        [Serializable] private sealed class Fixture{public int mapClassId,configId,width,height;public float[] origin;public Cell[] cells;public Area[] areas;public RegionRow[] regions;}
        [Serializable] private sealed class Cell{public int area,ground;}
        [Serializable] private sealed class Area{public int x,y,count;}
        [Serializable] private sealed class RegionRow{public int RegionTag,Region;public List<int> Areas;}
        private static MapRootEntityLiteSingleInfo ReadNavigation(int map,Fixture input)
        {
            var lite=new MapRootEntityLiteSingleInfo{MapClassId=map,MapGroundSize=new Pos2d(input.width,input.height),MapGroundOriginPos=new Vector3(input.origin[0],input.origin[1],input.origin[2]),PosDataArray=new MapRootEntityLiteGridInfo[input.height][],AreaInfos=input.areas.Select(a=>new MapRootEntityLiteAreaInfo{TotalPos=new Pos2d(a.x,a.y),TotalCount=a.count}).ToArray()};
            for(int y=0;y<input.height;y++)
            {
                lite.PosDataArray[y]=new MapRootEntityLiteGridInfo[input.width];
                for(int x=0;x<input.width;x++){var cell=input.cells[y*input.width+x];lite.PosDataArray[y][x]=new MapRootEntityLiteGridInfo{Area=cell.area,GroundType=(GroundType)cell.ground};}
            }
            return lite;
        }
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");SessionState.SetBool(Key,true);Hook();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;}
        [InitializeOnLoadMethod] private static void Hook(){if(!SessionState.GetBool(Key,false))return;errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=Mode;}
        private static void Observe(string message,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)errors++;}
        private static void Mode(PlayModeStateChange mode){if(mode==PlayModeStateChange.EnteredPlayMode){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Native root runtime errors: "+errors);SessionState.EraseBool(Key);EditorApplication.update-=Advance;EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);SessionState.EraseBool(Key);EditorApplication.update-=Advance;EditorApplication.Exit(1);}}
        private static IEnumerator Verify()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            const string native="Assets/Official/World/Map/13/NativeMiniMapRootCurrent/";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(native+"Resources/Map13/Native/mini-map-root.prefab");
            Require(prefab&&prefab.GetComponent<MainlineNativeMiniMapRoot>().MapClassId==13,"Complete native original root");
            foreach(string folder in new[]{10,11,12,13,14,16}.Select(id=>$"Assets/Official/World/Map/{id}/NativeMiniMapRootCurrent"))
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{folder}))
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));Require(!PrefabUtility.HasManagedReferencesWithMissingTypes(asset),"Original managed-reference type binding");
                foreach(var transform in asset.GetComponentsInChildren<Transform>(true))Require(transform.GetComponents<MonoBehaviour>().All(c=>c),"Every original component imported");
                foreach(var component in asset.GetComponentsInChildren<Component>(true)){var property=new SerializedObject(component).GetIterator();while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference)Require(property.objectReferenceValue||property.objectReferenceInstanceIDValue==0,"Original reference: "+property.propertyPath);}
            }
            var host=new GameObject("NativeRootStateInputs");host.SetActive(false);
            var description=host.AddComponent<Map13DescriptionFixture>();var labels=new DescriptionLabels();description.Labels=labels;
            description.Settings=AssetDatabase.LoadAssetAtPath<MiniMapSettings>("Assets/Official/World/Map/13/NativeMiniMapSettings/ScriptableConfig/Modules/MapCommon/MiniMapSettings.asset");
            var input=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Workspace,"minimap-description-fixture.json")));
            var lite=ReadNavigation(13,input);
            description.Lite=lite;description.Regions=input.regions.ToDictionary(r=>r.Region,r=>new MiniMapRegionInfo{LabelInfo=labels.Data.regions[r.Region],Areas=r.Areas,RegionTag=(MapRegionType)r.RegionTag});description.AreaRegions=description.Regions.Values.SelectMany(r=>r.Areas.Select(a=>new{a,r.Region})).ToDictionary(v=>v.a,v=>v.Region);
            var mask=host.AddComponent<Map13MaskFixture>();mask.Description=description;mask.Agent=host.AddComponent<Map13MaskAgentFixture>();
            var visibility=JsonUtility.FromJson<OfflineMapGridVisibility.Configuration>(Resources.Load<TextAsset>("Map13/grid-visibility").text);mask.ActiveRegions=visibility.regions.ToDictionary(r=>r.region,r=>new MapActivedRegionGridInfo{RegionActivedGrids=r.RegionActivedGrids,RegionExtendVisiableGrids=r.RegionExtendVisiableGrids});
            var centerInfo=lite.AreaInfos[description.Regions[38].Areas[0]];var center=new Pos2d(centerInfo.TotalPos.X/centerInfo.TotalCount,centerInfo.TotalPos.Y/centerInfo.TotalCount);mask.Position=center;
            var bits=new MapGroundGridBoolData(input.height,input.width);for(int y=center.Y-4;y<=center.Y+4;y++)for(int x=center.X-4;x<=center.X+4;x++)bits.SetPosValue(x,y,true);mask.Agent.Init(bits);
            var state=host.AddComponent<MainlineMiniMapRootStateFixture>();state.Mask=mask;state.Current=16;var queries=new Queries();state.ManagerQueries=queries;state.Area=queries;state.GroundInput=queries;
            state.RegionalPrefabs=new Dictionary<string,GameObject>();var catalog=new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Official/World/Map/13/NativeMiniMapCurrent/Resources/Map13/Native/mini-map-regions.asset"));var entries=catalog.FindProperty("Regions");for(int i=0;i<entries.arraySize;i++){var entry=entries.GetArrayElementAtIndex(i);state.RegionalPrefabs.Add(entry.FindPropertyRelative("OriginalGuid").stringValue,(GameObject)entry.FindPropertyRelative("Prefab").objectReferenceValue);}
            var grid=host.AddComponent<MyGridTransform>();grid.Component=new GridData{Position=center,Lite=lite};var characters=new Characters{MyGridTransform=grid};var data=new Data();var services=new Services();
            var bindings=new MainlineMiniMapBindings{State=state,Settings=description.Settings,MinimapConfigId=2,MapViewRangeExtraSize=0,Characters=characters,ResourcePoints=new ResourcePoints{LabelServices=labels},OfflineData=data,MarkerServices=services,MarkerPerformance=services,ClickInput=services,CameraControl=services,ClickPublisher=services};
            var module=MainlineMiniMap.Create(prefab,null,bindings);for(int i=0;i<4;i++)yield return null;
            Require(data.Requests==1,"Complete original prefab requests offline data after injection");
            var root=Object.FindObjectOfType<MainlineNativeMiniMapRoot>();var offline=root.GetComponent<OfflineMiniMap>();Require(!offline.HaveDataShow.State,"Original prefab content hidden while pending");
            data.Pending.Complete(new PlayerOfflineMiniMapDataResponse{MapClassId=13,GridVisibilityByte=GridVisibilityCompressUtils.DoMyZip(bits),ActiveRegions=new List<int>{38},LiteEntityDataDict=new Dictionary<int,PlayerOfflineMiniMapLiteEntityData>(),ActiveableEntDataDict=new Dictionary<MapWorldActiveableEntityType,List<int>>()},0);
            for(int i=0;i<5;i++)yield return null;Require(offline.HaveDataShow.State,"Original prefab response activates bound content");
            Require(root.ViewRangeHandler!=null&&root.GetComponentsInChildren<MiniMapPrivateRegionMaskManager>(true).Length==1,"Original root owns range and actual regional hierarchy");
            var miniTexture=module.Texture;var originalSize=new SerializedObject(prefab.GetComponent<MainlineNativeMiniMapRoot>().Character).FindProperty("mMiniMapUiTextureSize").vector2IntValue;
            Require(miniTexture.width==originalSize.x&&miniTexture.height==originalSize.y&&ReferenceEquals(miniTexture,module.Texture),"Original minimap texture dimensions and instance reuse");miniTexture.Create();
            var characterRange=root.ViewRangeHandler;
            var cameraHost=new GameObject("FullScreenCameraFixture");cameraHost.SetActive(false);
            var camera=new GameObject("OriginalCamera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(cameraHost.transform,false);camera.orthographic=true;camera.aspect=.75f;
            var touch=cameraHost.AddComponent<UITouchInput>();touch.Data=new Zjcs.Official.WorldUI.DragControllerData{Target=camera.transform};
            var controller=cameraHost.AddComponent<MiniMapCameraController>();controller.Settings=description.Settings;controller.myInput=touch;
            var cameraFields=new SerializedObject(controller);cameraFields.FindProperty("mMiniMapCamera").objectReferenceValue=camera;cameraFields.ApplyModifiedPropertiesWithoutUndo();
            cameraHost.SetActive(true);yield return null;
            int show=0,hide=0,close=0;root.OnShowFullScreen+=()=>{Require(root.IsFullScreen&&root.ViewRangeHandler.VisiblePosList==null,"Original show event runs after full-range transition");show++;};
            root.OnHideFullScreen+=()=>{Require(!root.IsFullScreen&&ReferenceEquals(root.ViewRangeHandler,characterRange),"Original hide event restores cached character range first");hide++;};
            module.CloseRequested+=()=>close++;
            module.ShowFullScreen(controller);for(int i=0;i<3;i++)yield return null;
            Require(show==1&&root.IsFullScreen&&root.ViewRangeHandler.VisiblePosList==null,"Complete native full-screen listeners");
            camera.transform.position=lite.GridToPos(center)+new Vector3(0,0,-250);camera.orthographicSize=35;camera.nearClipPlane=.3f;camera.farClipPlane=500;camera.cullingMask=(1<<16)|(1<<20);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var texture=new RenderTexture(768,1024,24);camera.targetTexture=texture;camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;
            var image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply(false,false);RenderTexture.active=previous;
            Require(image.GetPixels32().Count(p=>p.r>50&&p.g>60&&p.b<180)>100,"Complete native prefab renders explored regional geometry");
            File.WriteAllBytes(Path.Combine(Workspace,"native-minimap-root-offline.png"),image.EncodeToPNG());Object.Destroy(image);
            root.FireToCloseFullScreen();module.HideFullScreen(controller);Require(show==1&&hide==1&&close==1&&!root.IsFullScreen,"Original close/hide dispatch order");
            module.SetActive(false);module.SetActive(true);yield return null;Require(data.Requests==2&&!offline.HaveDataShow.State,"Full native instance re-entry requests and hides");
            module.Dispose();module.Dispose();yield return null;Require(characters.Subscribers==0&&!miniTexture,"Complete native hierarchy releases character subscriptions and owned texture");
            state.Current=13;
            host.AddComponent<MyEntity>();host.AddComponent<MyTransform>().Position=lite.GridToPos(center);
            var hudModule=MainlineMiniMap.Create(prefab,null,bindings);for(int i=0;i<5;i++)yield return null;
            var hudRoot=Object.FindObjectOfType<MainlineNativeMiniMapRoot>();
            var hudTest=VerifyHud(hudModule,hudRoot,state,labels,(GridData)grid.Component,queries);while(hudTest.MoveNext())yield return hudTest.Current;
            hudModule.Dispose();yield return null;Require(characters.Subscribers==0,"Current-map native HUD instance cleanup");state.Current=16;
            // A second actual map uses the same factory and its own original
            // geometry/settings. Its unresolved local response policy stays pending.
            var islandInput=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Workspace,"minimap-description-fixture-16.json")));
            Require(islandInput.width==140&&islandInput.height==120&&islandInput.regions.Length==43,"Original island fixture identity");
            var islandLite=ReadNavigation(16,islandInput);
            Require(islandInput.mapClassId==16&&islandInput.configId==4,"Original island setting identity");
            state.BoundMap=16;state.BoundRegionCount=43;state.OtherLite=islandLite;state.Current=13;data.ExpectedMap=16;bindings.MinimapConfigId=islandInput.configId;((ResourcePoints)bindings.ResourcePoints).BoundMap=16;
            var islandPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/World/Map/16/NativeMiniMapRootCurrent/Resources/Map16/Native/mini-map-root.prefab");
            Require(islandPrefab&&islandPrefab.GetComponent<MainlineNativeMiniMapRoot>().MapClassId==16,"Second complete original prefab");
            var islandModule=MainlineMiniMap.Create(islandPrefab,null,bindings);for(int i=0;i<4;i++)yield return null;
            var islandRoot=Object.FindObjectOfType<MainlineNativeMiniMapRoot>();Require(data.Requests==3&&islandRoot.MapClassId==16&&!islandRoot.GetComponent<OfflineMiniMap>().HaveDataShow.State,"Island identity and pending offline lifecycle");
            islandModule.Dispose();yield return null;Require(characters.Subscribers==0,"Island native instance releases subscriptions");
            foreach(int map in new[]{10,11,12,14})
            {
                var otherInput=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Workspace,$"minimap-description-fixture-{map}.json")));
                Require(otherInput.mapClassId==map&&otherInput.configId>0,"Original map and setting identity");
                var otherLite=ReadNavigation(map,otherInput);
                state.BoundMap=map;state.BoundRegionCount=otherInput.regions.Length;state.OtherLite=otherLite;data.ExpectedMap=map;bindings.MinimapConfigId=otherInput.configId;((ResourcePoints)bindings.ResourcePoints).BoundMap=map;
                var otherPrefab=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Official/World/Map/{map}/NativeMiniMapRootCurrent/Resources/Map{map}/Native/mini-map-root.prefab");
                Require(otherPrefab&&otherPrefab.GetComponent<MainlineNativeMiniMapRoot>().MapClassId==map,"Complete selected original map root");
                var regional=otherPrefab.GetComponentInChildren<MiniMapPrivateRegionMaskManager>(true);Require(new SerializedObject(regional).FindProperty("mRegionTilemapSlots").arraySize==otherInput.regions.Length,"Original region slots match installed configuration");
                int requested=data.Requests;var otherModule=MainlineMiniMap.Create(otherPrefab,null,bindings);for(int i=0;i<4;i++)yield return null;
                var otherRoot=Object.FindObjectOfType<MainlineNativeMiniMapRoot>();Require(data.Requests==requested+1&&otherRoot.MapClassId==map&&!otherRoot.GetComponent<OfflineMiniMap>().HaveDataShow.State,"Configured pending original map instance");
                otherModule.Dispose();yield return null;Require(characters.Subscribers==0,"Configured instance subscription release");
            }
            Object.Destroy(host);Object.Destroy(cameraHost);Object.Destroy(texture);yield return null;labels.Dispose();
            File.WriteAllText(Path.Combine(Workspace,"native-minimap-root-proof.json"),"{\"map\":13,\"complete_original_prefab\":true,\"offline_response\":true,\"fullscreen\":true,\"native_render\":true,\"reentry\":true,\"subscriptions_released\":true,\"pending_only_native_maps\":[10,11,12,14,16]}");Debug.Log("MAINLINE_MINIMAP_NATIVE_ROOT_PASS");
        }
    }
}
