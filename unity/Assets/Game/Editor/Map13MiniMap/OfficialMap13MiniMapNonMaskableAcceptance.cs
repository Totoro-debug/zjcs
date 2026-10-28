using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.WorldUI;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.Map13MiniMap
{
    public static class OfficialMap13MiniMapNonMaskableAcceptance
    {
        private const string Key="Zjcs.Map13.MiniMapNonMaskable.Running";
        private static IEnumerator routine;private static int errors;
        [Serializable] private sealed class Proof{public string editor;public int game_objects,components,mesh_vertices,rendered_pixels;public bool original_prefab,show_hide,zoom_scale,camera_follow,event_unsubscription,original_urp;}
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private static void Require(bool condition,string detail){if(!condition)throw new InvalidDataException(detail);}
        private static void Observe(string condition,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static IEnumerator Verify()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");Require(pipeline,"Original URP asset");GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/World/Map/13/NativeMiniMapNonMaskable/Prefabs/Map/Common/MiniMap/MinimapNonMaskable.prefab");Require(prefab,"Original non-maskable prefab");
            foreach(var t in prefab.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Original prefab script: "+t.name);
            int components=prefab.GetComponentsInChildren<Component>(true).Length;Require(prefab.GetComponentsInChildren<Transform>(true).Length==5&&components==11,"Complete original five-object eleven-component hierarchy");
            var root=Object.Instantiate(prefab);var manager=root.GetComponent<MiniMapNonMaskableManager>();var content=root.transform.Find("Content");var renderer=content.Find("MiniMap").GetComponent<MeshRenderer>();var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            Require(manager&&content&&renderer&&mesh&&mesh.vertexCount==4&&!manager.NonMaskableCamera.enabled,"Original manager Awake, Quad and references");
            Require(renderer.sharedMaterial.shader&&renderer.sharedMaterial.shader.isSupported,"Original built-in shader");
            var sprite=content.Find("Bg").GetComponent<SpriteRenderer>().sprite;Require(sprite&&sprite.vertices.Length==4&&sprite.texture.width==256&&sprite.texture.height==256,"Original background sprite and native texture");
            var host=new GameObject("OriginalCameraFixture");host.SetActive(false);var camera=new GameObject("OriginalMiniMapCamera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(host.transform,false);camera.orthographic=true;camera.nearClipPlane=.3f;camera.farClipPlane=500;camera.transform.localPosition=new Vector3(0,-20,-250);camera.cullingMask=1<<16;
            var input=host.AddComponent<Zjcs.Official.World.Map13MiniMap.UITouchInput>();input.Data=new DragControllerData{Target=camera.transform,BorderBounds=new Bounds(Vector3.zero,new Vector3(240,240,0))};
            var controller=host.AddComponent<MiniMapCameraController>();controller.Settings=AssetDatabase.LoadAssetAtPath<MiniMapSettings>("Assets/Official/World/Map/13/NativeMiniMapSettings/ScriptableConfig/Modules/MapCommon/MiniMapSettings.asset");controller.myInput=input;controller.mySizeMin=20;controller.mySizeMax=60;var serialized=new SerializedObject(controller);serialized.FindProperty("mMiniMapCamera").objectReferenceValue=camera;serialized.ApplyModifiedPropertiesWithoutUndo();host.SetActive(true);yield return null;
            controller.SetCameraOrthographicSize(40);manager.OnShow(controller);Require(manager.NonMaskableCamera.enabled&&manager.FullScreenSwitcher.State&&renderer.sharedMaterial.mainTexture==controller.GetRenderTexture(),"Original show state and shared render texture");
            Require(manager.NonMaskableCamera.orthographicSize==40&&Vector3.Distance(content.localScale,new Vector3(80*camera.aspect,80,1))<.0001f,"Original show scale");
            controller.SetCameraOrthographicSize(60);Require(manager.NonMaskableCamera.orthographicSize==60&&Vector3.Distance(content.localScale,new Vector3(120*camera.aspect,120,1))<.0001f,"Original zoom event scale");
            for(int i=0;i<3;i++)yield return null;Require(content.position==camera.transform.position,"Original LateUpdate camera following");camera.transform.position+=new Vector3(7,-9,0);for(int i=0;i<3;i++)yield return null;Require(content.position==camera.transform.position,"Original moving camera following");
            var grid=new GameObject("OriginalMap13RegionalTilemaps",typeof(Grid));foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Official/World/Map/13/NativeMiniMapRegions"}))Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)),grid.transform,false);
            var main=new GameObject("DiagnosticBaseCamera",typeof(Camera)).GetComponent<Camera>();main.tag="MainCamera";main.orthographic=true;main.transform.position=new Vector3(0,0,-250);main.cullingMask=0;main.clearFlags=CameraClearFlags.SolidColor;main.backgroundColor=Color.black;
            main.GetUniversalAdditionalCameraData().cameraStack.Add(manager.NonMaskableCamera);var target=new RenderTexture(384,288,24);main.targetTexture=target;
            for(int i=0;i<12;i++)yield return null;Require(GraphicsSettings.currentRenderPipeline==pipeline,"Actual original URP running");main.Render();
            var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply(false,false);RenderTexture.active=old;int pixels=image.GetPixels32().Count(p=>p.g>p.r+10&&p.g>p.b+10);Require(pixels>100,"Original non-maskable layer must display actual regional minimap texture");File.WriteAllBytes(Path.Combine(Workspace,"minimap-nonmaskable-render.png"),image.EncodeToPNG());Object.Destroy(image);
            manager.OnHide(controller);Require(!manager.NonMaskableCamera.enabled&&!manager.FullScreenSwitcher.State,"Original hide disables camera and state");Vector3 hiddenPosition=content.position,hiddenScale=content.localScale;controller.SetCameraOrthographicSize(20);camera.transform.position+=Vector3.one;for(int i=0;i<3;i++)yield return null;Require(content.position==hiddenPosition&&content.localScale==hiddenScale,"Original hide detaches zoom and camera tracking");
            manager.OnShow(controller);Require(manager.NonMaskableCamera.enabled&&manager.NonMaskableCamera.orthographicSize==20&&content.localScale.y==40,"Original re-entry uses current zoom");manager.OnHide(controller);
            File.WriteAllText(Path.Combine(Workspace,"minimap-nonmaskable-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,game_objects=5,components=components,mesh_vertices=mesh.vertexCount,rendered_pixels=pixels,original_prefab=true,show_hide=true,zoom_scale=true,camera_follow=true,event_unsubscription=true,original_urp=true},true)+"\n");
            main.targetTexture=null;Object.Destroy(target);Object.Destroy(main.gameObject);Object.Destroy(grid);Object.Destroy(root);Object.Destroy(host);yield return null;
            Debug.Log("Map13 complete original non-maskable prefab Play Mode passed: show/hide/re-entry, original zoom scale and camera following, shared render target, unsubscription and actual native regional minimap rendering through original URP overlay. Formal map mouse/save and Windows Player remain required.");
        }
    }
}
