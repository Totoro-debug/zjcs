using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;

namespace Zjcs.Presentation.CharacterCreation
{
    // Explicit diagnostic entry, with isolated profiles and process-local input.
    public sealed class CreationAppearanceLayoutProbe : MonoBehaviour
    {
        private static string output;
        private static bool finished, passed;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--zjcs-validate-appearance-layout");
            output = i >= 0 ? args[i+1] : Environment.GetEnvironmentVariable("ZJCS_APPEARANCE_LAYOUT_PROBE");
            if (string.IsNullOrEmpty(output)) return;
            Directory.CreateDirectory(output);
            finished = passed = false;
            AudioListener.volume = 0;
            Application.runInBackground = true;
            var host = new GameObject("CreationAppearanceLayoutProbe");
            DontDestroyOnLoad(host);
            host.AddComponent<CreationAppearanceLayoutProbe>().StartCoroutine(Run());
        }
        private static IEnumerator Run()
        {
            CharacterCreationFlowController controller;
            while ((controller = FindObjectOfType<CharacterCreationFlowController>()) == null) yield return null;
            yield return null;
            var service = CharacterCreationOfflineService.OpenDefault(Path.Combine(output,"profiles"));
            string slot = File.Exists(Path.Combine(output,"slot.txt")) ? File.ReadAllText(Path.Combine(output,"slot.txt")) : service.CreateSlot();
            File.WriteAllText(Path.Combine(output,"slot.txt"),slot);
            CharacterCreationDraft draft = service.OpenDraft(slot);
            draft.page = "appearance";
            if (!File.Exists(Path.Combine(output,"result.txt"))) { draft.tab = "整体"; draft.field = "skin_color"; }
            Set(controller,"service",service); Set(controller,"slotId",slot); Set(controller,"draft",draft);
            Invoke(controller,"ShowCreation");
            yield return new WaitForSecondsRealtime(2);
            yield return new WaitForEndOfFrame();
            try
            {
                Capture("appearance-restored");
                ValidateBounds();
            }
            catch (Exception e) { Finish(false,e); yield break; }
            foreach (string body in new[]{"male","female"})
            {
                draft.body_type = body;
                draft.tab = "整体"; draft.field = "skin_color";
                Invoke(controller,"ShowCreation");
                yield return new WaitForSecondsRealtime(2);
                yield return new WaitForEndOfFrame();
                try
                {
                    ValidateBounds();
                    Capture(body + "-default");
                    var group = Active("AppearanceGroup_" + (body == "male" ? 4 : 14));
                    Click(group);
                }
                catch(Exception e) { Finish(false,e); yield break; }
                try
                {
                    var hairTab = FindObjectsOfType<TMPro.TMP_Text>().First(t => t.text == "头发");
                    Click(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hairTab.gameObject));
                }
                catch(Exception e) { Finish(false,e); yield break; }
                yield return new WaitForSecondsRealtime(.5f);
                yield return new WaitForEndOfFrame();
                try
                {
                    var items = FindObjectsOfType<Toggle>().Where(t => t.name.StartsWith("HairTypeItem_")).OrderBy(t=>t.transform.GetSiblingIndex()).ToArray();
                    Require(items.Length >= 15,"Hair options missing");
                    var catalog = CharacterCreationAssetCatalog.LoadDefault();
                    var options = catalog.GetAppearanceOptions(body,"hairstyle_id");
                    Require(items.Select(t=>t.transform.Find("Background").GetComponent<Image>().sprite).Distinct().Count()==items.Length,"Hair previews repeat");
                    foreach(var item in items)
                    {
                        var option = options.First(o=>item.name=="HairTypeItem_"+o.Id);
                        Require(item.transform.Find("Background").GetComponent<Image>().sprite==catalog.FindSkinIcon(option.SkinName),"Wrong official hair sprite");
                        Require(!item.transform.Find("Background/Select").gameObject.activeSelf,"Unexpected circular selection");
                    }
                    var title = FindObjectsOfType<TMPro.TMP_Text>().First(t=>t.name=="SelectSexTitle");
                    var effect = title.GetComponent<TMPro.TMPEffect>();
                    Require(effect != null && effect.outline && effect.outlineWidth==3f,"Original title outline missing");
                    var chosen = items.First(item => !item.isOn);
                    Click(chosen.gameObject);
                    Require(chosen.isOn && chosen.transform.Find("Checkmark").gameObject.activeSelf,"Hair pointer selection failed");
                    Require(items.Count(t=>t.transform.Find("Checkmark").gameObject.activeSelf)==1,"Multiple hair selections");
                    Invoke(controller,"PersistDraft");
                    var saved = service.OpenDraft(slot);
                    Require((body=="male"?saved.bodies.male:saved.bodies.female).hairstyle_id == (body=="male"?draft.bodies.male:draft.bodies.female).hairstyle_id,"Hair draft did not persist");
                    Canvas.ForceUpdateCanvases();
                    Capture(body+"-hair");
                    var wholeTab = FindObjectsOfType<TMPro.TMP_Text>().First(t => t.text == "整体");
                    Click(ExecuteEvents.GetEventHandler<IPointerClickHandler>(wholeTab.gameObject));
                }
                catch(Exception e) { Finish(false,e); yield break; }
                yield return new WaitForSecondsRealtime(.6f);
                yield return new WaitForEndOfFrame();
                try
                {
                    int expected = body == "male" ? 4 : 14;
                    Require((body == "male" ? draft.bodies.male : draft.bodies.female).appearance_group_id == expected,"Trial pointer did not select outfit");
                    ValidateBounds(); Capture(body + "-trial");
                    Click(Active("FixedColorItem_" + service.GetFixedDyeChoices(1).Last().ColorId));
                    Invoke(controller,"PersistDraft");
                    CharacterCreationDraft saved = service.OpenDraft(slot);
                    Require(saved.body_type == body,"Draft body did not persist");
                    Require((body=="male" ? saved.bodies.male : saved.bodies.female).appearance_group_id==expected,"Trial draft did not persist");
                }
                catch(Exception e) { Finish(false,e); yield break; }
            }
            draft.tab = "头发"; draft.field = "hairstyle_id";
            Invoke(controller,"PersistDraft");
            Finish(true,null);
        }
        private static void ValidateBounds()
        {
            Canvas.ForceUpdateCanvases();
            foreach (Button button in FindObjectsOfType<Button>().Where(b => b.name.StartsWith("AppearanceGroup_") || b.name.StartsWith("FixedColorItem_") || b.name == "CustomColorItem"))
            {
                var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                Require(corners.All(c => c.x >= -1 && c.x <= Screen.width+1),"Horizontal overflow: " + button.name);
                Require(corners.All(c => c.y >= 0 && c.y <= Screen.height),"Vertical overflow: " + button.name);
            }
            foreach (Mask mask in FindObjectsOfType<Mask>().Where(m => m.name == "mask"))
                Require(mask.GetComponent<Image>().sprite != null,"Official circular mask missing");
            foreach (Animation animation in FindObjectsOfType<Animation>().Where(a=>a.name=="SelAnimGo"))
                Require(animation.isPlaying,"Official trial selection loop is not playing");
        }
        private static GameObject Active(string name) => FindObjectsOfType<Transform>().First(t=>t.name==name).gameObject;
        private static void Click(GameObject go)
        {
            var rect = (RectTransform)go.transform;
            var pointer = new PointerEventData(EventSystem.current) {position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
            Require(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==go,"Pointer obstructed: " + go.name);
            ExecuteEvents.Execute(go,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(go,pointer,ExecuteEvents.pointerClickHandler);
        }
        private static void Set(object target,string field,object value) => target.GetType().GetField(field,Flags).SetValue(target,value);
        private static object Invoke(object target,string method) => target.GetType().GetMethod(method,Flags).Invoke(target,null);
        private static void Require(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
        private static void Capture(string name)
        {
            var tex=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG()); Destroy(tex);
        }
        private static void Finish(bool ok,Exception e)
        {
            passed=ok; finished=true;
#if UNITY_EDITOR
            UnityEditor.SessionState.SetInt("AppearanceLayoutProbe.Result",ok?1:-1);
#endif
            File.WriteAllText(Path.Combine(output,"result.txt"),ok?"passed":e.ToString());
            if(e!=null) Debug.LogException(e);
#if !UNITY_EDITOR
            Application.Quit(ok?0:1);
#endif
        }
#if UNITY_EDITOR
        public static void RunEditor()
        {
            try { PrepareEditor(); }
            catch (Exception error) { Debug.LogException(error); UnityEditor.EditorApplication.Exit(1); }
        }
        private static void PrepareEditor()
        {
            // Reflection avoids adding editor-only assembly dependencies to the runtime.
            Type Tool(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
            Tool("Zjcs.Editor.CharacterCreation.CharacterCreationUiTools").GetMethod("RestoreTrialIcons").Invoke(null,null);
            Tool("Zjcs.Editor.ProjectTools").GetMethod("Validate").Invoke(null,null);
            Tool("Zjcs.Editor.Startup.StartupPlayModeValidation").GetMethod("ConfigureView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{450,938});
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            UnityEditor.SessionState.SetBool("AppearanceLayoutProbe",true);
            UnityEditor.SessionState.SetInt("AppearanceLayoutProbe.Result",0);
            Hook(); UnityEditor.EditorApplication.isPlaying=true;
        }
        [UnityEditor.InitializeOnLoadMethod] private static void Hook()
        {
            if (!UnityEditor.SessionState.GetBool("AppearanceLayoutProbe",false)) return;
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.EditorApplication.update += EditorTick;
        }
        private static void EditorTick()
        {
            int result = UnityEditor.SessionState.GetInt("AppearanceLayoutProbe.Result",0);
            if(result==0) return;
            if(UnityEditor.EditorApplication.isPlaying) {UnityEditor.EditorApplication.isPlaying=false;return;}
            if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            passed=result==1;
            UnityEditor.SessionState.SetBool("AppearanceLayoutProbe",false);
            UnityEditor.EditorApplication.update-=EditorTick;
            UnityEditor.EditorApplication.Exit(passed?0:1);
        }
        public static void BuildPlayerForValidation()
        {
            string path=Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"Player/Zjcs.exe");
            bool scriptsOnly=Environment.GetEnvironmentVariable("ZJCS_WINDOWS_SCRIPTS_ONLY")=="1";
            if(scriptsOnly && !File.Exists(path)) throw new InvalidOperationException("A scripts-only update requires this session's complete Player.");
            UnityEditor.PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,UnityEditor.ScriptingImplementation.Mono2x);
            var report=UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions { scenes=new[]{"Assets/Game/Scenes/Bootstrap.unity"},locationPathName=path,target=UnityEditor.BuildTarget.StandaloneWindows64,options=scriptsOnly?UnityEditor.BuildOptions.BuildScriptsOnly:UnityEditor.BuildOptions.None });
            UnityEditor.EditorApplication.Exit(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded?0:1);
        }
#endif
    }
}
