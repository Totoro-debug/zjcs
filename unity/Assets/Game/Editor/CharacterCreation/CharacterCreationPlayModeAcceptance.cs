using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Editor.CharacterCreation
{
    public static class CharacterCreationPlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.CharacterCreation.Acceptance.Active";
        private const string PathKey = "Zjcs.CharacterCreation.Acceptance.Path";
        private const string ResultKey = "Zjcs.CharacterCreation.Acceptance.Result";
        private const string DeadlineKey = "Zjcs.CharacterCreation.Acceptance.Deadline";
        private static bool started;
        private static double nextRefresh;
        private const string RetryKey = "Zjcs.CharacterCreation.Acceptance.Retry";
        private static bool KeepAlive => Environment.GetEnvironmentVariable("ZJCS_CREATION_KEEP_ALIVE") == "1";

        public static void PrepareAndRun()
        {
            SessionState.SetBool(RetryKey, KeepAlive);
            try
            {
                CharacterCreationAssetTools.Validate();
                Run();
            }
            catch (Exception error)
            {
                if (!KeepAlive) throw;
                Debug.LogException(error);
            }
        }

        [MenuItem("Zjcs/Validate/Character Creation Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running character creation acceptance.");

            ConfigurePortraitGameView();

            string testPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "output", "character-creation-smoke-" + Guid.NewGuid().ToString("N"));
            SessionState.SetString(PathKey, testPath);
            SessionState.SetInt(ResultKey, 2);
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 300f);
            SessionState.SetBool(ActiveKey, true);
            started = false;
            HookEditorEvents();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload()
        {
            if (SessionState.GetBool(ActiveKey, false)) HookEditorEvents();
            if (KeepAlive)
            {
                EditorApplication.update -= RefreshDuringDiagnosis;
                EditorApplication.update += RefreshDuringDiagnosis;
                if (SessionState.GetBool(RetryKey, false) && !SessionState.GetBool(ActiveKey, false))
                    EditorApplication.delayCall += PrepareAndRun;
            }
        }

        private static void RefreshDuringDiagnosis()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + 3;
            AssetDatabase.Refresh();
        }

        private static void HookEditorEvents()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= LocateController;
            EditorApplication.update += LocateController;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= LocateController;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                string testPath = SessionState.GetString(PathKey, string.Empty);
                if (Directory.Exists(testPath)) Directory.Delete(testPath, true);
                bool passed = SessionState.GetInt(ResultKey, 1) == 0;
                SessionState.SetBool(ActiveKey, false);
                if (passed) SessionState.SetBool(RetryKey, false);
                if ((Application.isBatchMode || Environment.GetEnvironmentVariable("ZJCS_CREATION_AUTOMATION") == "1")
                    && (passed || !KeepAlive)) EditorApplication.Exit(passed ? 0 : 1);
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.update += LocateController;
        }

        private static void LocateController()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Character creation acceptance exceeded its deadline."));
                return;
            }
            if (started) return;
            CharacterCreationFlowController controller = UnityEngine.Object.FindObjectOfType<CharacterCreationFlowController>();
            if (controller == null)
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
                    Finish(false, new TimeoutException("Character creation runtime controller did not start."));
                return;
            }

            started = true;
            Application.logMessageReceived -= OnRuntimeLog;
            Application.logMessageReceived += OnRuntimeLog;
            try
            {
                Field(controller, "service").SetValue(controller,
                    CharacterCreationOfflineService.OpenDefault(SessionState.GetString(PathKey, string.Empty)));
                controller.StartCoroutine(Guarded(RunScenario(controller)));
            }
            catch (Exception error)
            {
                Finish(false, error);
            }
        }

        private static IEnumerator Guarded(IEnumerator scenario)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(scenario);
            while (stack.Count > 0)
            {
                scenario = stack.Peek();
                object current;
                bool moved;
                try
                {
                    moved = scenario.MoveNext();
                    current = moved ? scenario.Current : null;
                }
                catch (Exception error)
                {
                    Finish(false, error);
                    yield break;
                }

                if (!moved) { stack.Pop(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
        }

        private static void OnRuntimeLog(string message, string stackTrace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception) && SessionState.GetInt(ResultKey, 1) == 2)
                Finish(false, new InvalidOperationException(message + "\n" + stackTrace));
        }

        private static IEnumerator RunScenario(CharacterCreationFlowController controller)
        {
            Debug.Log("Creation acceptance viewport: " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height);
            string testPath = SessionState.GetString(PathKey, string.Empty);
            CharacterCreationOfflineService service = (CharacterCreationOfflineService)Field(controller, "service").GetValue(controller);

            // This suite covers creation itself. The opening has its own playback suite.
            object opening = Field(controller, "opening").GetValue(controller);
            var played = (HashSet<string>)opening.GetType().GetField("PlayedSlots", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            played.Add("slot-1");
            played.Add("slot-2");

            Zjcs.Presentation.Opening.CharacterOpeningFlow.SkipOpeningForAcceptance = true;
            Zjcs.Presentation.Opening.CharacterOpeningFlow.SkipFirstStoryForAcceptance = true;
            ClickByLabel(Screen(controller), "新建存档");
            yield return WaitForPage(controller, "body");
            Require(Panel(controller) != null, "Creation panel initialization failed: " + CurrentStatus(controller));
            TMP_Text[] initialLabels = Screen(controller).GetComponentsInChildren<TMP_Text>(true);
            TMP_FontAsset officialFont = (TMP_FontAsset)Field(controller, "font").GetValue(controller);
            Require(officialFont != null && initialLabels.Length > 0,
                "The official font or save creation labels are missing.");
            foreach (TMP_Text label in initialLabels)
            {
                Require(label.font == officialFont, "The official font was not applied to save creation label '"
                    + label.text + "': " + (label.font == null ? "null" : label.font.name));
                string visibleText = label.text.Replace("\u200b", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
                Require(officialFont.HasCharacters(visibleText, out uint[] missingGlyphs, false, true),
                    "The original FZY4K font has missing characters in: " + label.text);
            }
            string slotId = (string)Field(controller, "slotId").GetValue(controller);
            Require(!string.IsNullOrEmpty(slotId) && slotId == "slot-1", "The isolated profile slot was not created.");
            Require(((CharacterCreationDraft)Field(controller, "draft").GetValue(controller)).body_type == "male",
                "The official default body selection was not restored.");
            RequireNativePreview(controller, "malePreview", true);

            yield return WaitForTransition(controller);
            yield return Capture(controller, "male-body");
            ClickOfficial(controller, "FemaleBtn");
            RequireNativePreview(controller, "femalePreview", true);
            RequireNativePreview(controller, "malePreview", false);
            yield return WaitForTransition(controller);
            ClickOfficial(controller, "NextBtn");
            yield return WaitForPage(controller, "appearance");
            yield return WaitForTransition(controller);

            yield return Capture(controller, "female-appearance");
            Transform panel = Panel(controller).transform;
            Transform diyRoot = Deep(panel, "DIYContent");
            Transform mainTabs = Deep(Deep(diyRoot, "TabGroupGo"), "TabGroup");
            Transform[] tabItems = OfficialTabItems(mainTabs);
            Require(tabItems.Length == 4, "Official appearance tab group is incomplete.");
            Click(tabItems[1]);
            yield return null;
            string initialHair = ActiveAppearance(controller).hairstyle_id;
            string selectedHair = SelectDifferentOption(controller, initialHair);
            Require(ActiveAppearance(controller).hairstyle_id == selectedHair, "Hair option did not update the draft.");

            Transform diy = Deep(panel, "DIYContent");
            Transform hairFields = Deep(Deep(diy, "HairTabGroup"), "TabGroup");
            Require(hairFields != null && hairFields.childCount == 2, "Official hair color controls are incomplete.");
            Click(hairFields.GetChild(1));
            yield return null;
            string oldHairColor = ActiveAppearance(controller).hair_color;
            SelectDifferentDye(controller);
            Require(ActiveAppearance(controller).hair_color != oldHairColor, "Fixed hair color selection did not update the draft.");
            OpenCustomColorAndChangeHue(controller);
            yield return Capture(controller, "female-custom-dye");

            Click(tabItems[2]);
            yield return null;
            ClickField(controller, "FaceTabGroup", 0);
            yield return null;
            SelectDifferentOption(controller, ActiveAppearance(controller).eyebrow_id);
            ClickField(controller, "FaceTabGroup", 1);
            yield return null;
            SelectDifferentOption(controller, ActiveAppearance(controller).eye_id);
            ClickField(controller, "FaceTabGroup", 2);
            yield return null;
            SelectDifferentDye(controller);
            ClickField(controller, "FaceTabGroup", 3);
            yield return null;
            SelectDifferentOption(controller, ActiveAppearance(controller).mouth_id);
            ClickField(controller, "FaceTabGroup", 4);
            yield return null;
            SelectDifferentOption(controller, ActiveAppearance(controller).makeup_id);
            Click(tabItems[3]);
            yield return null;
            SelectDifferentOption(controller, ActiveAppearance(controller).feature_id);
            Click(tabItems[0]);
            yield return null;
            SelectDifferentDye(controller);

            ClickOfficial(controller, "NextBtn");
            yield return WaitForPage(controller, "name");
            yield return WaitForTransition(controller);
            yield return Capture(controller, "female-name");
            string savedHair = ActiveAppearance(controller).hairstyle_id;
            string savedHairColor = ActiveAppearance(controller).hair_color;
            TMP_InputField name = Deep(Panel(controller).transform, "NameInput").GetComponent<TMP_InputField>();
            Require(name != null, "Official character name input was not imported.");
            name.text = "测试法师";
            yield return new WaitForSecondsRealtime(0.4f);

            ClickOfficial(controller, "CloseBtn_dark");
            yield return WaitForPage(controller, "appearance");
            yield return WaitForTransition(controller);
            ClickOfficial(controller, "CloseBtn_dark");
            yield return WaitForPage(controller, "body");
            yield return WaitForTransition(controller);
            ClickOfficial(controller, "CloseBtn_dark");
            yield return WaitUntil(() => Screen(controller).name == "Screen_选择存档", "Returning to the save list timed out.");

            ClickByLabel(Screen(controller), "新角色 · 存档 1");
            yield return WaitForPage(controller, "body");
            yield return WaitForTransition(controller);
            ClickOfficial(controller, "NextBtn");
            yield return WaitForPage(controller, "appearance");
            yield return WaitForTransition(controller);
            ClickOfficial(controller, "NextBtn");
            yield return WaitForPage(controller, "name");
            yield return WaitForTransition(controller);
            name = Deep(Panel(controller).transform, "NameInput").GetComponent<TMP_InputField>();
            Require(name.text == "测试法师", "Manual name did not survive reopening the draft.");
            Require(ActiveAppearance(controller).hairstyle_id == savedHair, "Selected hairstyle did not survive reopening the draft.");
            Require(ActiveAppearance(controller).hair_color == savedHairColor, "Custom hair color did not survive reopening the draft.");
            Require(Deep(Panel(controller).transform, "SetNameContent").gameObject.activeInHierarchy,
                "Saved name page was not restored to its final UI state.");

            name.text = string.Empty;
            ClickOfficial(controller, "CommonBtn_2");
            Require(!service.HasCharacter(slotId), "A character was created without a required name.");
            GameObject requiredNameDialog = (GameObject)Field(controller, "activeConfirmation").GetValue(controller);
            Require(requiredNameDialog != null, "Required-name validation did not show its message.");
            ClickByLabel(requiredNameDialog, "确认");
            yield return null;
            name = Deep(Panel(controller).transform, "NameInput").GetComponent<TMP_InputField>();
            name.text = "测试法师";
            yield return new WaitForSecondsRealtime(0.4f);
            ClickOfficial(controller, "CommonBtn_2");
            GameObject submissionDialog = (GameObject)Field(controller, "activeConfirmation").GetValue(controller);
            Require(service.HasCharacter(slotId), "Submitting the named character did not persist it: "
                + (submissionDialog == null ? CurrentStatus(controller) : string.Join(";", Array.ConvertAll(submissionDialog.GetComponentsInChildren<TMP_Text>(), label => label.text))));
            yield return AdvanceToWeapon(controller);
            ClickByLabel(Screen(controller), "法师");
            Require(Screen(controller).name == "Screen_角色创建完成",
                "Initial weapon click left " + Screen(controller).name + ": " + CurrentStatus(controller));
            yield return WaitUntil(() => Screen(controller) != null && Screen(controller).name == "Screen_角色创建完成",
                "Initial weapon selection did not finish character creation. screen="
                + (Screen(controller) == null ? "null" : Screen(controller).name)
                + " status=" + CurrentStatus(controller));

            Require(service.HasCharacter(slotId), "The character was not written to the isolated profile.");
            Require(service.GetCharacterName(slotId) == "测试法师", "The saved character name differs from the submitted name.");
            Require(service.GetInitialWeaponType(slotId) == CharacterCreationOfflineService.WandInitialWeaponType,
                "The selected initial weapon was not persisted.");

            CharacterCreationOfflineService reopened = CharacterCreationOfflineService.OpenDefault(testPath);
            Require(reopened.GetCharacterName(slotId) == "测试法师"
                && reopened.GetInitialWeaponType(slotId) == CharacterCreationOfflineService.WandInitialWeaponType,
                "The character record could not be read back from disk.");
            string savePath = Path.Combine(testPath, "saves", slotId, "city-save.json");
            string json = File.ReadAllText(savePath, Encoding.UTF8);
            OfflineMainlineSave saved = JsonUtility.FromJson<OfflineMainlineSave>(json);
            CharacterCreationAssetCatalog.LoadDefault().TryGetAppearanceOption("female", "hairstyle_id", savedHair,
                out CharacterCreationOptionInfo hairOption);
            Require(saved.Player.Wardrobe.WornSkins.Any(skin => skin.Type == hairOption.SkinType && skin.Id == hairOption.SkinId),
                "The selected official hairstyle ID was not serialized into the character save.");
            OfflineAppearanceDye savedDye = saved.Player.Wardrobe.Dyes.Single(dye => dye.Type == 2);
            Color actualHair = Color.HSVToRGB(savedDye.Hue / 360f, savedDye.Saturation / 100f, savedDye.Value / 100f);
            Color expectedHair = ParseHex(savedHairColor);
            Require(Mathf.Abs(actualHair.r - expectedHair.r) <= .01f && Mathf.Abs(actualHair.g - expectedHair.g) <= .01f
                && Mathf.Abs(actualHair.b - expectedHair.b) <= .01f, "The custom hair dye did not survive save readback.");

            yield return Capture(controller, "saved-character");
            ClickByLabel(Screen(controller), "返回存档");
            yield return null;
            ConfigurePortraitGameView(450, 800);
            ClickByLabel(Screen(controller), "新建存档");
            yield return WaitForPage(controller, "body");
            yield return WaitForTransition(controller);
            Require(((CharacterCreationDraft)Field(controller, "draft").GetValue(controller)).body_type == "male",
                "The second slot did not keep its own default body.");
            ClickOfficial(controller, "NextBtn");
            yield return WaitForPage(controller, "appearance");
            yield return WaitForTransition(controller);
            panel = Panel(controller).transform;
            diyRoot = Deep(panel, "DIYContent");
            tabItems = OfficialTabItems(Deep(Deep(diyRoot, "TabGroupGo"), "TabGroup"));
            Click(tabItems[1]);
            yield return null;
            string defaultMaleHair = ActiveAppearance(controller).hairstyle_id;
            SelectDifferentOption(controller, defaultMaleHair);
            ClickOfficial(controller, "ResetBtn");
            yield return null;
            ClickByLabel((GameObject)Field(controller, "activeConfirmation").GetValue(controller), "取消");
            Require(ActiveAppearance(controller).hairstyle_id != defaultMaleHair, "Cancelling reset changed the appearance.");
            yield return null;
            ClickOfficial(controller, "ResetBtn");
            yield return null;
            ClickByLabel((GameObject)Field(controller, "activeConfirmation").GetValue(controller), "确认");
            yield return null;
            Require(ActiveAppearance(controller).hairstyle_id == defaultMaleHair, "Confirmed reset did not restore the default hair.");
            RequireNativePreview(controller, "malePreview", true);
            object malePreview = Field(controller, "malePreview").GetValue(controller);
            GameObject maleModel = (GameObject)Field(malePreview, "modelRoot").GetValue(malePreview);
            var meshPlayer = maleModel.GetComponent<CharacterCreationMeshFramePlayer>();
            int frame = meshPlayer.CurrentFrame;
            yield return new WaitForSecondsRealtime(.15f);
            Require(meshPlayer.CurrentFrame != frame, "Native official idle frames did not advance.");
            yield return Capture(controller, "male-appearance-450");
            ClickOfficial(controller, "NextBtn");
            yield return WaitForPage(controller, "name");
            yield return WaitForTransition(controller);
            Deep(Panel(controller).transform, "NameInput").GetComponent<TMP_InputField>().text = "测试剑士";
            yield return new WaitForSecondsRealtime(.4f);
            ClickOfficial(controller, "CommonBtn_2");
            yield return AdvanceToWeapon(controller);
            ClickByLabel(Screen(controller), "战士");
            yield return WaitUntil(() => Screen(controller).name == "Screen_角色创建完成", "Male character did not finish.");
            reopened = CharacterCreationOfflineService.OpenDefault(testPath);
            Require(reopened.GetCharacterName("slot-1") == "测试法师" && reopened.GetCharacterName("slot-2") == "测试剑士",
                "The two saved character slots were not kept independent.");
            Require(reopened.GetInitialWeaponType("slot-2") == CharacterCreationOfflineService.SwordInitialWeaponType,
                "The sword choice was not persisted.");
            Debug.Log("Character creation Play Mode acceptance passed: save slot, body selection, official appearance tabs, fixed/custom dyes, manual name, draft resume, required-name validation, character persistence, and initial weapon.");
            Finish(true, null);
        }

        private static IEnumerator Capture(CharacterCreationFlowController controller, string name)
        {
            string directory = Environment.GetEnvironmentVariable("ZJCS_CREATION_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) yield break;
            Directory.CreateDirectory(directory);
            yield return null;
            Canvas.ForceUpdateCanvases();
            string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            yield return WaitUntil(() => File.Exists(path), "Screenshot was not rendered: " + name);
            object preview = Field(controller, "femalePreview").GetValue(controller)
                ?? Field(controller, "malePreview").GetValue(controller);
            if (preview != null)
            {
                Camera camera = (Camera)Field(preview, "camera").GetValue(preview);
                GameObject model = (GameObject)Field(preview, "modelRoot").GetValue(preview);
                if (!model.activeInHierarchy)
                {
                    preview = Field(controller, "malePreview").GetValue(controller);
                    camera = (Camera)Field(preview, "camera").GetValue(preview);
                }
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = camera.targetTexture;
                var texture = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                texture.Apply();
                int visible = 0;
                foreach (Color32 pixel in texture.GetPixels32()) if (pixel.a > 20) visible++;
                Require(visible > 500, "Native character preview rendered no visible geometry: " + name);
                File.WriteAllBytes(Path.Combine(directory, name + "-preview.png"), texture.EncodeToPNG());
                UnityEngine.Object.Destroy(texture);
                RenderTexture.active = previous;
                Debug.Log("Creation snapshot: " + path + "; visible preview pixels=" + visible);
            }
        }

        private static IEnumerator AdvanceToWeapon(CharacterCreationFlowController controller)
        {
            // FirstStory is covered by CharacterOpeningPlayModeAcceptance. This suite
            // isolates creation persistence and the already confirmed weapon choices.
            yield return null;
            var opening = (MonoBehaviour)Field(controller, "opening").GetValue(controller);
            opening.StopAllCoroutines();
            opening.GetType().GetMethod("Cleanup", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(opening, null);
            opening.GetType().GetMethod("DestroyWorld", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(opening, null);
            controller.GetType().GetMethod("ShowInitialWeapon", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
            yield return null;
        }

        private static Color ParseHex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString("#" + hex, out Color color)) throw new FormatException("Invalid saved dye: " + hex);
            return color;
        }

        private static void ConfigurePortraitGameView(int width = 720, int height = 1280)
        {
            Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1) });
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type kind = editor.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { Enum.ToObject(kind, 1), width, height, "Creation acceptance" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type viewType = editor.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
        }

        private static IEnumerator WaitForPage(CharacterCreationFlowController controller, string page)
        {
            yield return WaitUntil(() => ((CharacterCreationDraft)Field(controller, "draft").GetValue(controller))?.page == page,
                "Character creation page did not become " + page + ".");
        }

        private static IEnumerator WaitForTransition(CharacterCreationFlowController controller)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10.0;
            CharacterCreationPerformancePlayer player = (CharacterCreationPerformancePlayer)Field(controller, "performancePlayer").GetValue(controller);
            while (player != null && player.IsTransitionPlaying)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline)
                    throw new TimeoutException("An official character creation animation did not finish.");
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string error)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 15.0;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException(error);
                yield return null;
            }
        }

        private static void ClickField(CharacterCreationFlowController controller, string groupName, int index)
        {
            Transform group = Deep(Deep(Deep(Panel(controller).transform, "DIYContent"), groupName), "TabGroup");
            Require(group != null && index >= 0 && index < group.childCount, "Official appearance field group is incomplete: " + groupName);
            Click(group.GetChild(index));
        }

        private static Transform[] OfficialTabItems(Transform group)
        {
            if (group == null) return Array.Empty<Transform>();
            var items = new List<Transform>();
            foreach (Transform child in group)
                if (child.name.StartsWith("CreatePlayerPanelTabGroupItem", StringComparison.Ordinal)
                    || child.GetComponent<Button>() != null || child.GetComponentInChildren<Button>(true) != null)
                    items.Add(child);
            return items.ToArray();
        }

        private static string SelectDifferentOption(CharacterCreationFlowController controller, string current)
        {
            IDictionary items = (IDictionary)Field(controller, "optionItems").GetValue(controller);
            foreach (DictionaryEntry item in items)
            {
                string id = item.Key as string;
                if (id == current) continue;
                Transform target = item.Value as Transform;
                Toggle toggle = target == null ? null : target.GetComponentInChildren<Toggle>(true);
                if (toggle != null) ClickAt(toggle.gameObject);
                else Click(target);
                return id;
            }
            throw new InvalidOperationException("No alternate official appearance option was available for " + current + ".");
        }

        private static void SelectDifferentDye(CharacterCreationFlowController controller)
        {
            IDictionary items = (IDictionary)Field(controller, "dyeItems").GetValue(controller);
            int current = (int)Field(controller, "dyeColorId").GetValue(controller);
            foreach (DictionaryEntry item in items)
            {
                int id = (int)item.Key;
                if (id == current) continue;
                Transform target = item.Value as Transform;
                Toggle toggle = target == null ? null : target.GetComponentInChildren<Toggle>(true);
                if (toggle != null) ClickAt(toggle.gameObject);
                else Click(target);
                return;
            }
            throw new InvalidOperationException("No alternate official dye color was available.");
        }

        private static void OpenCustomColorAndChangeHue(CharacterCreationFlowController controller)
        {
            string initialColor = ActiveAppearance(controller).hair_color;
            Transform colorRoot = Deep(Deep(Panel(controller).transform, "DIYContent"), "ColorGo");
            Transform custom = Deep(colorRoot, "CustomColorItem");
            Toggle toggle = custom == null ? null : custom.GetComponentInChildren<Toggle>(true);
            if (toggle != null) ClickAt(toggle.gameObject);
            else Click(custom);

            Transform sliderNode = Deep(colorRoot, "HSlider");
            Slider slider = sliderNode == null ? null : sliderNode.GetComponent<Slider>();
            Require(slider != null && slider.maxValue > slider.minValue, "Official custom hue slider is unavailable.");
            float value = slider.value >= slider.maxValue ? slider.minValue : slider.value + 1f;
            slider.value = value;
            Require(ActiveAppearance(controller).hair_color != initialColor,
                "Changing the custom hue did not update the selected hair color.");
            foreach (string axis in new[] { "SSlider", "VSlider" })
            {
                Slider channel = Deep(colorRoot, axis).GetComponent<Slider>();
                channel.value = channel.value > channel.minValue ? channel.minValue : channel.maxValue;
                Require(channel.value >= channel.minValue && channel.value <= channel.maxValue,
                    "The custom dye channel is outside the official range: " + axis);
            }
        }

        private static void RequireNativePreview(CharacterCreationFlowController controller,
            string previewField, bool expectedVisible)
        {
            object preview = Field(controller, previewField).GetValue(controller);
            Require(preview != null, "The native character preview was not initialized: " + previewField);
            GameObject modelRoot = (GameObject)Field(preview, "modelRoot").GetValue(preview);
            Require(modelRoot != null && modelRoot.activeInHierarchy == expectedVisible,
                "The native character preview visibility is incorrect: " + previewField);
            if (!expectedVisible) return;
            CharacterCreationMeshFramePlayer player = modelRoot.GetComponent<CharacterCreationMeshFramePlayer>();
            Require(player != null && player.VisiblePartCount > 10,
                "The native character preview is missing official mesh parts: " + previewField);
            foreach (Renderer renderer in modelRoot.GetComponentsInChildren<Renderer>(false))
                if (renderer.enabled && renderer.sharedMaterial != null)
                    return;
            throw new InvalidOperationException("The selected native character preview has no active renderer: " + previewField);
        }

        private static CharacterAppearanceDraft ActiveAppearance(CharacterCreationFlowController controller)
        {
            CharacterCreationDraft draft = (CharacterCreationDraft)Field(controller, "draft").GetValue(controller);
            return draft.body_type == "female" ? draft.bodies.female : draft.bodies.male;
        }

        private static void ClickOfficial(CharacterCreationFlowController controller, string nodeName)
        {
            GameObject panel = Panel(controller);
            Require(panel != null, "Official creation panel is unavailable. " + CurrentStatus(controller));
            Transform node = null;
            foreach (Transform candidate in panel.GetComponentsInChildren<Transform>(false))
                if (candidate.name == nodeName) { node = candidate; break; }
            Require(node != null, "Official character creation control is missing: " + nodeName);
            Click(node);
        }

        private static string CurrentStatus(CharacterCreationFlowController controller)
        {
            Component status = (Component)Field(controller, "status").GetValue(controller);
            if (status is TMP_Text tmp) return tmp.text;
            if (status is Text text) return text.text;
            return "screen=" + (Screen(controller) == null ? "null" : Screen(controller).name);
        }

        private static void Click(Transform target)
        {
            Require(target != null, "Character creation interaction target is missing.");
            Button button = target.GetComponent<Button>() ?? target.GetComponentInChildren<Button>(true);
            Require(button != null && button.interactable, "Character creation control has no active Button: " + target.name);
            ClickAt(button.gameObject);
        }

        private static void ClickAt(GameObject control)
        {
            Canvas.ForceUpdateCanvases();
            var rect = control.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0, "Pointer did not hit any UI at " + control.name + ": " + pointer.position);
            GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Require(receiver == control, "Pointer hit " + (receiver == null ? hits[0].gameObject.name : receiver.name)
                + " instead of " + control.name + ": " + pointer.position);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void ClickByLabel(GameObject root, string label)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy || !button.interactable) continue;
                foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
                    if (text.text == label)
                    {
                        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                        return;
                    }
                foreach (Text text in button.GetComponentsInChildren<Text>(true))
                    if (text.text == label)
                    {
                        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                        return;
                    }
            }
            throw new InvalidOperationException("Character creation button was not found: " + label);
        }

        private static GameObject Screen(CharacterCreationFlowController controller)
        {
            return (GameObject)Field(controller, "screen").GetValue(controller);
        }

        private static GameObject Panel(CharacterCreationFlowController controller)
        {
            return (GameObject)Field(controller, "officialPanel").GetValue(controller);
        }

        private static Transform Deep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = Deep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static FieldInfo Field(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            return field;
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
