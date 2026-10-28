using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.WorldUI;
using FullScreenAction=Zjcs.Official.StoryPlayables.GAction;
using Object=UnityEngine.Object;

namespace Zjcs.Editor.Map13MiniMap
{
    public sealed class Map13SizingFixture:MiniMapSizingContext
    {
        private readonly FullScreenAction hidden=new FullScreenAction();
        public override MiniMapKeepSizeHandler SizingHandler=>GetComponent<MiniMapKeepSizeHandler>();
        public override FullScreenAction HideFullScreenAction=>hidden;
        public override int MapClassId=>13;public override int CurrentMapClassId=>13;public override bool HasMainPlayer=>true;public override IMapViewRangeHandler ViewRangeHandler=>null;
        public override event Action<IMapViewRangeHandler> OnVisibleRangeUpdate;
        public override event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;
        public override List<MapRegionStatus> GetMapStatusListInTeam(int map)=>throw new NotSupportedException();
        public override bool IsRangeInViewRange(Pos2d min,Pos2d max)=>throw new NotSupportedException();
        public override IMiniMapRegionAssetLease LoadAssetByGuid(string guid)=>throw new NotSupportedException();
        public override GameObject InstantiateRegion(GameObject prefab,Transform parent,bool world)=>throw new NotSupportedException();
    }
    public static class OfficialMap13MiniMapSizingAcceptance
    {
        private const string Key="Zjcs.Map13.MiniMapSizing.Running";
        private static IEnumerator routine;private static int errors;
        [Serializable] private sealed class Proof{public string editor;public int thresholds,formula_cases,helper_cases,wrapper_cases,ordered_events;public bool full_original_classes,exact_thresholds,ratio_guards,original_uniform_scale,hide_restores_default,unsubscribe,reentry;}
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private static void Require(bool condition,string detail){if(!condition)throw new InvalidDataException(detail);}
        private static void Scale(Transform target,Vector3 expected,string detail)=>Require(Vector3.Distance(target.localScale,expected)<.00001f,detail+": "+target.localScale+" expected "+expected);
        private static void Observe(string condition,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static IEnumerator Verify()
        {
            var root=new GameObject("OriginalMiniMapSizingCamera");root.SetActive(false);var camera=new GameObject("OriginalMiniMapCamera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.orthographic=true;
            var input=root.AddComponent<Zjcs.Official.World.Map13MiniMap.UITouchInput>();input.Data=new DragControllerData{Target=camera.transform,BorderBounds=new Bounds(Vector3.zero,new Vector3(240,240,0))};var controller=root.AddComponent<MiniMapCameraController>();controller.Settings=AssetDatabase.LoadAssetAtPath<MiniMapSettings>("Assets/Official/World/Map/13/NativeMiniMapSettings/ScriptableConfig/Modules/MapCommon/MiniMapSettings.asset");controller.myInput=input;controller.mySizeMin=20;controller.mySizeMax=60;var serialized=new SerializedObject(controller);serialized.FindProperty("mMiniMapCamera").objectReferenceValue=camera;serialized.ApplyModifiedPropertiesWithoutUndo();root.SetActive(true);yield return null;
            var map=new GameObject("OriginalMiniMapSizingFixture");var context=map.AddComponent<Map13SizingFixture>();var handler=map.AddComponent<MiniMapKeepSizeHandler>();Require(map.GetComponent<MiniMapSizingContext>()==context,"Required actual context is preserved");
            int events=0;handler.OnCameraOrthographicSizeUpdate.Subscribe(c=>{Require(c==controller,"Original camera event identity");events++;});controller.SetCameraOrthographicSize(20);handler.OnShowFullScreenMiniMap(controller);Require(events==1,"Original show immediately invokes current camera event");
            float[] thresholds={0,.25f,.5f,1};float[] sizes={20,30,40,50,60};float[][] formula={new[]{1,1.5f,2,2.5f,3},new[]{1,1,4f/3,5f/3,2},new[]{1,1,1,1.25f,1.5f},new float[]{1,1,1,1,1}};int formulaCases=0,helperCases=0;
            for(int threshold=0;threshold<thresholds.Length;threshold++)for(int i=0;i<sizes.Length;i++)
            {controller.SetCameraOrthographicSize(sizes[i]);float actual=MiniMapKeepSizeHandler.GetScale(controller,thresholds[threshold],controller.GetZoomRate());Require(Math.Abs(actual-formula[threshold][i])<.000001f,"Original threshold formula: "+thresholds[threshold]+" size "+sizes[i]);formulaCases++;}
            var defaults=new Vector3(2,3,4);
            for(int threshold=0;threshold<thresholds.Length;threshold++)
            {
                var content=new GameObject("OriginalSizeHelperContent");content.transform.localScale=defaults;var helper=new MinimapKeepSizeObjHelper();helper.Start(context,content,thresholds[threshold]);
                for(int i=0;i<sizes.Length;i++)
                {controller.SetCameraOrthographicSize(sizes[i]);Vector3 expected=threshold==0||i==0?defaults:Vector3.one*formula[threshold][i];Scale(content.transform,expected,"Original helper guards/uniform scale");helperCases++;}
                var last=content.transform.localScale;camera.orthographicSize=10;handler.OnCameraOrthographicSizeUpdate.Invoke(controller);Scale(content.transform,last,"Negative ratio preserves current scale");camera.orthographicSize=80;handler.OnCameraOrthographicSizeUpdate.Invoke(controller);Scale(content.transform,last,"Ratio above one preserves current scale");helperCases+=2;context.HideFullScreenAction.Invoke();Scale(content.transform,defaults,"Original hide restores complete nonuniform default scale");helper.End();content.transform.localScale=Vector3.one*7;controller.SetCameraOrthographicSize(60);context.HideFullScreenAction.Invoke();Scale(content.transform,Vector3.one*7,"Original End unsubscribes camera and hide");Object.Destroy(content);
            }
            // The wrapper follows the original UIBase initialization/enable cycle.
            int wrappers=0;foreach(float requested in new[]{-1f,.25f})
            {
                var content=new GameObject("OriginalSizeWrapperContent");content.transform.localScale=defaults;var wrapper=content.AddComponent<MiniMapKeepSizeObj>();wrapper.myContent=content;wrapper.Init(context,requested);Require(wrapper.ZoomThreshold==(requested>0?requested:.5f),"Original positive-only threshold override");controller.SetCameraOrthographicSize(60);float expected=requested>0?2:1.5f;Scale(content.transform,Vector3.one*expected,"Original initialized wrapper forwards zoom");context.HideFullScreenAction.Invoke();Scale(content.transform,defaults,"Original wrapper hide restoration");wrapper.enabled=false;content.transform.localScale=Vector3.one*8;controller.SetCameraOrthographicSize(40);context.HideFullScreenAction.Invoke();Scale(content.transform,Vector3.one*8,"Disabled wrapper detaches both original subscriptions");wrapper.enabled=true;controller.SetCameraOrthographicSize(60);Scale(content.transform,Vector3.one*expected,"Re-enabled wrapper resumes original zoom");context.HideFullScreenAction.Invoke();Scale(content.transform,Vector3.one*8,"Re-entry captures current default scale as original source does");wrapper.enabled=false;Object.Destroy(content);wrappers++;
            }
            int before=events;handler.OnHideFullScreenMiniMap(controller);controller.SetCameraOrthographicSize(30);Require(events==before,"Original full-screen hide detaches controller event");handler.OnShowFullScreenMiniMap(controller);Require(events==before+1,"Original full-screen re-entry invokes exactly once");controller.SetCameraOrthographicSize(40);Require(events==before+2,"Original re-entry keeps one controller subscription");handler.OnHideFullScreenMiniMap(controller);
            File.WriteAllText(Path.Combine(Workspace,"minimap-sizing-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,thresholds=4,formula_cases=formulaCases,helper_cases=helperCases,wrapper_cases=wrappers,ordered_events=events,full_original_classes=true,exact_thresholds=true,ratio_guards=true,original_uniform_scale=true,hide_restores_default=true,unsubscribe=true,reentry=true},true)+"\n");Object.Destroy(map);Object.Destroy(root);yield return null;
            Debug.Log("Map13 complete original minimap sizing Play Mode passed: exact threshold formula and boundary ratios, original uniform scaling/default restoration, UIBase positive-only threshold override, disable/re-entry and exact event subscription order. Original root/prefab binding and formal map mouse/save/Windows Player remain required.");
        }
    }
}
