using System;
using System.Collections;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Opening;

namespace Zjcs.Editor.CharacterCreation
{
    /// <summary>Play Mode acceptance for the official opening story fight played before creation.</summary>
    public static class CharacterOpeningPlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.Opening.Acceptance.Active";
        private const string PathKey = "Zjcs.Opening.Acceptance.Path";
        private const string ResultKey = "Zjcs.Opening.Acceptance.Result";
        private const string DeadlineKey = "Zjcs.Opening.Acceptance.Deadline";
        private static bool started;

        [MenuItem("Zjcs/Validate/Opening Story Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running opening acceptance.");
            ConfigurePortraitGameView();
            string testPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "output", "opening-smoke-" + Guid.NewGuid().ToString("N"));
            SessionState.SetString(PathKey, testPath);
            SessionState.SetInt(ResultKey, 2);
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 420f);
            SessionState.SetBool(ActiveKey, true);
            started = false;
            CharacterOpeningFlow.SkipOpeningForAcceptance = false;
            CharacterOpeningFlow.SkipFirstStoryForAcceptance = false;
            CharacterOpeningFlow.ForceSkipButtonForAcceptance = false;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= LocateController;
            EditorApplication.update += LocateController;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                EditorApplication.update -= LocateController;
                EditorApplication.update += LocateController;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }
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
                if (Application.isBatchMode
                    || Environment.GetEnvironmentVariable("ZJCS_OPENING_AUTOMATION") == "1")
                    EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        private static void LocateController()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Opening acceptance exceeded its deadline."));
                return;
            }
            if (started) return;
            CharacterCreationFlowController controller = UnityEngine.Object.FindObjectOfType<CharacterCreationFlowController>();
            if (controller == null) return;
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

        private static void OnRuntimeLog(string message, string stackTrace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception) && SessionState.GetInt(ResultKey, 1) == 2)
                Finish(false, new InvalidOperationException(message + "\n" + stackTrace));
        }

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

        private static IEnumerator RunScenario(CharacterCreationFlowController controller)
        {
            CharacterOpeningFlow flow = (CharacterOpeningFlow)Field(controller, "opening").GetValue(controller);
            Require(flow != null, "The opening flow component was not attached to the creation controller.");
            Require(!CharacterOpeningFlow.HasPlayed("slot-1"), "The opening was already marked played in a fresh process.");
            Require(CharacterOpeningFlow.ShouldSkipNewbieCutscene(() => "130077", null),
                "The official skip condition 21003 channel branch was not evaluated.");
            Require(!CharacterOpeningFlow.ShouldSkipNewbieCutscene(() => string.Empty, () => string.Empty),
                "The official skip condition 21003 matched an empty channel/media.");

            ClickByLabel(controller, "新建存档");
            yield return WaitUntil(() => Field(flow, "assets").GetValue(flow) != null,
                "The opening assets were not loaded: " + CurrentStatus(controller));

            OpeningAssets assets = (OpeningAssets)Field(flow, "assets").GetValue(flow);
            Require(assets.Story.Manifest == "854_156383", "Unexpected opening story manifest.");
            Require(assets.Story.Graphs.Count == 3, "The three official opening graphs were not loaded.");
            Require(assets.Story.Dialogues.Count == 6, "The official opening dialogues are incomplete.");
            VerifyOfficialVoiceFiles(assets);
            Require(assets.Record.StageId == 20 && assets.Record.Entities.Count == 19
                && assets.Record.Rounds.Count == 14 && assets.Record.StepCount == 1168,
                "The official record did not parse to its pinned shape.");
            Require(assets.Stage.StageId == 20 && assets.Stage.Scene == "game_902",
                "The official stage 20 scene binding is missing.");
            Require(assets.Stage.Models.Count > 0, "The official monster model identities were not bound.");
            Debug.Log("[Opening acceptance] record stage=" + assets.Record.StageId + " entities="
                + assets.Record.Entities.Count + " rounds=" + assets.Record.Rounds.Count
                + " steps=" + assets.Record.StepCount + " scene=" + assets.Stage.Scene);

            yield return WaitUntil(() => FindText("又到最后的时刻了，我们上吧！") != null,
                "The BeforeFight official dialog text was not presented.");
            Require(FindText("薇兰") != null, "The official dialog role name was not presented.");
            OpeningStoryPlayer voicePlayer = (OpeningStoryPlayer)Field(flow, "story").GetValue(flow);
            var voiceClips = (System.Collections.Generic.Dictionary<string, AudioClip>)
                Field(voicePlayer, "voiceClips").GetValue(voicePlayer);
            AudioSource voiceAudio = (AudioSource)Field(flow, "voiceAudio").GetValue(flow);
            Require(voiceClips.TryGetValue("prologue_main_1_01", out AudioClip firstVoice)
                && firstVoice != null && firstVoice.samples > 0 && voiceAudio.clip == firstVoice,
                "The first BeforeFight line did not load its official local voice event.");
            yield return Capture("opening-before-fight");
            yield return VerifyOfficialWindowAnimations(controller, assets);

            yield return WaitUntil(() =>
            {
                var playback = (OpeningFightPlayback)Field(flow, "playback").GetValue(flow);
                return playback != null && playback.PreparedUnits > 0;
            }, "The official record playback did not spawn units or advance rounds.");
            OpeningFightPlayback running = (OpeningFightPlayback)Field(flow, "playback").GetValue(flow);
            OpeningBattlefield battlefield = (OpeningBattlefield)Field(flow, "battlefield").GetValue(flow);
            Require(battlefield != null, "The official game_902 battlefield was not mounted.");
            Require(battlefield.BatchCount == 142 && battlefield.ParticleCount == 23,
                "The official game_902 geometry or particle mounts are incomplete: batches="
                + battlefield.BatchCount + " particles=" + battlefield.ParticleCount);
            Require(battlefield.StageCamera != null && battlefield.StageCamera.orthographic,
                "The official opening battlefield camera was not restored.");
            MeshFilter[] stageMeshes = battlefield.Root.GetComponentsInChildren<MeshFilter>(false);
            Require(stageMeshes.Length == 142, "The official stage mesh batches were not created: " + stageMeshes.Length);
            foreach (MeshFilter stageMesh in stageMeshes)
            {
                MeshRenderer renderer = stageMesh.GetComponent<MeshRenderer>();
                Require(renderer != null && renderer.sharedMaterial != null
                    && renderer.sharedMaterial.mainTexture != null,
                    "An official game_902 mesh has no bound texture: " + stageMesh.name);
            }
            Require(running.SpawnedUnits > 0, "No official Spine battle unit was rendered.");
            Debug.Log("[Opening acceptance] playback resolved units=" + running.PreparedUnits
                + " rendered=" + running.SpawnedUnits + " round=" + running.Round
                + " stageMeshes=" + stageMeshes.Length + " stageParticles=" + battlefield.ParticleCount
                + " stageCamera=" + battlefield.StageCamera.name);
            yield return Capture("opening-fight");

            running.RequestFastForward();
            yield return WaitUntil(() =>
            {
                AdvanceOpeningDialog();
                var current = (OpeningFightPlayback)Field(flow, "playback").GetValue(flow);
                return current == null || current.Finished;
            }, "Fast-forwarding the opening record did not finish playback.");
            yield return WaitUntil(() =>
            {
                AdvanceOpeningDialog();
                return (GameObject)Field(controller, "officialPanel").GetValue(controller) != null;
            }, "The creation panel did not open after the opening: " + CurrentStatus(controller));
            Require(CharacterOpeningFlow.HasPlayed("slot-1"), "The opened slot was not marked as played.");
            yield return Capture("opening-creation");

            // Re-entering the same slot in one process must not replay the opening.
            controller.GetType().GetMethod("ShowSlots", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { null });
            yield return WaitUntil(() => ((GameObject)Field(controller, "screen").GetValue(controller)).name == "Screen_选择存档",
                "Returning to the slot list failed.");
            yield return null;
            yield return null;
            ClickByLabel(controller, "新角色 · 存档 1");
            yield return WaitUntil(() => Field(flow, "assets").GetValue(flow) == null
                && (GameObject)Field(controller, "officialPanel").GetValue(controller) != null,
                "The already-played opening replayeed or the creation panel did not reopen.");

            Debug.Log("Opening Play Mode acceptance passed: official skip condition, story graphs, dialog text/role, "
                + "record parse (19 entities, 14 rounds, 1168 steps), unit spawn, fast-forward, creation hand-off, "
                + "and single play per slot per process.");
            Finish(true, null);
        }

        private static IEnumerator VerifyOfficialWindowAnimations(CharacterCreationFlowController controller,
            OpeningAssets assets)
        {
            OpeningUiNode dialog = assets.Ui.Prefabs["talk_dialog"];
            OpeningUiNode exit = assets.Ui.Prefabs["exit_fight_transition"];
            OpeningUiNode enter = assets.Ui.Prefabs["enter_game_transition"];
            OpeningUiNode firstStory = assets.Ui.Prefabs["first_story"];
            Require(dialog.Window.ShowClip == "TalkDialog_Show"
                && Mathf.Abs(dialog.Window.Clips[dialog.Window.ShowClip].Duration - 1f / 3f) < 0.002f,
                "The official dialogue show clip was not restored.");
            Require(dialog.Window.LoopClip == "TalkDialog_Loop"
                && dialog.Window.Clips[dialog.Window.LoopClip].Tracks.Count == 0,
                "The official dialogue loop clip binding differs from the APK.");
            Require(exit.Window.ShowClip == "ExitFightTransitionPanel_Show"
                && Mathf.Abs(exit.Window.Clips[exit.Window.ShowClip].Duration - 1f) < 0.002f,
                "The official post-fight panel show clip was not restored.");
            Require(enter.Window.ShowClip == "StoryTransitionPanel_Show"
                && Mathf.Abs(enter.Window.Clips[enter.Window.ShowClip].Duration - 2f) < 0.002f,
                "The official enter-game panel show clip was not restored.");
            Require(firstStory.Window.ShowClip == "StoryPanel_Show"
                && Mathf.Abs(firstStory.Window.Clips[firstStory.Window.ShowClip].Duration - 13.216666f) < 0.01f
                && firstStory.Window.Clips[firstStory.Window.ShowClip].Tracks.Count == 124,
                "The official first-story clip curves are incomplete.");

            var view = new OpeningUiView(assets.Ui, null, null, 1000);
            Coroutine animation = null;
            try
            {
                GameObject exitPanel = view.Create("exit_fight_transition");
                exitPanel.SetActive(true);
                Image image = exitPanel.GetComponentInChildren<Image>(true);
                Require(image != null, "The post-fight transition image was not restored.");
                float start = Time.realtimeSinceStartup;
                animation = controller.StartCoroutine(view.PlayAnimation(exitPanel, "exit_fight_transition",
                    exit.Window.ShowClip));
                yield return new WaitForSecondsRealtime(0.45f);
                Require(image.color.a > 0.2f && image.color.a < 0.8f,
                    "The post-fight panel did not evaluate its official curve mid-transition.");
                yield return animation;
                animation = null;
                Require(image.color.a > 0.99f,
                    "The post-fight panel did not reach its official final alpha.");
                Require(Time.realtimeSinceStartup - start >= 0.95f,
                    "The post-fight panel show did not wait for its official clip duration.");
                UnityEngine.Object.Destroy(exitPanel);

                GameObject storyPanel = view.Create("first_story");
                storyPanel.SetActive(true);
                OpeningUiMeshGraphic mesh = null;
                foreach (OpeningUiMeshGraphic candidate in storyPanel.GetComponentsInChildren<OpeningUiMeshGraphic>(true))
                    if (candidate.gameObject.name == "Mesh") { mesh = candidate; break; }
                Require(mesh != null, "The first-story UI mesh was not restored.");
                Material originalMaterial = mesh.material;
                animation = controller.StartCoroutine(view.PlayAnimation(storyPanel, "first_story",
                    firstStory.Window.ShowClip));
                Require(mesh.material != originalMaterial,
                    "The official first-story material animation was not applied.");
                yield return new WaitForSecondsRealtime(0.2f);
                controller.StopCoroutine(animation);
                animation = null;
            }
            finally
            {
                if (animation != null) controller.StopCoroutine(animation);
                view.Dispose();
            }
        }

        private static void VerifyOfficialVoiceFiles(OpeningAssets assets)
        {
            Require(assets.VoiceFiles.Count == 6, "The six official opening voice events were not mapped.");
            MethodInfo decoder = typeof(OpeningStoryPlayer).GetMethod("ReadPcmWave",
                BindingFlags.NonPublic | BindingFlags.Static);
            Require(decoder != null, "The official PCM voice decoder is unavailable.");
            foreach (System.Collections.Generic.KeyValuePair<string, string> entry in assets.VoiceFiles)
            {
                string path = Path.Combine(OpeningAssets.Directory,
                    entry.Value.Replace('/', Path.DirectorySeparatorChar));
                Require(File.Exists(path), "An official opening voice file is missing: " + entry.Value);
                AudioClip clip = (AudioClip)decoder.Invoke(null, new object[] { path, entry.Key });
                Require(clip != null && clip.samples > 0 && clip.channels > 0 && clip.frequency > 0,
                    "An official opening voice file did not decode: " + entry.Key);
                UnityEngine.Object.Destroy(clip);
            }
        }

        private static void ClickOfficialAny(CharacterCreationFlowController controller, params string[] nodeNames)
        {
            foreach (string name in nodeNames)
            {
                GameObject panel = (GameObject)Field(controller, "officialPanel").GetValue(controller);
                if (panel == null) continue;
                foreach (Transform candidate in panel.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name != name || !candidate.gameObject.activeInHierarchy) continue;
                    Button button = candidate.GetComponent<Button>() ?? candidate.GetComponentInChildren<Button>(true);
                    if (button == null || !button.interactable) continue;
                    Click(button);
                    return;
                }
            }
            throw new InvalidOperationException("Official creation back control is missing.");
        }

        private static void ClickOfficial(CharacterCreationFlowController controller, string nodeName)
        {
            GameObject panel = (GameObject)Field(controller, "officialPanel").GetValue(controller);
            Require(panel != null, "The official creation panel is unavailable.");
            foreach (Transform candidate in panel.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != nodeName || !candidate.gameObject.activeInHierarchy) continue;
                Button button = candidate.GetComponent<Button>() ?? candidate.GetComponentInChildren<Button>(true);
                Require(button != null && button.interactable, "Official control has no active Button: " + nodeName);
                Click(button);
                return;
            }
            throw new InvalidOperationException("Official character creation control is missing: " + nodeName);
        }

        private static void AdvanceOpeningDialog()
        {
            foreach (Button button in UnityEngine.Object.FindObjectsOfType<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy || !button.interactable) continue;
                if (!button.name.EndsWith("NextBtn", StringComparison.Ordinal)) continue;
                Click(button);
                return;
            }
        }

        private static TMP_Text FindText(string value)
        {
            foreach (TMP_Text text in UnityEngine.Object.FindObjectsOfType<TMP_Text>(true))
                if (text.isActiveAndEnabled && text.text != null && text.text.Contains(value)) return text;
            return null;
        }

        private static IEnumerator Capture(string name)
        {
            string directory = Environment.GetEnvironmentVariable("ZJCS_OPENING_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) yield break;
            Directory.CreateDirectory(directory);
            yield return null;
            Canvas.ForceUpdateCanvases();
            string path = Path.Combine(directory, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return WaitUntil(() => File.Exists(path), "Screenshot was not rendered: " + name);
            Debug.Log("[Opening acceptance] snapshot " + path);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string error)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 120.0;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException(error);
                yield return null;
            }
        }

        private static void ClickByLabel(CharacterCreationFlowController controller, string label)
        {
            GameObject screen = (GameObject)Field(controller, "screen").GetValue(controller);
            Require(screen != null, "The creation screen is unavailable. " + CurrentStatus(controller));
            foreach (Button button in screen.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy || !button.interactable) continue;
                foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
                    if (text.text == label) { Click(button); return; }
                foreach (Text text in button.GetComponentsInChildren<Text>(true))
                    if (text.text == label) { Click(button); return; }
            }
            throw new InvalidOperationException("Opening acceptance button was not found: " + label);
        }

        private static void Click(Button button)
        {
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            }, ExecuteEvents.pointerClickHandler);
        }

        private static string CurrentStatus(CharacterCreationFlowController controller)
        {
            Component status = (Component)Field(controller, "status").GetValue(controller);
            if (status is TMP_Text tmp) return tmp.text;
            if (status is Text text) return text.text;
            return "no status";
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
                null, new object[] { Enum.ToObject(kind, 1), 720, 1280, "Opening acceptance" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type viewType = editor.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(view, index);
        }
    }
}
