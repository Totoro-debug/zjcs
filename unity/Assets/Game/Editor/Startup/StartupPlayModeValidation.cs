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
using Zjcs.Offline.Startup;
using Zjcs.Presentation.CharacterCreation;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Startup
{
    public static class StartupPlayModeValidation
    {
        private const string Key = "Zjcs.Startup.Validation";
        private static bool started;
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_STARTUP_VALIDATION_OUTPUT")
            ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../.tools/session-startup/captures"));

        public static void Run()
        {
            SessionState.SetString(Key + "Company", PlayerSettings.companyName);
            SessionState.SetString(Key + "Product", PlayerSettings.productName);
            PlayerSettings.companyName = "ZjcsStartupValidation";
            PlayerSettings.productName = Environment.GetEnvironmentVariable("ZJCS_STARTUP_PROBE_PRODUCT") ?? "probe-" + Guid.NewGuid().ToString("N");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + "Pass", Environment.GetEnvironmentVariable("ZJCS_STARTUP_PROBE_RESTART") == "1" ? 1 : 0);
            SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 240f);
            ConfigureView(450, 800);
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            Directory.CreateDirectory(Output);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = false;
            EditorApplication.update -= Update; EditorApplication.update += Update;
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "Deadline", 0)) { Finish(new TimeoutException("Startup validation timed out.")); return; }
            if (!EditorApplication.isPlaying || started) return;
            started = true;
            var go = new GameObject("StartupValidationRunner");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            PlayerSettings.companyName = SessionState.GetString(Key + "Company", "LocalOffline");
            PlayerSettings.productName = SessionState.GetString(Key + "Product", "杖剑传说");
            string storage = SessionState.GetString(Key + "Storage", "");
            if (SessionState.GetInt(Key + "Pass", -1) == 2 && storage.Contains("ZjcsStartupValidation") && Directory.Exists(storage)) Directory.Delete(storage, true);
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetInt(Key + "Pass", -1) >= 1 ? 0 : 1);
        }
        private static void Finish(Exception error)
        {
            if (error != null) { Debug.LogException(error); SessionState.SetInt(Key + "Pass", -1); }
            EditorApplication.isPlaying = false;
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Start() => StartCoroutine(Guard(Probe()));
            private IEnumerator Guard(IEnumerator routine)
            {
                while (true)
                {
                    object value;
                    try { if (!routine.MoveNext()) break; value = routine.Current; }
                    catch (Exception error) { Finish(error); yield break; }
                    yield return value;
                }
            }
            private IEnumerator Probe()
            {
                SessionState.SetString(Key + "Storage", Application.persistentDataPath);
                int pass = SessionState.GetInt(Key + "Pass", 0);
                OfflineStartupFlow flow = null;
                while (flow == null) { flow = FindObjectOfType<OfflineStartupFlow>(); yield return null; }
                if (pass == 0)
                {
                    Require(flow.Phase == "BootSplash", "Save selector bypassed the original boot splash.");
                    yield return new WaitForEndOfFrame(); Capture("boot");
                    Button ageButton = null;
                    while (ageButton == null)
                    {
                        ageButton = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b => b.name == "AgeTipBtn" && b.gameObject.activeInHierarchy);
                        yield return null;
                    }
                    while (flow.Phase == "BootSplash") yield return null;
                    Click(ageButton.gameObject);
                    yield return null;
                    Require(flow.HasOpenDialog, "Original age-tip pointer click did not open its dialog.");
                    while (flow.Phase != "Ready") yield return null;
                    Require(flow.Progress == 1f, "Local resource loading did not fill the official progress bar.");
                    yield return new WaitForEndOfFrame(); Capture("age-and-progress");
                    Require(GameObject.Find("AgeTipsDialog(Clone)").GetComponent<RectTransform>().rect.width > 100f,
                        "Original age dialog has a collapsed canvas rectangle.");
                    ConfigureView(675, 1200);
                    yield return null; yield return new WaitForEndOfFrame(); Capture("scaled");
                    ConfigureView(900, 800);
                    yield return null; yield return null; yield return null; yield return new WaitForEndOfFrame(); Capture("wide");
                    CheckAdaptation();
                    ConfigureView(360, 1000);
                    yield return null; yield return null; yield return null; yield return new WaitForEndOfFrame(); Capture("narrow");
                    CheckAdaptation();
                    ConfigureView(675, 1200);
                    yield return null; yield return null; yield return null; yield return new WaitForEndOfFrame();
                    Button confirm = Resources.FindObjectsOfTypeAll<Button>().First(b => b.name == "CommonBtn_12" && b.gameObject.activeInHierarchy);
                    Click(confirm.gameObject);
                    yield return null;
                    yield return new WaitForEndOfFrame(); Capture("loading-bar");
                }
                OfficialSaveSelectionView selector = null;
                while (selector == null || !selector.IsReady) { selector = FindObjectOfType<OfficialSaveSelectionView>(); yield return null; }
                yield return new WaitForEndOfFrame(); Capture(pass == 0 ? "save-selector" : "restart-save-selector");
                Require(FindObjectOfType<OfflineStartupFlow>() == null, "Startup canvases were not released before local input.");
                if (pass == 0)
                {
                    if (!selector.Panel.CreateButton.gameObject.activeInHierarchy)
                    {
                        Click(selector.Panel.transform.Find("BgDialog1 (1)/Content/TabGroup/TabGroup/TabGroup/GiftMailDialogTabGroupItem").gameObject);
                        yield return new WaitForSecondsRealtime(.4f);
                    }
                    Button create = selector.Panel.CreateButton;
                    Click(create.gameObject);
                    yield return new WaitForSecondsRealtime(2f);
                    string saves = Path.Combine(Application.persistentDataPath, "saves");
                    Require(Directory.Exists(saves) && Directory.GetFiles(saves, "character-draft.json", SearchOption.AllDirectories).Length == 1,
                        "Startup handoff did not create one isolated draft through the actual button.");
                    yield return new WaitForEndOfFrame(); Capture("new-save-opening");
                    SessionState.SetInt(Key + "Pass", 1);
                }
                else
                {
                    Require(selector.Panel.RoleItemSlot.Find("SaveSlot_slot-1") != null,
                        "Cold startup did not restore the isolated draft entry.");
                    SessionState.SetInt(Key + "Pass", 2);
                    Debug.Log("Startup cold restart validation passed: original splash/local loading to save selector and isolated draft recovery.");
                }
                Finish(null);
            }
        }

        private static void Click(GameObject control)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = control.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0, "No UI hit for " + control.name);
            GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Require(receiver == control, "Pointer hit " + hits[0].gameObject.name + " instead of " + control.name);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static void Capture(string name)
        {
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(Output, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
        }
        private static void CheckAdaptation()
        {
            foreach (StartupCanvasWidthAdjuster adjuster in Object.FindObjectsOfType<StartupCanvasWidthAdjuster>())
            {
                Vector2 size = adjuster.mContent.rect.size;
                Require(size.x > 0f && size.y > 0f && size.x / size.y >= .445f && size.x / size.y <= .761f,
                    "Original startup aspect limits were not preserved: " + adjuster.name + " " + size);
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void ConfigureView(int width, int height)
        {
            Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1) });
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            object size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1), width, height, "Startup validation" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type viewType = editor.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
        }
    }
}
