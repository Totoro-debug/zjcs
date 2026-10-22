using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Opening;

namespace Zjcs.Editor.CharacterCreation
{
    /// <summary>
    /// Fault-branch acceptance for the official opening payload. Each case copies the
    /// shipped payload into an isolated folder, removes or corrupts one file, and checks
    /// that the loader reports the documented failure instead of failing silently.
    /// </summary>
    public static class CharacterOpeningFaultPlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.OpeningFault.Active";
        private const string ResultKey = "Zjcs.OpeningFault.Result";
        private const string DeadlineKey = "Zjcs.OpeningFault.Deadline";
        private const string PathKey = "Zjcs.OpeningFault.Path";
        private static bool started;

        [MenuItem("Zjcs/Validate/Opening Fault Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running the fault acceptance.");
            string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "output", "opening-fault-" + Guid.NewGuid().ToString("N"));
            SessionState.SetString(PathKey, root);
            SessionState.SetInt(ResultKey, 2);
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 300f);
            SessionState.SetBool(ActiveKey, true);
            started = false;
            CharacterOpeningFlow.SkipOpeningForAcceptance = false;
            CharacterOpeningFlow.ForceSkipButtonForAcceptance = false;
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
            Cleanup();
            bool passed = SessionState.GetInt(ResultKey, 1) == 0;
            SessionState.SetBool(ActiveKey, false);
            if (Application.isBatchMode
                || Environment.GetEnvironmentVariable("ZJCS_OPENING_FAULT_AUTOMATION") == "1")
                EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Locate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Finish(false, new TimeoutException("Opening fault acceptance exceeded its deadline."));
                return;
            }
            if (started) return;
            started = true;
            var host = new GameObject("OpeningFaultHost");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<FaultRunner>();
        }

        private static void Cleanup()
        {
            OpeningAssets.DirectoryOverride = null;
            OpeningBattlefield.PayloadOverride = null;
            string root = SessionState.GetString(PathKey, string.Empty);
            if (Directory.Exists(root))
            {
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }

        internal static void Finish(bool passed, Exception error)
        {
            if (SessionState.GetInt(ResultKey, 1) != 2) return;
            if (error != null)
            {
                Debug.LogException(error);
                Debug.LogError("Opening fault acceptance failed: " + error.Message);
            }
            else
            {
                Debug.Log("Opening fault acceptance passed: baseline load, missing story, missing record, "
                          + "corrupt UI atlas and missing battlefield payload all report their documented errors, "
                          + "and the flow still opens character creation after a payload failure.");
            }
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private sealed class FaultRunner : MonoBehaviour
        {
            private void Start()
            {
                StartCoroutine(Run());
            }

            private IEnumerator Run()
            {
                string root = SessionState.GetString(PathKey, string.Empty);
                string source = Path.Combine(Application.streamingAssetsPath, OpeningAssets.Folder);
                CopyDirectory(source, root);
                OpeningAssets.DirectoryOverride = root;

                // Baseline: the copied payload still loads.
                OpeningAssets baseline = OpeningAssets.LoadDefault();
                Require(baseline.Story.Graphs.Count == 3 && baseline.Ui.Sprites.Count > 0,
                    "The copied opening payload does not load.");

                // Missing record.
                string record = Path.Combine(root, "opening-record.bytes");
                File.Move(record, record + ".bak");
                RequireThrows<FileNotFoundException>(() => OpeningAssets.LoadDefault(), "missing record");
                File.Move(record + ".bak", record);

                // Missing story graph data.
                string story = Path.Combine(root, "opening-story.json");
                File.Move(story, story + ".bak");
                RequireThrows<FileNotFoundException>(() => OpeningAssets.LoadDefault(), "missing story");
                File.Move(story + ".bak", story);

                // Corrupt UI atlas page (Texture2D.LoadImage must reject it).
                string atlas = Path.Combine(root, "ui", "atlas-1.png");
                byte[] backup = File.ReadAllBytes(atlas);
                File.WriteAllBytes(atlas, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0 });
                OpeningUiData ui = OpeningUiData.Parse(File.ReadAllBytes(Path.Combine(root, "ui", "opening-ui.json")),
                    Path.Combine(root, "ui"));
                bool atlasRejected = false;
                try
                {
                    using (var view = new OpeningUiView(ui, null, null, 60))
                        atlasRejected = false;
                }
                catch (InvalidOperationException)
                {
                    atlasRejected = true;
                }
                Require(atlasRejected, "A corrupt UI atlas page was accepted.");
                File.WriteAllBytes(atlas, backup);

                // Missing battlefield payload.
                OpeningBattlefield.PayloadOverride = Path.Combine(root, "missing", "game_902.json");
                Require(!OpeningBattlefield.PayloadExists, "The missing battlefield payload was reported as present.");
                RequireThrows<FileNotFoundException>(() => OpeningBattlefield.Load(null, _ => { }),
                    "missing battlefield");
                OpeningBattlefield.PayloadOverride = null;

                // Flow-level fallback: a payload failure still opens character creation.
                File.Move(story, story + ".bak");
                var host = new GameObject("OpeningFaultFlow");
                CharacterOpeningFlow flow = host.AddComponent<CharacterOpeningFlow>();
                bool opened = false;
                string slot = "fault-" + Guid.NewGuid().ToString("N");
                flow.Begin(slot, () => opened = true, _ => { });
                float deadline = Time.realtimeSinceStartup + 30f;
                while (!opened && Time.realtimeSinceStartup < deadline) yield return null;
                Require(opened, "A failed payload load did not fall back to character creation.");
                UnityEngine.Object.Destroy(host);
                File.Move(story + ".bak", story);

                OpeningAssets.DirectoryOverride = null;
                Finish(true, null);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void RequireThrows<T>(Action action, string what) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            catch (Exception error)
            {
                throw new InvalidOperationException(what + " threw " + error.GetType().Name + " instead of "
                                                    + typeof(T).Name + ": " + error.Message, error);
            }
            throw new InvalidOperationException(what + " did not throw " + typeof(T).Name + ".");
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(target, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
        }
    }
}
