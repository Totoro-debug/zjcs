using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.Map13MiniMapWindows;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Offline.World.NativeMap13;
using Object=UnityEngine.Object;
using PopupWindow=Zjcs.Official.World.Map13MiniMapWindows.PopupWindow;
using FullScreenAction=Zjcs.Official.StoryPlayables.GAction<UnityEngine.GameObject>;

namespace Zjcs.Editor.Map13MiniMap
{
    public sealed class Map13NativeDialogFixture:Map13MarkFixture
    {
        public override void CloseWindow(GameObject window){base.CloseWindow(window);window.GetComponent<PopupWindow>().Hide().Forget();}
    }
    public static class OfficialMap13MiniMapMarkDialogsAcceptance
    {
        private const string Key="Zjcs.Map13.MiniMapMarkDialogs.Running";
        private static IEnumerator routine;private static readonly Stack<IEnumerator> routines=new Stack<IEnumerator>();private static int errors;
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private sealed class Data:MiniMapMarkBase
        {
            private readonly List<string> trace;private readonly string description;
            public Data(List<string> events,string text){trace=events;description=text;MapClassId=13;Region=38;GridPos=new Pos2d(111,146);}
            public override MiniMapMarkDataType DataType=>MiniMapMarkDataType.Entity;
            public override bool EntityActive=>false;
            public override List<Pos2d> BodyRange{get;}=new List<Pos2d>{Pos2d.Zero};
            public override int GetEntityState()=>0;
            public override string GetMarkNameKey()=>"region_13_38";
            public override MiniMapMarkTextKeyArg GetMarkDescTextArgKey()=>new MiniMapMarkTextKeyArg("{0}",description);
            public override string GetDialogIconPrefabPath()=>null;
            public override void InitDialogIconHandler(IMapEntityDialogIconHandler[] handlers)=>throw new InvalidOperationException();
            public override bool InteractPossible()=>true;
            public override void OnClick()=>trace.Add("click");
        }
        private sealed class WindowHost:IMap13PopupWindowHost
        {
            public FullScreenAction OnWindowsOpening{get;}=new FullScreenAction();
            public FullScreenAction OnWindowOpened{get;}=new FullScreenAction();
            public FullScreenAction OnWindowClosing{get;}=new FullScreenAction();
            public FullScreenAction OnWindowClosed{get;}=new FullScreenAction();
            internal readonly List<string> Events=new List<string>();
            public WindowHost(){OnWindowsOpening+=_=>Events.Add("opening");OnWindowOpened+=_=>Events.Add("opened");OnWindowClosing+=_=>Events.Add("closing");OnWindowClosed+=_=>Events.Add("closed");}
            public void Close(GameObject window,bool instant){Events.Add("mask-close");window.GetComponent<PopupWindow>().Hide(instant).Forget();}
        }
        private sealed class BlurCapture:IBlurManager,IDisposable
        {
            private readonly Camera camera;private readonly Material material;
            private readonly HashSet<RenderTexture> live=new HashSet<RenderTexture>();
            public int Captures,Releases;
            public BlurCapture(Camera camera,Material source){this.camera=camera;material=new Material(source);}
            public RenderTexture GetRenderTexture(){var target=RenderTexture.GetTemporary(camera.targetTexture.width/13,camera.targetTexture.height/13,0);Require(live.Add(target),"Independent original blur texture lease");return target;}
            public void TakeAShot(RenderTexture target){Require(live.Contains(target),"Capture only a leased blur target");camera.Render();Zjcs.Presentation.World.OfficialDungeonPopupMask.Blur(camera.targetTexture,target,material);Captures++;}
            public void ReleaseRenderTexture(RenderTexture target){Require(live.Remove(target),"Release each original blur texture once");RenderTexture.ReleaseTemporary(target);Releases++;}
            public void Dispose(){Require(live.Count==0&&Captures==Releases,"All original blur captures released");Object.Destroy(material);}
        }
        [Serializable] private sealed class Proof{public string editor;public int prefabs,mask_prefabs,blur_captures,objects,components,sprites,animation_clips,pointer_clicks,rendered_pixels,window_states,queue_tasks;public bool complete_native_hierarchy,all_references,managed_references,original_window_events,pause_resume,mask_time_guard,instant,reentry,cancellation_finally,hide_ignores_pause,original_action_order,original_audio_playback,resource_disposal;}
        private static void Require(bool value,string detail){if(!value)throw new InvalidDataException(detail);}
        private static void Observe(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange value){if(value==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();routines.Clear();routines.Push(routine);EditorApplication.update+=Advance;}}
        private static void Advance(){try{while(routines.Count>0){var current=routines.Peek();if(!current.MoveNext()){routines.Pop();continue;}if(current.Current is IEnumerator nested)routines.Push(nested);return;}Require(errors==0,"Runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static IEnumerator Until(Func<bool> condition,string detail,float limit=5){float start=Time.realtimeSinceStartup;while(!condition()){Require(Time.realtimeSinceStartup-start<limit,detail);yield return null;}}
        private static IEnumerator Verify()
        {
            Application.runInBackground=true;
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            int objects=0,components=0;
            var catalog=new MainlineMiniMapMarkerCatalog(Resources.Load<TextAsset>("MainlineMiniMap/current-marker-data"),Resources.Load<NativeMap13AssetCatalog>("MainlineMiniMap/current-marker-assets"));
            var prefabs=new[]{typeof(MiniMapDisplayMarkDialog),typeof(MiniMapEntryMarkDialog),typeof(MiniMapWayPointMarkDialog)}.Select(catalog.LoadDialogPrefab).OrderBy(p=>p.name).ToArray();Require(prefabs.Length==3,"Three original dialog prefabs through Player resources");
            var nativeCatalog=Resources.Load<NativeMap13AssetCatalog>("MainlineMiniMap/mark-dialog-assets");Require(nativeCatalog&&nativeCatalog.Bindings.Length==21,"Complete Player dialog resource closure");
            foreach(var binding in nativeCatalog.Bindings.Where(row=>row.Asset is Sprite))Require(catalog.LoadDialogSprite(binding.OfficialPath)==binding.Asset,"Original sprite address resolves the bound native object");
            foreach(var prefab in prefabs.Concat(new[]{catalog.LoadDialogBlurMask()}))
            {
                Require(!PrefabUtility.HasManagedReferencesWithMissingTypes(prefab),"Original managed references: "+prefab.name);
                foreach(var transform in prefab.GetComponentsInChildren<Transform>(true)){objects++;Require(transform.GetComponents<MonoBehaviour>().All(c=>c),"Every original MonoBehaviour: "+transform.name);}
                foreach(var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    components++;var property=new SerializedObject(component).GetIterator();
                    while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference)Require(property.objectReferenceValue||property.objectReferenceInstanceIDValue==0,"Original object reference: "+prefab.name+":"+property.propertyPath);
                }
            }
            var labels=new DescriptionLabels();var previousFormatter=MiniMapMarkLanguage.Formatter;MiniMapMarkLanguage.Formatter=(key,args)=>LanguageManager.Instance.GetTextFormated(key,args);
            var sound=new GameObject("OriginalNativeDialogSoundHost").AddComponent<OfflineProfessionSoundHost>();sound.Bind(AssetDatabase.LoadAssetAtPath<OfflineProfessionAudioCatalog>("Assets/Official/ProfessionAudio/ProfessionAudio.asset"));
            var camera=new GameObject("OriginalNativeDialogCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=500;camera.transform.position=new Vector3(0,0,-100);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.06f,.06f,1);
            var target=new RenderTexture(720,1280,24);camera.targetTexture=target;
            var canvas=new GameObject("OriginalWindowContainerFixture",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));var cv=canvas.GetComponent<Canvas>();cv.renderMode=RenderMode.ScreenSpaceCamera;cv.worldCamera=camera;cv.planeDistance=100;var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
            var staging=new GameObject("OriginalWindowInjectionFixture");staging.SetActive(false);
            var eventSystem=new GameObject("SimulatedPointer",typeof(EventSystem)).GetComponent<EventSystem>();
            var map=new GameObject("OriginalMarkerDialogFixture").AddComponent<Map13NativeDialogFixture>();map.Background=catalog.LoadDialogSprite("UI/ItemNotes/BigMapStageBg/map_11_stage_bg");var data=new Data(map.Trace,labels.Text("region_13_38"));int clicks=0,pixels=0,states=0;
            var background=new GameObject("OriginalMapBackgroundFixture",typeof(RectTransform),typeof(Image));background.transform.SetParent(canvas.transform,false);var backgroundRect=(RectTransform)background.transform;backgroundRect.anchorMin=Vector2.zero;backgroundRect.anchorMax=Vector2.one;backgroundRect.sizeDelta=Vector2.zero;background.GetComponent<Image>().sprite=map.Background;background.GetComponent<Image>().raycastTarget=false;background.transform.SetAsFirstSibling();
            var maskGo=Object.Instantiate(catalog.LoadDialogBlurMask(),canvas.transform,false);var mask=maskGo.GetComponent<WindowBlurMask>();var maskImage=maskGo.GetComponent<RawImage>();var maskButton=maskGo.GetComponent<Button>();Require(mask&&maskImage&&maskButton&&mask.MaskType==WindowMaskType.BlurMask&&maskImage.uvRect==new Rect(0,0,1,1),"Original complete blur mask and UV fields");Require(maskGo.GetComponents<Component>().Length==7&&maskGo.transform.childCount==0,"Original blur mask hierarchy");
            var capture=new BlurCapture(camera,catalog.LoadDialogBlurMaterial());mask.BlurManager=capture;maskGo.SetActive(true);yield return null;maskGo.SetActive(false);
            foreach(var prefab in prefabs)
            {
                var host=new WindowHost();var go=Object.Instantiate(prefab,staging.transform,false);labels.Bind(go);var window=go.GetComponent<PopupWindow>();window.WindowManager=host;var dialog=go.GetComponent<MiniMapMarkDialogBase>();Require(dialog&&window,"Original dialog/window components");go.transform.SetParent(canvas.transform,false);go.SetActive(true);yield return null;go.SetActive(false);
                MiniMapMarkUIBase mark=prefab.name.Contains("WayPoint")?new GameObject("OriginalWaypointFixture").AddComponent<MiniMapWayPointMarkUI>():prefab.name.Contains("Entry")?new GameObject("OriginalEntryFixture").AddComponent<MiniMapEntryMarkUI>():new GameObject("OriginalDisplayFixture").AddComponent<MiniMapDisplayMarkUI>();mark.Init(map,data);dialog.Init(mark);
                var clips=new List<AnimationClip>();window.GetAnimationClips(clips);Require(clips.Count==2&&clips.All(c=>c&&c.length>0),"Original show and hide clips");Require(window.WindowType==WindowType.Dialog&&window.IsDuplicate&&window.CloseByClickMask&&!window.FullScreen&&window.FullScreenDisplay&&window.MaskType==WindowMaskType.BlurMask,"Original popup flags and blur-mask full display");
                window.SetCanvasSortingOrder(10);var prepare=mask.Prepare(window,false,System.Threading.CancellationToken.None);Require(!prepare.Status.IsCompleted(),"Original blur capture waits for the next frame");yield return Until(()=>prepare.Status.IsCompleted(),"Original blur capture prepared");prepare.GetAwaiter().GetResult();
                Action<PopupWindow> attach=w=>mask.AttachToWindow(w),detach=w=>mask.UnattachToWindow();Action<GameObject> refresh=_=>mask.Refresh();window.OnShowing+=attach;window.OnHide+=detach;window.OnPauseEvent+=refresh;window.OnResumeEvent+=refresh;
                var show=window.Show();Require(window.State==PopupWindow.EState.Showing&&window.IsVisible,"Original showing state");states++;Require(mask.AttachWindow==window&&maskGo.activeSelf&&mask.Canvas.sortingOrder==window.Canvas.sortingOrder-1&&maskImage.texture,"Original mask attach, sorting and captured frame");
                window.OnMaskClick();Require(!host.Events.Contains("mask-close"),"Original half-second mask guard");
                Require(window.Pause()&&!window.Pause()&&window.IsPause&&!window.CanvasGroup.interactable&&!window.CanvasGroup.blocksRaycasts&&window.CanvasGroup.alpha==0&&!mask.Canvas.enabled,"Original pause idempotence and canvas/mask state");float pauseUntil=Time.realtimeSinceStartup+clips[0].length+.1f;while(Time.realtimeSinceStartup<pauseUntil)yield return null;Require(window.State==PopupWindow.EState.Showing,"Original paused timer");Require(window.Resume()&&!window.Resume()&&!window.IsPause&&window.CanvasGroup.interactable&&window.CanvasGroup.blocksRaycasts&&window.CanvasGroup.alpha==1&&mask.Canvas.enabled,"Original resume idempotence");
                yield return Until(()=>show.Status.IsCompleted(),"Original show animation completes");show.GetAwaiter().GetResult();Require(window.State==PopupWindow.EState.Show&&host.Events.SequenceEqual(new[]{"opening","opened"}),"Original open event order");states++;window.Show(true).GetAwaiter().GetResult();Require(host.Events.Count==2,"Already visible window does not repeat events");
                for(int frame=0;frame<5;frame++)yield return null;Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(720,1280,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,720,1280),0,0);image.Apply(false,false);RenderTexture.active=previous;int count=image.GetPixels32().Count(p=>p.r>30||p.g>30||p.b>30);Require(count>5000,"Actual native dialog render: "+prefab.name);pixels+=count;File.WriteAllBytes(Path.Combine(Workspace,prefab.name+".png"),image.EncodeToPNG());Object.Destroy(image);
                Require(dialog.myTitle.Text.Contains(labels.Text("region_13_38")),"Original Chinese title");
                map.Trace.Clear();
                if(dialog is MiniMapFunctionMarkDialog function)
                {
                    var point=RectTransformUtility.WorldToScreenPoint(camera,((RectTransform)function.Btn.transform).TransformPoint(((RectTransform)function.Btn.transform).rect.center));var pointer=new PointerEventData(eventSystem){position=point,button=PointerEventData.InputButton.Left};var results=new List<RaycastResult>();eventSystem.RaycastAll(pointer,results);Require(results.Count>0,"Native UI pointer raycast");var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(results[0].gameObject);Require(handler==function.Btn.gameObject,"Native topmost hit resolves original action button: "+(handler?handler.name:"none"));pointer.pointerPressRaycast=results[0];pointer.pointerCurrentRaycast=results[0];ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerClickHandler);Require(map.Trace.SequenceEqual(new[]{"click","close","fullscreen"}),"Original click/close/fullscreen sequence");clicks++;
                    Require(Object.FindObjectsOfType<AudioSource>().Any(a=>a.clip&&a.isPlaying),"Actual original Wwise-event PCM playback");
                }
                else {var pointer=new PointerEventData(eventSystem){position=new Vector2(10,10),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();eventSystem.RaycastAll(pointer,hits);Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==maskButton.gameObject,"Original mask receives the topmost outside-window pointer");ExecuteEvents.Execute(maskButton.gameObject,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(maskButton.gameObject,pointer,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(maskButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);clicks++;}
                yield return Until(()=>window.State==PopupWindow.EState.Hide,"Original hide completes");Require(!window.IsVisible&&host.Events.TakeLast(2).SequenceEqual(new[]{"closing","closed"}),"Original hidden state and events");states+=2;
                Require(!maskGo.activeSelf&&mask.AttachWindow==null&&capture.Captures==capture.Releases,"Original mask unattach releases its expired capture");window.OnShowing-=attach;window.OnHide-=detach;window.OnPauseEvent-=refresh;window.OnResumeEvent-=refresh;
                int prior=host.Events.Count;window.Hide(true).GetAwaiter().GetResult();Require(host.Events.Count==prior,"Already hidden guard");window.Show(true).GetAwaiter().GetResult();Require(window.State==PopupWindow.EState.Show&&window.IsVisible,"Original instant reentry");window.Hide(true).GetAwaiter().GetResult();Require(window.State==PopupWindow.EState.Hide&&!window.IsVisible,"Original instant hide");states+=2;
                host.Events.Clear();var interrupted=window.Show();var hiding=window.Hide();yield return Until(()=>interrupted.Status.IsCompleted()&&hiding.Status.IsCompleted(),"Original interrupted-show queue");bool cancelled=false;try{interrupted.GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}hiding.GetAwaiter().GetResult();Require(cancelled&&window.State==PopupWindow.EState.Hide&&host.Events.SequenceEqual(new[]{"opening","opened","closing","closed"}),"Original cancellation retains finally/open events before queued hide");states+=4;
                window.Show(true).GetAwaiter().GetResult();var unpausedHide=window.Hide();Require(window.Pause(),"Pause while hiding");yield return Until(()=>unpausedHide.Status.IsCompleted(),"Original hide timer ignores pause");unpausedHide.GetAwaiter().GetResult();Require(window.State==PopupWindow.EState.Hide&&window.IsPause&&window.Resume(),"Original hide supportPause=false");states+=4;
                Object.Destroy(go);Object.Destroy(mark.gameObject);yield return null;
            }
            Require(map.Loads==map.Disposed&&map.Loads==3,"Original background asset leases released");int tasks=0;using(var queue=new UniTaskPipeline()){var gate=new UniTaskCompletionSource();var one=queue.StartTask(async()=>{tasks++;await gate.Task;tasks++;});var two=queue.StartTask(()=>{tasks++;return UniTask.CompletedTask;});Require(tasks==1&&queue.IsProcessing&&queue.PendingTaskCount==1,"Original serialized task queue");gate.TrySetResult();yield return Until(()=>two.Status.IsCompleted(),"Queued tasks finish");one.GetAwaiter().GetResult();two.GetAwaiter().GetResult();Require(tasks==3&&!queue.IsProcessing&&queue.PendingTaskCount==0,"Original task order");queue.WaitForCompletionAsync().GetAwaiter().GetResult();}
            capture.Dispose();Object.Destroy(map.gameObject);Object.Destroy(staging);Object.Destroy(canvas);Object.Destroy(eventSystem.gameObject);Object.Destroy(sound.gameObject);camera.targetTexture=null;Object.Destroy(target);Object.Destroy(camera.gameObject);yield return null;labels.Dispose();MiniMapMarkLanguage.Formatter=previousFormatter;
            File.WriteAllText(Path.Combine(Workspace,"minimap-mark-dialogs-rules.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,prefabs=3,mask_prefabs=1,blur_captures=capture.Captures,objects=objects,components=components,sprites=17,animation_clips=7,pointer_clicks=clicks,rendered_pixels=pixels,window_states=states,queue_tasks=tasks,complete_native_hierarchy=true,all_references=true,managed_references=true,original_window_events=true,pause_resume=true,mask_time_guard=true,instant=true,reentry=true,cancellation_finally=true,hide_ignores_pause=true,original_action_order=true,original_audio_playback=true,resource_disposal=true},true));Debug.Log("MAP13_MINIMAP_MARK_DIALOGS_PASS");
        }
    }
}
