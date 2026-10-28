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
using Zjcs.Offline.StoryBubbles;
using Zjcs.Official.StoryBubbles;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14BubbleAcceptance
    {
        private const string Key = "Zjcs.Map14.OriginalBubble";
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
                Debug.Log("Map14 original bubble Play Mode passed: native prefab, fixed/interval typewriter interruption/completion, original four entity Timeline texts, original following offsets, click/autoplay queue, HUD order, disable cleanup and two portrait renders. Formal map/Timeline/save/release remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidDataException("Original bubble: " + message); }
        private static IEnumerator Steps()
        {
            Application.runInBackground = true;
            var camera = new GameObject("Original bubble world camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 5;
            camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = new Color(.08f,.08f,.08f); camera.clearFlags = CameraClearFlags.SolidColor;
            var events = new GameObject("Bubble simulated input", typeof(EventSystem), typeof(StandaloneInputModule));
            var mouse = events.AddComponent<SimulatedMouse>(); events.GetComponent<StandaloneInputModule>().inputOverride = mouse;
            EventSystem.current.sendNavigationEvents = false;
            var canvas = new GameObject("Original UI host", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080,1920); scaler.matchWidthOrHeight = .5f;
            var host = new Fixture(); BubbleServices.Current = host;
            var entity = new GameObject("Bound original entity 8453").AddComponent<BubbleEntityBinding>(); entity.ClassId = 8453;
            host.Entities.Add(entity); host.SelfObject = entity.gameObject;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OfficialMap14BubbleTools.Destination + "/BubbleDialog.prefab");
            Require(prefab != null && prefab.GetComponentsInChildren<Transform>(true).Length == 10, "native hierarchy loads");
            var originalFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OfficialMap14BubbleTools.FontPath);
            int originalGlyphs=originalFont.glyphTable.Count, originalCharacters=originalFont.characterTable.Count;
            {
                foreach (int width in new[] {450,720})
                {
                    SetView(width, width * 16 / 9); yield return null; yield return null;
                    var dialog = UnityEngine.Object.Instantiate(prefab, canvas.transform, false).GetComponent<BubbleDialog>();
                    var texts = dialog.GetComponentsInChildren<WorldMultiLanguageText>(true);
                    foreach (var text in texts) text.Localizer = key => key;
                    yield return null;
                    var normal = texts.Single(t => t.transform.parent.parent.name == "Normal");
                    var writer = normal.GetComponent<TMP_TypeWriterEffect>(); var tmp = normal.GetComponent<TMP_Text>();
                    dialog.Play(16,0,entity, .6f);
                    Require(normal.Text == host.Localize("bubble_dialog_16_0") && writer.IsPlaying, "original chant and fixed duration start");
                    Require(tmp.maxVisibleCharacters < tmp.text.Length, "original partial text is rendered");
                    yield return new WaitForSeconds(.15f);
                    Require(writer.IsPlaying && tmp.maxVisibleCharacters < tmp.text.Length, "clip duration controls typewriter");
                    yield return new WaitForSeconds(.6f);
                    Require(!writer.IsPlaying && tmp.maxVisibleCharacters == int.MaxValue, "fixed duration completes");
                    Vector3 before = dialog.GetComponentInChildren<AttachToObjUI>().transform.position;
                    entity.transform.position += Vector3.right; yield return null; yield return null;
                    Require((dialog.GetComponentInChildren<AttachToObjUI>().transform.position - before).sqrMagnitude > 1, "LateUpdate follows the bound entity");
                    entity.transform.position = Vector3.zero;
                    foreach (var row in host.Rows)
                    {
                        dialog.Play(row.Id,row.Order,entity, .15f); yield return new WaitForSeconds(.2f);
                        Require(normal.Text == host.Localize("bubble_dialog_"+row.Id+"_"+row.Order), "original localized Timeline row");
                        yield return Capture(width + "-" + row.Id + "-" + row.Order, tmp);
                    }
                    int completed=0, interrupted=0;
                    writer.StartTypeWithFixedDuration(1, stopped => { if(stopped) interrupted++; else completed++; });
                    yield return null; writer.FastEndType();
                    Require(interrupted==1 && completed==0 && !writer.IsPlaying, "fast end preserves interruption callback");
                    writer.StartType(.01f, stopped => { if(stopped) interrupted++; else completed++; });
                    yield return new WaitForSeconds(.2f);
                    Require(completed==1 && interrupted==1, "interval path completes once");
                    dialog.Init(16,.05f,true,true,true);
                    Require(normal.Text == host.Localize("bubble_dialog_16_0") && !dialog.TalkFinish, "queue begins with order zero");
                    var button=dialog.transform.Find("NextBtn").GetComponent<Button>();
                    yield return Click(mouse,button);
                    Require(normal.Text == host.Localize("bubble_dialog_16_1") && !dialog.TalkFinish, "simulated mouse advances original queue");
                    yield return Click(mouse,button);
                    Require(dialog.TalkFinish && !dialog.gameObject.activeSelf && host.Closed==1, "last click closes original window");
                    Require(host.Calls.TakeLast(4).SequenceEqual(new[]{"home+","world+","bottom+","player+"}), "original HUD restore order");
                    UnityEngine.Object.Destroy(dialog.gameObject); yield return null; host.Closed=0; host.Calls.Clear();
                    dialog=UnityEngine.Object.Instantiate(prefab,canvas.transform,false).GetComponent<BubbleDialog>();
                    foreach(var text in dialog.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer=k=>k;
                    yield return null;
                    dialog.Init(16,.05f,true,false,false); yield return new WaitForSeconds(.25f);
                    Require(dialog.TalkFinish && host.Closed==1, "original autoplay completes and closes");
                    UnityEngine.Object.Destroy(dialog.gameObject); yield return null;
                    host.Closed=0; host.Calls.Clear();
                }
            }
            Require(originalFont.glyphTable.Count==originalGlyphs && originalFont.characterTable.Count==originalCharacters && originalFont.atlasTextures[0].width==0,"runtime glyph generation keeps original source assets unchanged");
            BubbleServices.Current = null;
        }
        private static IEnumerator Click(SimulatedMouse input, Button target)
        {
            Canvas.ForceUpdateCanvases(); var rect=target.GetComponent<RectTransform>();
            input.point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=input.point},hits);
            Require(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==target.gameObject,"original next button is unobscured");
            yield return null; input.down=true;input.held=true;yield return null;
            input.down=false;input.up=true;input.held=false;yield return null;input.up=false;yield return null;
        }
        private static IEnumerator Capture(string name, TMP_Text text)
        {
            yield return new WaitForEndOfFrame(); var image=ScreenCapture.CaptureScreenshotAsTexture();
            foreach (var character in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.character=='…'))
            {
                Vector2 min=RectTransformUtility.WorldToScreenPoint(null,text.transform.TransformPoint(character.bottomLeft));
                Vector2 max=RectTransformUtility.WorldToScreenPoint(null,text.transform.TransformPoint(character.topRight));
                int pixels=0;
                for(int y=Mathf.Max(0,Mathf.FloorToInt(min.y));y<Mathf.Min(image.height,Mathf.CeilToInt(max.y));y++)
                    for(int x=Mathf.Max(0,Mathf.FloorToInt(min.x));x<Mathf.Min(image.width,Mathf.CeilToInt(max.x));x++)
                    {var p=image.GetPixel(x,y);if(p.r<.4f&&p.g<.4f&&p.b<.4f)pixels++;}
                Require(character.isVisible && pixels>0,"original ellipsis has actual rendered pixels");
            }
            Directory.CreateDirectory("output/map14-bubble-ui");File.WriteAllBytes("output/map14-bubble-ui/"+name+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
        }
        private static void SetView(int width,int height)
        {
            var editor=typeof(EditorWindow).Assembly;var sizesType=editor.GetType("UnityEditor.GameViewSizes");
            var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
            var size=Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize"),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"),1),width,height,"Original Map14 bubble"},null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;
            var type=editor.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type,false,"Game",false);
            type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,index);view.Repaint();
        }
        private sealed class Runner : MonoBehaviour { }
        public sealed class SimulatedMouse:BaseInput
        {
            public Vector2 point;public bool down,held,up;
            public override bool mousePresent=>true;public override Vector2 mousePosition=>point;
            public override Vector2 mouseScrollDelta=>Vector2.zero;
            public override bool GetMouseButtonDown(int b)=>b==0&&down;
            public override bool GetMouseButtonUp(int b)=>b==0&&up;
            public override bool GetMouseButton(int b)=>b==0&&held;
            public override float GetAxisRaw(string a)=>0;public override bool GetButtonDown(string b)=>false;
        }
        private sealed class Fixture : IBubbleServices
        {
            public List<BubbleEntityBinding> Entities=new List<BubbleEntityBinding>();
            public List<BubbleDialogInfo> Rows=new List<BubbleDialogInfo>();
            public List<string> Calls=new List<string>();public int Closed;
            private Dictionary<string,object> texts;
            private GameObject home=new GameObject("Home host hook"),world=new GameObject("World host hook");
            public Fixture()
            {
                var data=(Dictionary<string,object>)OfficialEquipmentJson.Parse(File.ReadAllText("Assets/Official/World/Map/14/Source/entity-timelines.json"),true);
                texts=(Dictionary<string,object>)data["localization"];
                foreach(Dictionary<string,object> r in (List<object>)data["bubbles"])
                {var offset=(Dictionary<string,object>)r["OffsetToRoot"];Rows.Add(new BubbleDialogInfo{Id=Convert.ToInt32(r["Id"]),Order=Convert.ToInt32(r["Order"]),EntityClassId=Convert.ToInt32(r["EntityClassId"]),IsSelf=(bool)r["IsSelf"],IsAngry=(bool)r["IsAngry"],OffsetToRoot=new Vector2(Convert.ToSingle(offset["X"]),Convert.ToSingle(offset["Y"]))});}
                world.AddComponent<WorldPanel>();
            }
            public BubbleDialogInfo Get(int id,int order)=>Rows.FirstOrDefault(r=>r.Id==id&&r.Order==order);
            public List<BubbleDialogInfo> GetDialogInfosById(int id)=>Rows.Where(r=>r.Id==id).OrderBy(r=>r.Order).ToList();
            public string Localize(string key)=>(string)texts[key];
            public IEnumerable<BubbleEntityBinding> ServerEntities=>Entities;
            public IEnumerable<BubbleEntityBinding> ClientEntities=>Array.Empty<BubbleEntityBinding>();
            public GameObject SelfObject{get;set;}
            public bool TryGetHomeUI(out GameObject go){go=home;Calls.Add(home.activeSelf?"home-":"home+");return true;}
            public bool TryGetWorldPanel(out WorldPanel panel){panel=world.GetComponent<WorldPanel>();Calls.Add(home.activeSelf?"world+":"world-");return true;}
            public bool HasBottomBar=>true;public bool HasPlayerInfo=>true;
            public void HideBottomBar(object o)=>Calls.Add("bottom-");public void TryShowBottomBar(object o)=>Calls.Add("bottom+");
            public void HidePlayerInfo(object o)=>Calls.Add("player-");public void TryShowPlayerInfo(object o)=>Calls.Add("player+");
            public void Close(GameObject go){Closed++;go.SetActive(false);}
        }
    }
}
