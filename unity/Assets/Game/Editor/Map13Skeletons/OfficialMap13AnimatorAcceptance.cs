using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13Skeletons;

namespace Zjcs.Editor.Map13Skeletons
{
    [InitializeOnLoad]
    public static class OfficialMap13AnimatorAcceptance
    {
        private const string Source = "Assets/Official/World/Map/13/NativePropAnimators";
        private const string Key = "Map13.NativeAnimatorAcceptance";
        private static bool started, error;
        [Serializable] private sealed class Receipt { public Binary native_windows_bundle; }
        [Serializable] private sealed class Binary { public string sha256; }
        [Serializable] private sealed class Proof
        {
            public string manifest = "871_156598", unity_version, graphics_api;
            public string source_sha256, binary_sha256, cases_sha256, acceptance_sha256, loader_sha256;
            public int controllers, clips, states, parameters, nonzero_transitions;
            public string coverage = "Original native Windows controller/clip loading, state clocks and nonzero transition execution; no map/prefab, pointer, visual or save acceptance.";
        }
        static OfficialMap13AnimatorAcceptance()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
                Application.logMessageReceived -= Observe; SessionState.SetBool(Key, false);
                EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
            };
        }
        public static void Run()
        {
            ValidationOutput();
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.isPlaying = true;
        }
        private static void Observe(string text, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = true; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Original Map13 native animation execution timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Original Map13 Mecanim execution").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            Application.logMessageReceived += Observe;
            try { VerifyNativeControllers(); if (!error) SessionState.SetInt(Key + ".result", 0); }
            catch (Exception e) { Debug.LogException(e); }
            yield return null;
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        private static string ValidationOutput()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(repository))
                throw new InvalidDataException("Set ZJCS_VALIDATION_DIR and ZJCS_REPOSITORY_ROOT for this validation session.");
            string root = Path.GetFullPath(Path.Combine(repository, "output/validation")) + Path.DirectorySeparatorChar;
            output = Path.GetFullPath(output);
            if (!output.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(output))
                throw new InvalidDataException("Native animation validation must use one repository output/validation session.");
            return output;
        }
        private static void Reset(Animator animator, NativeMap13AnimatorLibrary.Entry entry)
        {
            animator.Rebind();
            foreach (var p in entry.parameters)
                if (p.type == 9) animator.ResetTrigger(p.name); else animator.SetBool(p.name, false);
        }
        private static void HoldState(Animator animator, NativeMap13AnimatorLibrary.Entry entry, int index)
        {
            var bools = entry.parameters.Where(p => p.type == 4).ToArray();
            Require(bools.Length < 16, "Native state probe requires too many bool combinations.");
            foreach (int mask in Enumerable.Range(0, 1 << bools.Length))
            {
                bool Fires(NativeMap13AnimatorLibrary.Transition t) => t.conditions.All(c =>
                {
                    int bit = Array.FindIndex(bools, p => p.name == c.name);
                    bool value = bit >= 0 && (mask & (1 << bit)) != 0;
                    return c.mode == 1 ? value : c.mode == 2 ? !value : throw new InvalidDataException("Additional condition type needs verification.");
                });
                if (entry.transitions.Any(t => !t.has_exit_time && (t.source < 0 || t.source == index) && Fires(t))) continue;
                for (int bit = 0; bit < bools.Length; bit++) animator.SetBool(bools[bit].name, (mask & (1 << bit)) != 0);
                return;
            }
            throw new InvalidDataException("Original state has no stable immediate-parameter assignment: " + entry.name + "/" + entry.states[index].name);
        }
        private static void VerifyNativeControllers()
        {
            string receiptPath = Path.Combine(OfficialMap13SkeletonTools.Repository,
                "assets/reference/official-map/map13-native-prop-animators-source-manifest.json");
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(receiptPath));
            using (var library = new NativeMap13AnimatorLibrary(
                AssetDatabase.LoadAssetAtPath<TextAsset>(Source + "/map13-prop-animators.windows.bundle.bytes"),
                AssetDatabase.LoadAssetAtPath<TextAsset>(Source + "/execution-cases.json"), receipt.native_windows_bundle.sha256))
            {
                int clips = 0, controllers = 0, states = 0, parameters = 0, transitions = 0;
                foreach (var entry in library.Source.entries)
                {
                    if (entry.kind == "AnimationClip")
                    {
                        var clip = library.Clip(entry.key);
                        Require(clip.length == entry.duration && clip.isLooping == entry.loop, "Original clip clock/loop differs: " + entry.name);
                        clips++; continue;
                    }
                    var go = new GameObject(entry.name); var animator = go.AddComponent<Animator>();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.runtimeAnimatorController = library.Controller(entry.key); animator.Update(0);
                    Require(animator.layerCount == 1 && animator.GetLayerName(0) == "Base Layer", "Original Mecanim layer differs.");
                    Require(animator.parameters.Length == entry.parameters.Length, "Original Mecanim parameter count differs.");
                    for (int i = 0; i < entry.parameters.Length; i++)
                    {
                        var p = entry.parameters[i]; var actual = animator.parameters[i];
                        Require(actual.name == p.name && (int)actual.type == p.type, "Original Mecanim parameter identity differs."); parameters++;
                    }
                    foreach (var state in entry.states)
                    {
                        Reset(animator, entry); HoldState(animator, entry, Array.IndexOf(entry.states, state));
                        animator.Play(unchecked((int)state.hash), 0, 0); animator.Update(0);
                        var current = animator.GetCurrentAnimatorStateInfo(0);
                        Require(unchecked((uint)current.fullPathHash) == state.hash, "Original native state differs: " + entry.name + "/" + state.name);
                        var info = animator.GetCurrentAnimatorClipInfo(0);
                        if (string.IsNullOrEmpty(state.clip_key)) Require(info.Length == 0, "Original empty state gained a clip.");
                        else
                        {
                            var clip = library.Clip(state.clip_key);
                            Require(info.Length == 1 && info[0].clip == clip, "Original state clip differs: " + entry.name + "/" + state.name);
                            Require(current.loop == state.loop && current.speed == state.speed, "Original native state speed/loop differs.");
                            if (clip.length > 0)
                            {
                                animator.Update(clip.length * .17f);
                                Require(animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0, "Original native clock did not advance: " + entry.name + "/" + state.name);
                            }
                        }
                        states++;
                    }
                    foreach (var transition in entry.transitions.Where(t => t.duration > 0))
                    {
                        Reset(animator, entry);
                        int from = transition.source < 0 ? 0 : transition.source;
                        animator.Play(unchecked((int)entry.states[from].hash), 0, .99f); animator.Update(0);
                        foreach (var condition in transition.conditions)
                        {
                            var parameter = entry.parameters.Single(p => p.name == condition.name);
                            if (parameter.type == 9) animator.SetTrigger(condition.name);
                            else animator.SetBool(condition.name, condition.mode == 1);
                        }
                        animator.Update(transition.has_exit_time ? animator.GetCurrentAnimatorStateInfo(0).length * .02f + .001f : .001f);
                        Require(animator.IsInTransition(0), "Original nonzero transition did not start: " + entry.name + "/" + transition.source + "/" + transition.duration);
                        var actual = animator.GetAnimatorTransitionInfo(0);
                        Require(actual.duration == transition.duration, "Original native transition duration changed: " + entry.name);
                        Require(unchecked((uint)animator.GetNextAnimatorStateInfo(0).fullPathHash) == entry.states[transition.destination].hash,
                            "Original native transition destination differs.");
                        animator.Update(transition.duration + .01f);
                        Require(unchecked((uint)animator.GetCurrentAnimatorStateInfo(0).fullPathHash) == entry.states[transition.destination].hash,
                            "Original native transition did not finish."); transitions++;
                    }
                    UnityEngine.Object.DestroyImmediate(go); controllers++;
                }
                Debug.Log($"Original Map13 native Windows animations verified in Play Mode: {controllers} controllers, {clips} clips, {states} states, {parameters} parameters, {transitions} nonzero transitions; {Application.unityVersion}, {SystemInfo.graphicsDeviceType}.");
                if (!error)
                {
                    string Hash(string path) => OfficialMap13SkeletonTools.Hash(File.ReadAllBytes(path));
                    var proof = new Proof { unity_version = Application.unityVersion, graphics_api = SystemInfo.graphicsDeviceType.ToString(),
                        source_sha256 = Hash(receiptPath), binary_sha256 = receipt.native_windows_bundle.sha256,
                        cases_sha256 = Hash(Source + "/execution-cases.json"),
                        acceptance_sha256 = Hash("Assets/Game/Editor/Map13Skeletons/OfficialMap13AnimatorAcceptance.cs"),
                        loader_sha256 = Hash("Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13AnimatorLibrary.cs"),
                        controllers = controllers, clips = clips, states = states, parameters = parameters, nonzero_transitions = transitions };
                    File.WriteAllText(Path.Combine(OfficialMap13SkeletonTools.Repository,
                        "assets/reference/official-map/map13-native-prop-animators-playmode-source-manifest.json"),
                        JsonUtility.ToJson(proof, true) + "\n", new System.Text.UTF8Encoding(false));
                }
            }
        }
        public static void BuildNativeRoundtrip()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var assets = AssetDatabase.FindAssets("t:AnimatorController t:AnimationClip", new[] { Source })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();
                if (assets.Length != 147) throw new InvalidDataException("Original native Animator/clip inventory differs: " + assets.Length);
                string output = Path.Combine(ValidationOutput(), "native-animator-roundtrip"); Directory.CreateDirectory(output);
                var result = BuildPipeline.BuildAssetBundles(output, new[] {
                    new AssetBundleBuild { assetBundleName = "map13-prop-animators", assetNames = assets }
                }, BuildAssetBundleOptions.UncompressedAssetBundle | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                    BuildTarget.StandaloneWindows64);
                if (!result) throw new InvalidDataException("Windows native animation bundle failed.");
                Debug.Log("Map13 original animator Windows roundtrip built: " + assets.Length);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public sealed class Runner : MonoBehaviour { }
    }
}
