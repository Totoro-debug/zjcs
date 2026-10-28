using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Official.StoryBubbleTimeline;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Zjcs.Official.StoryBubbles;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14BubbleTimelineAcceptance
    {
        private const string Key = "Zjcs.Map14.OriginalBubbleTimeline";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            try
            {
                OfficialMap14BubbleTools.Import();
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            }
            catch(Exception error) {Debug.LogException(error);EditorApplication.Exit(1);return;}
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            started = false; errors = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe; SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map14 original bubble verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original bubble verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Steps());
            while (stack.Count > 0)
            {
                object next;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    next = stack.Peek().Current;
                    if (next is IEnumerator child) { stack.Push(child); continue; }
                }
                catch (Exception e) { Debug.LogException(e); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 original bubble Timeline Play Mode passed: original three tracks/four clip fields and native UI, entity bindings, deferred popup completion, clip duration, pause/close and interval override. Full entity Timeline/camera/animation/audio/map/save/release remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidDataException("Original bubble: " + message); }
        private static IEnumerator Steps()
        {
            Application.runInBackground = true;
            typeof(OfficialMap14BubbleAcceptance).GetMethod("SetView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{720,1280});
            yield return null;yield return null;
            var camera = new GameObject("Entity Timeline camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag="MainCamera"; camera.orthographic=true;camera.orthographicSize=5;
            camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.08f,.08f,.08f);
            var canvas = new GameObject("Original UI host",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
            var data=(Dictionary<string,object>)OfficialEquipmentJson.Parse(File.ReadAllText("Assets/Official/World/Map/14/Source/entity-timelines.json"),true);
            var host=new Host(data,canvas.transform);BubbleServices.Current=host;
            var timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.name="Original bubble tracks verification";
            var director=new GameObject("Original entity Timeline").AddComponent<PlayableDirector>();
            director.timeUpdateMode=DirectorUpdateMode.Manual;director.playableAsset=timeline;
            var source=((List<object>)data["timelines"]).Cast<Dictionary<string,object>>().Single(t=>
                (string)((Dictionary<string,object>)((Dictionary<string,object>)t["asset"])["fields"])["m_Name"]=="SuiMo_StoryShow02Timeline");
            var records=((List<object>)source["objects"]).Cast<Dictionary<string,object>>().ToDictionary(o=>(string)o["path_id"]);
            var clips=new List<TimelineClip>();
            foreach(var record in records.Values.Where(o=>(string)o["type"]=="EntityTrack"))
            {
                var fields=(Dictionary<string,object>)record["fields"];
                Require(!(bool)Convert.ToBoolean(fields["m_Muted"]),"original bubble track is enabled");
                var track=timeline.CreateTrack<EntityTrack>(null,(string)fields["m_Name"]);
                BubbleEntityBinding binding=null;
                foreach(Dictionary<string,object> row in (List<object>)fields["m_Clips"])
                {
                    string id=Convert.ToString(((Dictionary<string,object>)row["m_Asset"])["m_PathID"]);
                    var native=(Dictionary<string,object>)records[id]["fields"];
                    var template=(Dictionary<string,object>)native["Template"];
                    var clip=track.CreateClip<EntityTalkAsset>();var asset=(EntityTalkAsset)clip.asset;
                    asset.name=(string)native["m_Name"];
                    asset.Template.DialogId=Convert.ToInt32(template["DialogId"]);asset.Template.Order=Convert.ToInt32(template["Order"]);
                    asset.Template.WordInterval=Convert.ToSingle(template["WordInterval"]);asset.Template.Instant=Convert.ToBoolean(template["Instant"]);
                    clip.start=Convert.ToDouble(row["m_Start"]);clip.duration=Convert.ToDouble(row["m_Duration"]);
                    clip.clipIn=Convert.ToDouble(row["m_ClipIn"]);clip.timeScale=Convert.ToDouble(row["m_TimeScale"]);
                    clip.easeInDuration=Convert.ToDouble(row["m_EaseInDuration"]);clip.easeOutDuration=Convert.ToDouble(row["m_EaseOutDuration"]);
                    clip.blendInDuration=Convert.ToDouble(row["m_BlendInDuration"]);clip.blendOutDuration=Convert.ToDouble(row["m_BlendOutDuration"]);
                    Require((int)clip.preExtrapolationMode==Convert.ToInt32(row["m_PreExtrapolationMode"])
                        && (int)clip.postExtrapolationMode==Convert.ToInt32(row["m_PostExtrapolationMode"]),"original no-extrapolation clip modes");
                    clip.displayName=(string)row["m_DisplayName"];
                    Require(asset.clipCaps==ClipCaps.None && asset.Template.WordInterval==-1 && !asset.Template.Instant,"original clip template/caps");
                    var config=host.Get(asset.Template.DialogId,asset.Template.Order);
                    if(!binding){binding=new GameObject("Bound entity "+config.EntityClassId).AddComponent<BubbleEntityBinding>();binding.ClassId=config.EntityClassId;host.Entities.Add(binding);}
                    Require(binding.ClassId==config.EntityClassId,"original same-track entity binding");clips.Add(clip);
                }
                director.SetGenericBinding(track,binding);
            }
            Require(timeline.GetOutputTracks().Count()==3 && clips.Count==4,"three original tracks and four original clips");
            yield return null;director.Play();director.Evaluate();yield return null;
            Require(host.Created==0,"no bubble before first clip");
            int shown=0;
            foreach(var clip in clips.OrderBy(c=>c.start))
            {
                director.time=clip.start+.001;director.Evaluate();yield return null;
                host.CompletePending();yield return null;
                Require(host.Created==++shown && host.Active,"one deferred popup per original clip");
                var asset=(EntityTalkAsset)clip.asset;var config=host.Get(asset.Template.DialogId,asset.Template.Order);
                var text=host.Active.GetComponentsInChildren<TMP_Text>(false).Single();
                Require(text.text==host.Localize("bubble_dialog_"+config.Id+"_"+config.Order),"original localized text after completion");
                var writer=text.GetComponent<TMP_TypeWriterEffect>();Require(writer.IsPlaying,"original clip duration drives typing");
                yield return new WaitForSeconds((float)clip.duration+.1f);
                Require(!writer.IsPlaying && text.maxVisibleCharacters==int.MaxValue,"clip duration completes the original writer");
                yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();
                Directory.CreateDirectory("output/map14-bubble-timeline");File.WriteAllBytes("output/map14-bubble-timeline/"+config.Id+"-"+config.Order+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
                director.time=clip.end-.001;director.Evaluate();yield return null;
                Require(host.Active,"window remains before original clip end");
                director.time=clip.end+.001;director.Evaluate();yield return null;
                Require(!host.Active && host.Closed==shown,"OnBehaviourPause closes each original popup once");
            }
            director.Stop();Require(host.Closed==4,"stop does not close a finished popup twice");
            // Cover the original optional interval override and template isolation separately.
            var graph=PlayableGraph.Create("Original bubble interval override");
            var original=new EntityTalkBehaviour{DialogId=16,Order=0,WordInterval=.02f,Instant=false};
            var playable=ScriptPlayable<EntityTalkBehaviour>.Create(graph,original);var behaviour=playable.GetBehaviour();
            behaviour.SetDuration(12);behaviour.BindEntity(host.Entities.Single(e=>e.ClassId==8453));
            behaviour.OnBehaviourPlay(playable,default);host.CompletePending();yield return null;
            Require(host.Active && host.Created==5,"bound interval path opens the original UI");
            behaviour.OnBehaviourPlay(playable,default);Require(host.Created==5,"existing popup is not duplicated");
            yield return new WaitForSeconds(host.Localize("bubble_dialog_16_0").Length*.02f+.1f);
            Require(!host.Active.GetComponentInChildren<TMP_TypeWriterEffect>().IsPlaying,"localized UTF-16 length controls interval duration");
            behaviour.OnBehaviourPause(playable,default);Require(!host.Active && host.Closed==5,"explicit pause closes interval popup");
            var unbound=ScriptPlayable<EntityTalkBehaviour>.Create(graph,original);unbound.GetBehaviour().OnBehaviourPlay(unbound,default);
            Require(host.Created==5,"unbound original entity creates no window");graph.Destroy();
            BubbleServices.Current=null;
        }
        private sealed class Runner:MonoBehaviour{}
        private sealed class Operation:IBubblePopupOperation
        {
            public event Action<GameObject> OnComplete;
            public GameObject Window;public void Complete()=>OnComplete?.Invoke(Window);
        }
        private sealed class Host:IBubblePopupServices
        {
            public readonly List<BubbleEntityBinding> Entities=new List<BubbleEntityBinding>();
            private readonly List<BubbleDialogInfo> rows=new List<BubbleDialogInfo>();
            private readonly Dictionary<string,object> texts;
            private readonly Transform canvas;private readonly Queue<Operation> pending=new Queue<Operation>();
            public int Created,Closed;public GameObject Active;
            public Host(Dictionary<string,object> source,Transform canvas)
            {
                this.canvas=canvas;texts=(Dictionary<string,object>)source["localization"];
                foreach(Dictionary<string,object> row in (List<object>)source["bubbles"])
                {var offset=(Dictionary<string,object>)row["OffsetToRoot"];rows.Add(new BubbleDialogInfo{Id=Convert.ToInt32(row["Id"]),Order=Convert.ToInt32(row["Order"]),EntityClassId=Convert.ToInt32(row["EntityClassId"]),IsSelf=(bool)row["IsSelf"],IsAngry=(bool)row["IsAngry"],OffsetToRoot=new Vector2(Convert.ToSingle(offset["X"]),Convert.ToSingle(offset["Y"]))});}
            }
            public IBubblePopupOperation Popup(Action<BubbleDialog> created)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OfficialMap14BubbleTools.Destination+"/BubbleDialog.prefab");
                Active=UnityEngine.Object.Instantiate(prefab,canvas,false);Created++;
                foreach(var text in Active.GetComponentsInChildren<WorldMultiLanguageText>(true))text.Localizer=k=>k;
                created(Active.GetComponent<BubbleDialog>());var operation=new Operation{Window=Active};pending.Enqueue(operation);return operation;
            }
            public void CompletePending(){while(pending.Count>0)pending.Dequeue().Complete();}
            public void Close(GameObject window){Closed++;window.SetActive(false);UnityEngine.Object.Destroy(window);if(Active==window)Active=null;}
            public BubbleDialogInfo Get(int id,int order)=>rows.SingleOrDefault(r=>r.Id==id&&r.Order==order);
            public List<BubbleDialogInfo> GetDialogInfosById(int id)=>rows.Where(r=>r.Id==id).OrderBy(r=>r.Order).ToList();
            public string Localize(string key)=>(string)texts[key];
            public IEnumerable<BubbleEntityBinding> ServerEntities=>Entities;
            public IEnumerable<BubbleEntityBinding> ClientEntities=>Array.Empty<BubbleEntityBinding>();
            public GameObject SelfObject=>null;
            public bool TryGetHomeUI(out GameObject go){go=null;return false;}
            public bool TryGetWorldPanel(out WorldPanel panel){panel=null;return false;}
            public bool HasBottomBar=>false;public bool HasPlayerInfo=>false;
            public void HideBottomBar(object owner){}public void TryShowBottomBar(object owner){}
            public void HidePlayerInfo(object owner){}public void TryShowPlayerInfo(object owner){}
        }
    }
}
