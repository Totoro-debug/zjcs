using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.WorldUI;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.Map13MiniMap
{
    public static class OfficialMap13MiniMapCameraAcceptance
    {
        private const string Key="Zjcs.Map13.MiniMapCamera.Running";
        private const string Assets="Assets/Official/World/Map/13/NativeMiniMapSettings/";
        private static IEnumerator routine;private static int errors;
        [Serializable] private sealed class SourceSetting{public int MinimapConfigId,ShowMapSwitch;public string MinimapConfigDesc;public float MiniMapUICameraOrthographicSize,BoundExtendSize,MarkZoomThreshold;public MiniMapSettings.MiniMapSetting.MiniMapCameraControllerSetting CameraControllerSetting;public MiniMapSettings.MiniMapSetting.MiniMapRegionDescSetting DescSetting;}
        [Serializable] private sealed class SourceFields{public float MiniMapFullScreenTextureQuality;public SourceSetting[] Settings;}
        [Serializable] private sealed class Source{public SourceFields fields;}
        [Serializable] private sealed class Proof{public string editor;public int camera_settings,tile_configurations,tile_rows,zoom_events,texture_width,texture_height,rendered_pixels,dash_vertices,dash_triangles;public bool original_settings,original_tile_cache,original_zoom_order,windows_wheel_hook,render_target_destroyed;}
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private static string Repository=>Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        private static void Require(bool condition,string detail){if(!condition)throw new InvalidDataException(detail);}
        private static void Observe(string condition,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static IEnumerator Verify()
        {
            var originalPipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            Require(originalPipeline,"Original Map13 URP asset");GraphicsSettings.renderPipelineAsset=originalPipeline;QualitySettings.renderPipeline=originalPipeline;
            var settings=AssetDatabase.LoadAssetAtPath<MiniMapSettings>(Assets+"ScriptableConfig/Modules/MapCommon/MiniMapSettings.asset");
            Require(settings&&settings.Settings.Count==5&&settings.TileSettings.Count==10,"Complete original settings asset");
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(Repository,"unity/Assets/Official/World/Map/13/Source/NativeMiniMapSource/minimap-settings.json"))).fields;
            Require(settings.MiniMapFullScreenTextureQuality==source.MiniMapFullScreenTextureQuality,"Exact original texture quality");
            foreach(var s in source.Settings)
            {
                var actual=settings.GetMinimapSetting(s.MinimapConfigId);Require(actual!=null&&actual.MinimapConfigDesc==s.MinimapConfigDesc&&actual.MiniMapUICameraOrthographicSize==s.MiniMapUICameraOrthographicSize&&actual.BoundExtendSize==s.BoundExtendSize&&actual.MarkZoomThreshold==s.MarkZoomThreshold&&actual.ShowMapSwitch==(s.ShowMapSwitch!=0)&&actual.CameraControllerSetting.MinCameraOrthographicSize==s.CameraControllerSetting.MinCameraOrthographicSize&&actual.CameraControllerSetting.MaxCameraOrthographicSize==s.CameraControllerSetting.MaxCameraOrthographicSize&&actual.DescSetting.ZoomThreshold==s.DescSetting.ZoomThreshold&&actual.DescSetting.DashedLinePrefab,"Original setting fields: "+s.MinimapConfigId);
                Require(ReferenceEquals(actual,settings.GetMinimapSetting(s.MinimapConfigId)),"Original setting cache identity");
            }
            Require(settings.GetMinimapSetting(-999)==null,"Original missing setting branch");
            int rows=0;foreach(var tileSetting in settings.TileSettings)foreach(int map in tileSetting.MapClassIds)foreach(var area in tileSetting.AreaTileDatas)foreach(var row in area.GroundTypeTileDatas)
            {
                Require(row.Tile&&row.Tile.sprite&&row.Tile.sprite.texture,"All original map/area Tiles and Sprite references");
                Require(settings.GetTile(map,area.AreaTag,row.GroundType)==row.Tile,"Original tile lookup: "+map+"/"+area.AreaTag+"/"+row.GroundType);rows++;
                if(string.IsNullOrEmpty(area.AreaTag))Require(settings.GetTile(map,"UnconfiguredDiagnosticArea",row.GroundType)==row.Tile,"Original area fallback selects default");
            }
            var line=Object.Instantiate(settings.GetMinimapSetting(2).DescSetting.DashedLinePrefab);var dashed=line.GetComponent<LineRendererDashedLine>();Require(dashed&&GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(line)==0,"Original dashed prefab script binding");
            dashed.SetPoints(new[]{new Vector3(-12,0,0),new Vector3(12,0,0),new Vector3(12,12,0)});yield return null;yield return null;
            var mesh=line.GetComponent<MeshFilter>().sharedMesh;Require(mesh&&mesh.vertexCount>20&&mesh.triangles.Length>30&&mesh.vertices.All(v=>v.z==0),"Original generated dashed mesh");int vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3;
            var root=new GameObject("OriginalMinimapCameraDiagnostic");root.SetActive(false);
            var camera=new GameObject("OriginalMinimapRenderCamera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.orthographic=true;camera.nearClipPlane=.3f;camera.farClipPlane=500;camera.transform.localPosition=new Vector3(0,-20,-250);camera.cullingMask=1<<16;
            var input=root.AddComponent<Zjcs.Official.World.Map13MiniMap.UITouchInput>();input.Data=new DragControllerData{Target=camera.transform,BorderBounds=new Bounds(Vector3.zero,new Vector3(240,240,0))};
            var controller=root.AddComponent<MiniMapCameraController>();controller.Settings=settings;controller.myInput=input;controller.mySizeMin=20;controller.mySizeMax=60;
            var serialized=new SerializedObject(controller);serialized.FindProperty("mMiniMapCamera").objectReferenceValue=camera;serialized.ApplyModifiedPropertiesWithoutUndo();root.SetActive(true);yield return null;
            Require(!camera.enabled&&Math.Abs(camera.aspect-(float)Screen.width/Screen.height)<.00001f,"Original Awake camera setup");
            int zoomEvents=0;controller.OnCameraOrthographicSizeUpdate+=c=>{Require(c==controller&&camera.orthographicSize>=20&&camera.orthographicSize<=60,"Camera updates before original event");Require(Math.Abs(input.ScreenOffsetScale.y-camera.orthographicSize*2/Screen.height)<.00001f,"Original input scale updates before event");zoomEvents++;};
            controller.SetCameraOrthographicSize(1);Require(camera.orthographicSize==20&&controller.GetZoomRate()==0,"Original minimum clamp");controller.SetCameraOrthographicSize(100);Require(camera.orthographicSize==60&&controller.GetZoomRate()==1,"Original maximum clamp");controller.SetCameraOrthographicSize(40);Require(controller.GetZoomRate()==.5f,"Original midpoint");
            var eventSystem=new GameObject("DiagnosticEventSystem",typeof(EventSystem)).GetComponent<EventSystem>();ExecuteEvents.Execute(input.gameObject,new PointerEventData(eventSystem){position=new Vector2(Screen.width/2f,Screen.height/2f),scrollDelta=Vector2.up},ExecuteEvents.scrollHandler);Require(camera.orthographicSize==30&&zoomEvents==4,"Windows wheel reaches original zoom formula");
            controller.enabled=false;ExecuteEvents.Execute(input.gameObject,new PointerEventData(eventSystem){scrollDelta=Vector2.up},ExecuteEvents.scrollHandler);Require(camera.orthographicSize==30&&zoomEvents==4,"Original disable unhooks input");controller.enabled=true;controller.SetCameraOrthographicSize(60);
            var target=controller.GetRenderTexture();float renderScale=GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline?pipeline.renderScale:1;float quality=settings.MiniMapFullScreenTextureQuality*renderScale;
            Require(target&&target.width==(int)(Screen.width*quality)&&target.height==(int)(Screen.height*quality)&&controller.GetRenderTexture()==target,"Original texture quality, truncation and cache");
            var grid=new GameObject("OriginalMap13RegionsCameraFixture",typeof(Grid));foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Official/World/Map/13/NativeMiniMapRegions"})){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));Object.Instantiate(prefab,grid.transform,false);}
            var main=new GameObject("DiagnosticMainCamera",typeof(Camera)).GetComponent<Camera>();main.tag="MainCamera";main.transform.position=new Vector3(0,0,-250);main.orthographic=true;main.cullingMask=0;main.clearFlags=CameraClearFlags.SolidColor;main.backgroundColor=Color.black;
            for(int i=0;i<8;i++)yield return null;
            Require(GraphicsSettings.currentRenderPipeline==originalPipeline,"Use the actual restored original render pipeline");
            main.Render();
            var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply(false,false);RenderTexture.active=old;
            int pixels=image.GetPixels32().Count(p=>p.a>0&&p.r+p.g+p.b>20);Require(pixels>100,"Original camera rendering callback must draw the actual Tilemaps");File.WriteAllBytes(Path.Combine(Workspace,"minimap-camera-regions.png"),image.EncodeToPNG());Object.Destroy(image);
            Object.Destroy(root);yield return null;Require(!target,"Original render target is destroyed with controller");
            File.WriteAllText(Path.Combine(Workspace,"minimap-camera-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,camera_settings=5,tile_configurations=10,tile_rows=rows,zoom_events=zoomEvents,texture_width=(int)(Screen.width*quality),texture_height=(int)(Screen.height*quality),rendered_pixels=pixels,dash_vertices=vertices,dash_triangles=triangles,original_settings=true,original_tile_cache=true,original_zoom_order=true,windows_wheel_hook=true,render_target_destroyed=true},true)+"\n");
            Object.Destroy(grid);Object.Destroy(main.gameObject);Object.Destroy(line);Object.Destroy(eventSystem.gameObject);
            Debug.Log("Map13 original minimap camera/settings Play Mode passed: five camera settings, ten complete tile configurations, original lookup/fallback/cache, dashed mesh, zoom clamp/order, Windows wheel callback and actual original URP rendering with native regions; render target released. Pointer callback checks are not formal map mouse/save or Windows Player validation.");
        }
    }
}
