using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Opening;

namespace Zjcs.Editor.CharacterCreation
{
    /// <summary>
    /// Visual acceptance for the migrated opening stage: mounts the official game_902
    /// battlefield, spawns the record units, plays the BeforeFight dialogue and captures
    /// the runtime frames. It deliberately does not go through character creation, so it
    /// can run in an isolated project that only contains the opening module.
    /// </summary>
    public static class CharacterOpeningVisualPlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.OpeningVisual.Active";
        private const string ResultKey = "Zjcs.OpeningVisual.Result";
        private const string DeadlineKey = "Zjcs.OpeningVisual.Deadline";
        private static bool started;

        [MenuItem("Zjcs/Validate/Opening Visual Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running the visual acceptance.");
            SessionState.SetInt(ResultKey, 2);
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 420f);
            SessionState.SetBool(ActiveKey, true);
            started = false;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= Locate;
            EditorApplication.update += Locate;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.update -= Locate;
            EditorApplication.update += Locate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Locate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            bool passed = SessionState.GetInt(ResultKey, 1) == 0;
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode
                || Environment.GetEnvironmentVariable("ZJCS_OPENING_VISUAL_AUTOMATION") == "1")
                EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Locate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Opening visual acceptance exceeded its deadline."));
                return;
            }
            if (started) return;
            started = true;
            var host = new GameObject("OpeningVisualHost");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<VisualRunner>();
        }

        internal static void Finish(bool passed, Exception error)
        {
            if (SessionState.GetInt(ResultKey, 1) != 2) return;
            if (error != null)
            {
                Debug.LogException(error);
                Debug.LogError("Opening visual acceptance failed: " + error.Message);
            }
            else
            {
                Debug.Log("Opening visual acceptance passed: official battlefield batches/particles/camera, "
                          + "record units, BeforeFight dialogue and fast-forward all rendered and advanced.");
            }
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private sealed class VisualRunner : MonoBehaviour
        {
            private sealed class Session
            {
                public OpeningUiView Ui;
                public OpeningBattlefield Battlefield;
                public OpeningFightPlayback Playback;
                public OpeningStoryPlayer Story;
            }

            private void Start()
            {
                StartCoroutine(Run());
            }

            private IEnumerator Run()
            {
                var session = new Session();
                Exception error = null;
                yield return Pump(Body(session), value => error = value);
                if (session.Playback != null) session.Playback.Dispose();
                if (session.Story != null) session.Story.Dispose();
                if (session.Battlefield != null) session.Battlefield.Dispose();
                if (session.Ui != null) session.Ui.Dispose();
                Finish(error == null, error);
            }

            private static IEnumerator Pump(IEnumerator root, Action<Exception> failed)
            {
                var stack = new System.Collections.Generic.Stack<IEnumerator>();
                stack.Push(root);
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
                        failed(error);
                        yield break;
                    }
                    if (!moved) { stack.Pop(); continue; }
                    if (value is IEnumerator nested) { stack.Push(nested); continue; }
                    yield return value;
                }
            }

            private IEnumerator Body(Session session)
            {
                Log("step: loading opening assets");
                OpeningAssets assets = OpeningAssets.LoadDefault();
                Log("step: opening assets loaded");
                TMP_FontAsset font = TMP_Settings.defaultFontAsset;
                try
                {
                    OfficialBattleHudCatalog hud = OfficialBattleHudCatalog.LoadDefault();
                    if (hud != null && hud.Font != null) font = hud.Font;
                }
                catch (Exception error)
                {
                    Log("official HUD font unavailable, using TMP default: " + error.Message);
                }
                Log("step: creating opening UI (font=" + (font == null ? "null" : font.name) + ")");
                session.Ui = new OpeningUiView(assets.Ui, font, null, 60);
                Log("step: opening UI created");
                Log("step: loading battlefield");
                session.Battlefield = OpeningBattlefield.Load(transform, Log);
                Log("step: battlefield loaded");
                Require(session.Battlefield.BatchCount == 142,
                    "battlefield batches " + session.Battlefield.BatchCount);
                Require(session.Battlefield.ParticleCount == 23,
                    "battlefield particles " + session.Battlefield.ParticleCount);
                Require(session.Battlefield.StageCamera != null && session.Battlefield.StageCamera.orthographic,
                    "official battlefield camera missing");
                foreach (Transform child in session.Battlefield.Root.GetComponentsInChildren<Transform>(true))
                {
                    MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                    if (renderer == null) continue;
                    Require(renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null,
                        "battlefield batch has no texture: " + child.name);
                }
                yield return Capture("visual-battlefield");

                var audio = gameObject.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                session.Story = new OpeningStoryPlayer(assets, session.Ui, audio, Log, _ => { }, () => { }, () => false);
                Coroutine storyRoutine = StartCoroutine(session.Story.Play("BeforeFight"));
                float deadline = Time.realtimeSinceStartup + 60f;
                TMP_Text line = null;
                while (line == null && Time.realtimeSinceStartup < deadline)
                {
                    line = FindText("又到最后的时刻了，我们上吧！");
                    yield return null;
                }
                Require(line != null, "official BeforeFight dialogue did not render");
                Require(FindText("薇兰") != null, "official BeforeFight role name did not render");
                yield return Capture("visual-before-fight");

                Log("step: loading battle model catalog");
                StopCoroutine(storyRoutine);
                session.Story.Abort();
                yield return null;
                var models = OfficialBattleModelCatalog.LoadDefault();
                Log("step: catalog loaded, bindings=" + (models == null ? -1 : models.Count));
                session.Playback = new OpeningFightPlayback(assets, session.Ui, models, session.Battlefield.UnitRoot,
                    session.Battlefield, () => false, Log);
                yield return session.Playback.Prepare();
                Require(session.Playback.PreparedUnits > 0, "record units were not prepared");
                Require(session.Playback.SpawnedUnits > 0, "record units did not render");
                Debug.Log("[Opening visual] units prepared=" + session.Playback.PreparedUnits
                          + " rendered=" + session.Playback.SpawnedUnits);
                foreach (MeshRenderer renderer in session.Battlefield.UnitRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Debug.Log("[Opening visual] unit " + renderer.name + " pos=" + renderer.transform.position
                              + " scale=" + renderer.transform.lossyScale.x.ToString("F4")
                              + " order=" + renderer.sortingOrder
                              + " tex=" + (renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null
                                  ? renderer.sharedMaterial.mainTexture.name : "null")
                              + " bounds=" + renderer.bounds.size
                              + " verts=" + (renderer.GetComponent<MeshFilter>() != null
                                  && renderer.GetComponent<MeshFilter>().sharedMesh != null
                                  ? renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount : -1));
                }
                yield return Capture("visual-units");
                Transform first = null;
                foreach (MeshRenderer renderer in session.Battlefield.UnitRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    first = renderer.transform;
                    break;
                }
                Camera stage = session.Battlefield.StageCamera;
                Vector3 savedPosition = stage.transform.position;
                float savedSize = stage.orthographicSize;
                if (first != null)
                {
                    stage.transform.position = new Vector3(first.position.x, first.position.y + 0.6f, savedPosition.z);
                    stage.orthographicSize = 1.5f;
                    yield return null;
                    yield return null;
                    yield return Capture("visual-unit-closeup");
                    stage.transform.position = savedPosition;
                    stage.orthographicSize = savedSize;
                }
                StartCoroutine(session.Playback.Play());
                float roundDeadline = Time.realtimeSinceStartup + 15f;
                while (session.Playback.Round < 1 && Time.realtimeSinceStartup < roundDeadline) yield return null;
                Require(session.Playback.TimelineItems > 0, "official action timeline was not populated");
                Debug.Log("[Opening visual] timeline items=" + session.Playback.TimelineItems
                          + " round=" + session.Playback.Round);
                yield return Capture("visual-timeline");
                float damageDeadline = Time.realtimeSinceStartup + 30f;
                while (session.Playback.DamageNumbers == 0 && Time.realtimeSinceStartup < damageDeadline)
                    yield return null;
                Require(session.Playback.DamageNumbers > 0, "record damage numbers were not rendered");
                Debug.Log("[Opening visual] damage numbers=" + session.Playback.DamageNumbers);
                float actionDeadline = Time.realtimeSinceStartup + 30f;
                while (session.Playback.ActionAnimations == 0 && Time.realtimeSinceStartup < actionDeadline)
                    yield return null;
                Require(session.Playback.ActionAnimations > 0, "record action animations were not driven");
                Debug.Log("[Opening visual] action animations=" + session.Playback.ActionAnimations);
                yield return Capture("visual-damage");
                if (Environment.GetEnvironmentVariable("ZJCS_OPENING_FULL_PLAYBACK") == "1")
                {
                    float fullDeadline = Time.realtimeSinceStartup + 300f;
                    while (!session.Playback.Finished && Time.realtimeSinceStartup < fullDeadline)
                    {
                        AdvanceDialog();
                        yield return null;
                    }
                    Require(session.Playback.Finished, "full record playback did not finish");
                    Require(session.Playback.DieActions > 0, "record death actions were not observed");
                    Require(session.Playback.DeathHolds > 0, "defeated units were not held for their death animation");
                    Debug.Log("[Opening visual] full playback: die actions=" + session.Playback.DieActions
                              + " death holds=" + session.Playback.DeathHolds
                              + " actions=" + session.Playback.ActionAnimations
                              + " damage=" + session.Playback.DamageNumbers);
                }
                else
                {
                    session.Playback.RequestFastForward();
                }
                float playDeadline = Time.realtimeSinceStartup + 60f;
                while (!session.Playback.Finished && Time.realtimeSinceStartup < playDeadline) yield return null;
                Require(session.Playback.Finished, "fast-forward did not finish the record");
                yield return Capture("visual-after-fight");
            }
        }

        private static void AdvanceDialog()
        {
            foreach (Button button in UnityEngine.Object.FindObjectsOfType<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy || !button.interactable) continue;
                if (!button.name.EndsWith("NextBtn", StringComparison.Ordinal)) continue;
                button.onClick.Invoke();
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
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Debug.Log("[Opening visual] snapshot " + path);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Log(string message) => Debug.Log("[Opening visual] " + message);
    }
}
