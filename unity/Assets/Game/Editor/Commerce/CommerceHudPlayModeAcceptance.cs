using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.Commerce
{
    /// <summary>Play Mode acceptance for the Map10 HUD commerce entry and first-pay icon.</summary>
    public static class CommerceHudPlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.CommerceHud.Acceptance.Active";
        private const string ResultKey = "Zjcs.CommerceHud.Acceptance.Result";
        private const string SlotKey = "Zjcs.CommerceHud.Acceptance.Slot";
        private const string DeadlineKey = "Zjcs.CommerceHud.Acceptance.Deadline";
        private static bool started;

        [MenuItem("Zjcs/Validate/Commerce HUD Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running commerce HUD acceptance.");
            ConfigurePortraitGameView();
            var slots = new ProfileSlotCatalog(Application.persistentDataPath);
            string slot = slots.CreateSlot();
            OfflineMainlineSave save = new OfflineMainlineSave();
            save.CreatedAtUnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "商城HUD验收";
            save.Player.Level = 30;
            save.Player.Wardrobe.Profession = 1;
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 15, Count = 200 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 16, Count = 60 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 2, Count = 20 });
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            SessionState.SetString(SlotKey, slot);
            SessionState.SetInt(ResultKey, 2);
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 600f);
            SessionState.SetBool(ActiveKey, true);
            started = false;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.update -= OnUpdate;
                EditorApplication.update += OnUpdate;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= OnUpdate;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                string slot = SessionState.GetString(SlotKey, string.Empty);
                if (!string.IsNullOrEmpty(slot))
                {
                    try
                    {
                        var slots = new ProfileSlotCatalog(Application.persistentDataPath);
                        string directory = slots.GetSlotDirectory(slot);
                        if (Directory.Exists(directory)) Directory.Delete(directory, true);
                    }
                    catch (Exception error) { Debug.LogWarning("Commerce HUD cleanup: " + error.Message); }
                }
                bool passed = SessionState.GetInt(ResultKey, 1) == 0;
                SessionState.SetBool(ActiveKey, false);
                if (Application.isBatchMode
                    || Environment.GetEnvironmentVariable("ZJCS_COMMERCE_HUD_AUTOMATION") == "1")
                    EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Commerce HUD acceptance exceeded its deadline."));
                return;
            }
            if (started) return;
            started = true;
            var runner = new GameObject("CommerceHudAcceptanceRunner");
            UnityEngine.Object.DontDestroyOnLoad(runner);
            runner.AddComponent<Runner>().StartCoroutine(Guarded(Scenario()));
        }

        public sealed class Runner : MonoBehaviour { }

        private static IEnumerator Guarded(IEnumerator scenario)
        {
            var stack = new System.Collections.Generic.Stack<IEnumerator>();
            stack.Push(scenario);
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                object value;
                bool moved;
                try
                {
                    moved = current.MoveNext();
                    value = moved ? current.Current : null;
                }
                catch (Exception error)
                {
                    Finish(false, error);
                    yield break;
                }
                if (!moved) { stack.Pop(); continue; }
                if (value is IEnumerator nested) { stack.Push(nested); continue; }
                yield return value;
            }
        }

        private static IEnumerator Scenario()
        {
            string slot = SessionState.GetString(SlotKey, string.Empty);
            foreach (Canvas existing in UnityEngine.Object.FindObjectsOfType<Canvas>())
                existing.gameObject.SetActive(false);
            var root = new GameObject("CommerceHudUiRoot");
            var catalog = OfficialMap10UiCatalog.LoadDefault();
            var renderer = new OfficialWorldUiRenderer(catalog);
            GameObject canvas = renderer.Mount(root.transform);
            yield return null;

            var runtimeObject = new GameObject("CommerceHudRuntime");
            var runtime = runtimeObject.AddComponent<OfficialMainlineMapRuntime>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(OfficialMainlineMapRuntime).GetField("slotId", flags).SetValue(runtime, slot);
            typeof(OfficialMainlineMapRuntime).GetField("ui", flags).SetValue(runtime, renderer);
            typeof(OfficialMainlineMapRuntime).GetMethod("CreateCommerceHudEntries", flags).Invoke(runtime, null);
            yield return null;

            GameObject firstEntry = null;
            foreach (Transform transform in UnityEngine.Object.FindObjectsOfType<Transform>(true))
                if (transform.name == "CommerceFirstPayEntry") { firstEntry = transform.gameObject; break; }
            Require(firstEntry != null, "The HUD first-pay entry was not created.");
            Require(firstEntry.activeInHierarchy, "The HUD first-pay entry should be visible for a new profile.");
            Transform title = firstEntry.transform.Find("TitleTxt");
            Require(title != null && title.GetComponent<TMPro.TMP_Text>() != null
                && title.GetComponent<TMPro.TMP_Text>().text == "首购", "The HUD first-pay title is wrong.");
            Transform redDot = firstEntry.transform.Find("RedDotGo");
            Require(redDot != null && redDot.gameObject.activeInHierarchy, "The HUD first-pay red dot is missing.");

            string hudCapture = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "output", "commerce-hud-entry.png");
            if (!Application.isBatchMode)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(hudCapture));
                if (File.Exists(hudCapture)) File.Delete(hudCapture);
                ScreenCapture.CaptureScreenshot(hudCapture);
                yield return null;
                yield return null;
                Debug.Log("[Commerce HUD acceptance] snapshot " + hudCapture);
            }

            GameObject entryButton = renderer.Find("WorldCanvas",
                "/WorldCanvas/Content/Content/WorldPanel/Anim/Right/FunctionEntryGroup/FunctionEntryPhoto/Content/Entry/EntryBtnFolded");
            Require(entryButton != null, "The official HUD function entry button is missing.");
            const string entryPath = "/WorldCanvas/Content/Content/WorldPanel/Anim/Right/FunctionEntryGroup/FunctionEntryPhoto/Content/Entry/EntryBtnFolded";
            typeof(OfficialMainlineMapRuntime).GetMethod("BindWorldControl", flags)
                .Invoke(runtime, new object[] { entryPath, entryButton });
            Button button = entryButton.GetComponent<Button>();
            Require(button != null, "The official HUD function entry has no Button.");
            if (EventSystem.current == null)
                new GameObject("CommerceHudEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            ExecuteEvents.Execute(entryButton, new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            }, ExecuteEvents.pointerClickHandler);
            double deadline = Time.realtimeSinceStartupAsDouble + 30.0;
            CommerceScreenController shop = null;
            while (shop == null && Time.realtimeSinceStartupAsDouble < deadline)
            {
                shop = UnityEngine.Object.FindObjectOfType<CommerceScreenController>();
                yield return null;
            }
            Require(shop != null && shop.IsOpen, "Clicking the official HUD function entry did not open the shop.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            string capture = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "output", "commerce-hud.png");
            if (!Application.isBatchMode)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(capture));
                if (File.Exists(capture)) File.Delete(capture);
                ScreenCapture.CaptureScreenshot(capture);
                yield return null;
                yield return null;
            }
            shop.Close();
            yield return null;
            Debug.Log("Commerce HUD acceptance passed: first-pay entry, red dot, title, official function entry click and shop opening.");
            Finish(true, null);
        }

        private static void ConfigurePortraitGameView()
        {
            Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[]
            {
                Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1),
            });
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type kind = editor.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { Enum.ToObject(kind, 1), 720, 1280, "Commerce HUD acceptance" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type viewType = editor.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(view, index);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(bool passed, Exception error)
        {
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            if (error != null) Debug.LogException(error);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
