using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Object=UnityEngine.Object;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;

namespace Zjcs.Editor.Map13MiniMap
{
    public sealed class Map13ClickFixture:Map13ManagedFixture,IMiniMapClickContext
    {public Object VisibilityObject;public Object MyGridVisibilityAgent=>VisibilityObject;public bool HaveLite=true;public override bool TryGetMapLiteData(int map,out MapRootEntityLiteSingleInfo value){value=Lite;return HaveLite;}}
    public sealed class Map13ClickLookupFixture:MonoBehaviour,IMiniMapMark
    {public MiniMapMarkUIBase Mark;public Action Query;public bool TryGetMark(Pos2d pos,out MiniMapMarkUIBase mark){Query?.Invoke();mark=Mark;return mark;}}
    public sealed class Map13ClickDialogFixture:MiniMapMarkUIBase
    {public bool Interactive;public int Opened;public Action OnOpen;public override MiniMapMarkType MarkType=>MiniMapMarkType.MarkOnly;public override bool CanInteractive=>Interactive;public override void OpenDialog(){Opened++;OnOpen?.Invoke();}}
    public static class OfficialMap13MiniMapMarkClickAcceptance
    {
        private const string Key="Zjcs.Map13.MarkClick.Running";
        private static IEnumerator routine;private static int errors;
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        private sealed class Input:IMiniMapClickInput{public int Error,Calls;public Func<int> Next;public int CanTeamClickPosCheck(int map,Pos2d p){Calls++;return Next!=null?Next():Error;}}
        private sealed class Brain:IMiniMapClickCameraBrain,IMiniMapClickCameraControl{public int Calls;public Vector3 Last;public IMiniMapClickCameraBrain ControlBrain=>this;public void SetViewPos(Vector3 position){Calls++;Last=position;}}
        private sealed class Publisher:IMiniMapClickPublisher{public readonly List<MiniMapMarkClick.OnClickEventData> Values=new List<MiniMapMarkClick.OnClickEventData>();public Action OnPublish;public void Publish(MiniMapMarkClick.OnClickEventData value){Values.Add(value);OnPublish?.Invoke();}}
        private sealed class Characters:IMiniMapCharacterManager{public IMiniMapCharacterController MyMeUiCharacter=>null;public event Action<IMiniMapCharacterController,IMiniMapCharacterController> OnMeMapControllerUpdated;}
        private sealed class Performance:IMiniMapMarkPerformance{public void RegisterSpawnManager(MiniMapMarkSpawnManager value){}public void UnregisterSpawnManager(MiniMapMarkSpawnManager value){}}
        private sealed class Queries:IMiniMapManagedQueries
        {
            public MiniMapManagedMapPartInfo GetMapPartInfo(int map)=>new MiniMapManagedMapPartInfo{SupportPrivateRegion=true,SupportPublicRegion=true};
            public List<MapRegionStatus> GetCurrentMapStatus()=>throw new NotSupportedException();public bool TryGetStatusByContainerId(long container,out MapRegionStatus status)=>throw new NotSupportedException();public IMiniMapSpawnEntity GetEntity(EcEntityIdentifier id)=>throw new NotSupportedException();public IEnumerable<MiniMapMarkLoEntityInfo> GetEntityByMapRegion(int map,int region)=>throw new NotSupportedException();public long MainPlayerId=>7;
        }
        [Serializable] private sealed class Proof{public string editor;public int simulated_clicks,publications,input_checks,dialog_opens,brain_moves,rendered_pixels;public bool native_frame,original_frame_size,collider_transform_position,main_viewport_fallback,transform_subtraction,publish_before_query,first_manager_wins,error_branch,visibility_guard,z_preserved,hide_unsubscribe,reentry,record_members,play_mode;}
        private static void Set(object target,string name,object value){for(var t=target.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);if(f!=null){f.SetValue(target,value);return;}}throw new MissingFieldException(name);}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        private static void Observe(string a,string b,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Unexpected runtime errors "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static void Click(EventSystem system,GameObject surface,Vector2 point)
        {
            Canvas.ForceUpdateCanvases();var pointer=new PointerEventData(system){position=point,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();system.RaycastAll(pointer,hits);Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==surface,"Simulated mouse reaches actual desktop input surface");ExecuteEvents.Execute(surface,pointer,ExecuteEvents.pointerClickHandler);
        }
        private static IEnumerator Verify()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            var host=new GameObject("OriginalClickRuleFixture");host.SetActive(false);host.transform.position=new Vector3(17,-23,0);var map=host.AddComponent<Map13ClickFixture>();map.ManagerQueries=new Queries();map.VisibilityObject=new GameObject("ActualVisibilityLifetime");
            var labels=new DescriptionLabels();var manager=host.AddComponent<MiniMapPrivateMarkManager>();manager.MiniMap=map;manager.Services=labels;manager.MapMyCharacterManager=new Characters();manager.enabled=false;
            var spawn=host.GetComponent<MiniMapMarkSpawnManager>();spawn.Services=labels;spawn.PerformanceMiniMapMarkProxy=new Performance();Set(spawn,"mMinimap",map);spawn.enabled=false;
            var first=host.AddComponent<Map13ClickLookupFixture>();var second=host.AddComponent<Map13ClickLookupFixture>();
            var frame=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/World/Map/13/NativeMiniMapClickFrame/Prefabs/Map/Map_13/MiniMap/CurrentCameraRect.prefab"),host.transform,false).GetComponent<SpriteRenderer>();Require(frame&&frame.sprite&&frame.drawMode==SpriteDrawMode.Sliced&&!frame.gameObject.activeSelf&&frame.gameObject.layer==20,"Original sliced/inactive/layer camera frame");float frameZ=frame.transform.localPosition.z;
            var click=host.AddComponent<MiniMapMarkClick>();click.MiniMap=map;click.Services=labels;Set(click,"mCameraRectFrameSprite",frame);var input=new Input();var brain=new Brain();var publisher=new Publisher();click.mMapInput=input;click.CameraControlManager=brain;click.OnClickEventPublisher=publisher;
            var main=new GameObject("MainCamera",typeof(Camera)).GetComponent<Camera>();main.tag="MainCamera";main.orthographic=true;main.orthographicSize=8;main.transform.position=new Vector3(2,3,-250);main.cullingMask=0;main.clearFlags=CameraClearFlags.SolidColor;main.backgroundColor=Color.black;
            var cameraRig=new GameObject("OriginalMiniMapCameraFixture");cameraRig.SetActive(false);var miniCamera=new GameObject("MiniMapCamera",typeof(Camera)).GetComponent<Camera>();miniCamera.transform.SetParent(cameraRig.transform,false);miniCamera.transform.position=host.transform.position+new Vector3(4,5,-250);miniCamera.orthographic=true;miniCamera.orthographicSize=25;miniCamera.nearClipPlane=-20;miniCamera.farClipPlane=500;miniCamera.cullingMask=1<<20;miniCamera.clearFlags=CameraClearFlags.SolidColor;miniCamera.backgroundColor=Color.black;miniCamera.gameObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(1);
            var canvas=new GameObject("SimulatedInputFixture",typeof(Canvas),typeof(GraphicRaycaster)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var surface=new GameObject("ActualDesktopTouchInput",typeof(RectTransform),typeof(Image));surface.transform.SetParent(canvas.transform,false);var rect=surface.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;surface.GetComponent<Image>().color=Color.clear;
            var touch=surface.AddComponent<UITouchInput>();touch.Data=new Zjcs.Official.WorldUI.DragControllerData{Target=miniCamera.transform,BorderBounds=new Bounds(Vector3.zero,Vector3.one*10000)};var controller=cameraRig.AddComponent<MiniMapCameraController>();controller.myInput=touch;Set(controller,"mMiniMapCamera",miniCamera);controller.Settings=AssetDatabase.LoadAssetAtPath<MiniMapSettings>("Assets/Official/World/Map/13/NativeMiniMapSettings/ScriptableConfig/Modules/MapCommon/MiniMapSettings.asset");controller.mySizeMin=20;controller.mySizeMax=60;
            var system=new GameObject("SimulatedMouseEventSystem",typeof(EventSystem)).GetComponent<EventSystem>();cameraRig.SetActive(true);host.SetActive(true);yield return null;yield return null;
            Require(frame.gameObject.activeSelf,"Original lifecycle shows frame in current map");click.OnShowFullScreenMiniMap(controller);Require(frame.size==new Vector2(main.orthographicSize*2*main.aspect,main.orthographicSize*2),"Original main-camera frame size");Require(frame.transform.localPosition==new Vector3(main.transform.position.x,main.transform.position.y,frameZ)&&brain.Calls==0,"Show frame follows main position without moving camera");
            var trace=new List<string>();publisher.OnPublish=()=>trace.Add("publish");first.Query=()=>trace.Add("first");second.Query=()=>trace.Add("second");input.Next=()=>{trace.Add("input");return input.Error;};int clicks=0;
            Vector2 point=new Vector2(Screen.width*.43f,Screen.height*.57f);Vector3 expected=miniCamera.ViewportToWorldPoint(main.ScreenToViewportPoint(new Vector3(point.x,point.y,0)))-host.transform.position;
            Click(system,surface,point);clicks++;Require(publisher.Values.Last().TargetPos==map.Lite.PosToGrid(expected)&&frame.transform.localPosition==new Vector3(expected.x,expected.y,frameZ)&&brain.Last==expected,"Original main viewport fallback, transform subtraction and frame XY");Require(trace.SequenceEqual(new[]{"publish","first","second"})&&input.Calls==0,"Publish before lookup and no mark skips team check");
            var collider=new GameObject("OriginalRaycastPositionFixture",typeof(BoxCollider));collider.layer=LayerMask.NameToLayer("MiniMap");collider.transform.position=host.transform.position+new Vector3(4,5,0);collider.GetComponent<BoxCollider>().center=new Vector3(1,0,0);collider.GetComponent<BoxCollider>().size=new Vector3(3,3,1);Physics.SyncTransforms();point=miniCamera.WorldToScreenPoint(collider.transform.position+new Vector3(1,0,0));trace.Clear();Click(system,surface,point);clicks++;Require(brain.Last==new Vector3(4,5,0)&&publisher.Values.Last().TargetPos==map.Lite.PosToGrid(new Vector3(4,5,0)),"Original 3D raycast uses collider Transform instead of impact/center");
            var marker=new GameObject("InteractiveBranchFixture").AddComponent<Map13ClickDialogFixture>();marker.Interactive=true;marker.OnOpen=()=>trace.Add("dialog");first.Mark=marker;second.Mark=new GameObject("LaterManagerFixture").AddComponent<Map13ClickDialogFixture>();input.Error=2116;trace.Clear();int moves=brain.Calls;Click(system,surface,point);clicks++;Require(trace.SequenceEqual(new[]{"publish","first","input"})&&map.Errors.Last()==2116&&marker.Opened==0&&brain.Calls==moves,"First mark wins and original team error skips camera/dialog");
            input.Error=0;trace.Clear();Click(system,surface,point);clicks++;Require(trace.SequenceEqual(new[]{"publish","first","input","dialog"})&&marker.Opened==1&&brain.Calls==moves,"Allowed interactive click opens dialog without recenter");
            marker.Interactive=false;trace.Clear();Click(system,surface,point);clicks++;Require(brain.Calls==moves+1&&!trace.Contains("input"),"Noninteractive mark follows original recenter branch");
            Object.Destroy(map.VisibilityObject);yield return null;moves=brain.Calls;var last=frame.transform.localPosition;Click(system,surface,point);clicks++;Require(brain.Calls==moves&&frame.transform.localPosition==last,"Destroyed original visibility object blocks recenter");
            var target=new GameObject("RecenterPreservesZ").transform;target.position=new Vector3(0,0,-777);Require(click.TrySetCameraPos(target)&&target.position==new Vector3(frame.transform.position.x,frame.transform.position.y,-777),"Original recenter uses world XY and caller Z");click.enabled=false;Require(!click.TrySetCameraPos(target),"Disabled click recenter guard");click.enabled=true;
            int publications=publisher.Values.Count;click.OnHideFullScreenMiniMap(controller);Click(system,surface,point);clicks++;Require(publisher.Values.Count==publications,"Hide unsubscribes actual desktop click event");map.VisibilityObject=target;click.OnShowFullScreenMiniMap(controller);Click(system,surface,point);clicks++;Require(publisher.Values.Count==publications+1,"Reentry subscribes exactly once");
            var value=new MiniMapMarkClick.OnClickEventData(new Pos2d(8,9));value.Deconstruct(out var position);Require(position==value.TargetPos&&value.Equals((object)new MiniMapMarkClick.OnClickEventData(position))&&value==new MiniMapMarkClick.OnClickEventData(position)&&value!=default(MiniMapMarkClick.OnClickEventData)&&value.ToString()=="OnClickEventData { TargetPos = "+position+" }","Complete record struct consumption members");map.HaveLite=false;Require(MiniMapClickRules.MapPosToGrid(map,13,Vector3.one)==Pos2d.Zero,"Original missing lite data branch");map.HaveLite=true;
            var renderTarget=new RenderTexture(640,480,24,RenderTextureFormat.ARGB32);renderTarget.Create();miniCamera.targetTexture=renderTarget;miniCamera.Render();var old=RenderTexture.active;RenderTexture.active=renderTarget;var image=new Texture2D(640,480,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,640,480),0,0);image.Apply();RenderTexture.active=old;int pixels=image.GetPixels32().Count(p=>p.r+p.g+p.b>30);Require(pixels>20,"Actual sliced official frame renders");File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"minimap-original-click-frame.png"),image.EncodeToPNG());Object.Destroy(image);
            var proof=new Proof{editor=Application.unityVersion,simulated_clicks=clicks,publications=publisher.Values.Count,input_checks=input.Calls,dialog_opens=marker.Opened,brain_moves=brain.Calls,rendered_pixels=pixels,native_frame=true,original_frame_size=true,collider_transform_position=true,main_viewport_fallback=true,transform_subtraction=true,publish_before_query=true,first_manager_wins=true,error_branch=true,visibility_guard=true,z_preserved=true,hide_unsubscribe=true,reentry=true,record_members=true,play_mode=Application.isPlaying};
            click.OnHideFullScreenMiniMap(controller);Object.Destroy(host);Object.Destroy(main.gameObject);Object.Destroy(cameraRig);Object.Destroy(canvas.gameObject);Object.Destroy(system.gameObject);Object.Destroy(collider);Object.Destroy(marker.gameObject);Object.Destroy(second.Mark.gameObject);Object.Destroy(target.gameObject);Object.Destroy(renderTarget);yield return null;labels.Dispose();yield return null;
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"minimap-mark-click-proof.json"),JsonUtility.ToJson(proof,true));Debug.Log("MAP13_ORIGINAL_MINIMAP_CLICK_PASS");
        }
    }
}
