using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialEmbeddedMecanimProbe : MonoBehaviour
    {
        private string output;
        private Action<bool, string> completed;
        private RenderTexture target;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--zjcs-validate-embedded-mecanim");
            if (index < 0 || index + 1 >= args.Length) return;
            StartDiagnostic(args[index + 1], (passed, error) => Application.Quit(passed ? 0 : 1));
        }
        public static void StartDiagnostic(string output, Action<bool, string> completed)
        {
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output))
                throw new ArgumentException("Run the component diagnostic with an absolute isolated output directory.");
            var host = new GameObject("Original embedded Mecanim component probe");
            DontDestroyOnLoad(host);
            var probe = host.AddComponent<OfficialEmbeddedMecanimProbe>();
            probe.output = Path.GetFullPath(output);
            probe.completed = completed;
            Directory.CreateDirectory(probe.output);
            Application.runInBackground = true;
        }
        private OfflineBattleNativePlayableScope scope;
        private EmbeddedMonsterMecanimResources resources;
        private Camera view;
        private IDisposable pipeline;
        private Exception failure;
        private float previousCapture;

        private IEnumerator Start()
        {
            previousCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60;
            Application.logMessageReceived += Log;
            var routine = Execute();
            while (true)
            {
                object next = null;
                bool more = false;
                try { more = routine.MoveNext(); if (more) next = routine.Current; }
                catch (Exception error) { failure = error; }
                if (!more || failure != null) break;
                yield return next;
            }
            hitPlayback?.Dispose(); hitPlayback = null;
            scope?.Dispose(); resources?.Dispose();
            pipeline?.Dispose(); pipeline = null;
            scope = null; resources = null;
            Application.logMessageReceived -= Log;
            Time.captureDeltaTime = previousCapture;
            if (failure != null) Debug.LogException(failure);
            File.WriteAllText(Path.Combine(output, "result.json"),
                JsonUtility.ToJson(new Result { passed = failure == null, error = failure?.ToString() ?? "" }, true));
            if (view) view.targetTexture = null;
            if (target) Destroy(target);
            completed?.Invoke(failure == null, failure?.ToString() ?? "");
        }

        private IEnumerator Execute()
        {
            Require(!string.IsNullOrEmpty(output) && Path.IsPathRooted(output), "An owned output directory is required.");
            var catalog = EmbeddedMonsterMecanimCatalog.Load();
            pipeline = Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets").Enter();
            resources = new EmbeddedMonsterMecanimResources();
            scope = new OfflineBattleNativePlayableScope(transform, null);
            scope.BindNativePrefabAdapter((source, instance) => resources.BindOriginalPrefab(source, instance));
            var sameName = new GameObject(catalog.SourcePrefab.name);
            Require(resources.BindOriginalPrefab(sameName, sameName) == 0, "A same-name prefab was incorrectly bound.");
            Destroy(sameName);
            view = new GameObject("Original prefab validation camera").AddComponent<Camera>();
            view.transform.SetParent(transform, false);
            view.transform.localPosition = new Vector3(0, 0, -20);
            view.orthographic = true; view.orthographicSize = 1.5f;
            target = new RenderTexture(600, 800, 24, RenderTextureFormat.ARGB32); target.Create();
            view.targetTexture = target;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.12f, .12f, .12f);
            var instances = new System.Collections.Generic.List<GameObject>();
            var natives = new System.Collections.Generic.List<BattleNativeMonsterRenderer>();
            int visibleFrames = 0;
            for (int replay = 0; replay < 2; replay++)
            {
                var instance = scope.Instantiate(catalog.SourcePrefab, transform, false);
                instances.Add(instance);
                instance.transform.localPosition = new Vector3(replay == 0 ? -.75f : .75f, 0, 0);
                var native = instance.GetComponentInChildren<BattleNativeMonsterRenderer>(true);
                Require(native && native.State == null && !native.gameObject.activeInHierarchy,
                    "The authored dormant track was unexpectedly activated.");
                // Explicit component fixture only. Production retains both original inactive parents.
                for (Transform node = native.transform.parent; node != instance.transform; node = node.parent)
                    node.gameObject.SetActive(true);
                Require(native.State != null && native.Animator && native.AnimationNames.Count() == 14,
                    "The original skeleton/controller did not initialize before appearance loading.");
                Require(natives.All(value => !ReferenceEquals(value.State, native.State)), "Embedded instances share their pose state.");
                natives.Add(native);
                Require(native.GetComponents<MonoBehaviour>().All(value => !value || value.GetType().FullName != "Spine.Unity.SkeletonMecanim"),
                    "The obsolete embedded renderer remains bound.");
                float firstClock = native.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                for (int frame = 0; frame < 48; frame++)
                {
                    resources.Update();
                    yield return null;
                    var meshes = native.GetComponentsInChildren<MeshFilter>(true).Where(value =>
                        value.gameObject.activeInHierarchy && value.sharedMesh && value.sharedMesh.vertexCount > 0
                        && value.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled).ToArray();
                    Require(meshes.All(value => value.sharedMesh.vertices.All(v =>
                        float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z))), "Nonfinite native geometry.");
                    bool ready = meshes.Length > 0 && meshes.All(value => value.GetComponent<MeshRenderer>().sharedMaterials.All(material =>
                        material && material.mainTexture && !material.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal)));
                    if (ready) visibleFrames++;
                    if (frame == 15 || frame == 35)
                    {
                        Require(ready && meshes.Any(value => value.GetComponent<MeshRenderer>().isVisible),
                            "The original Low appearance texture did not produce visible geometry.");
                        yield return new WaitForEndOfFrame();
                        var previousTarget = RenderTexture.active;
                        RenderTexture.active = target;
                        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                        RenderTexture.active = previousTarget;
                        var colors = pixels.GetPixels32(); var background = colors[0];
                        Require(colors.Count(color => Math.Abs(color.r - background.r) > 8
                            || Math.Abs(color.g - background.g) > 8 || Math.Abs(color.b - background.b) > 8) > 100,
                            "The actual camera produced no visible original pixels.");
                        Require(colors.Count(color => color.r > 240 && color.b > 240 && color.g < 15) < 100,
                            "The original shader produced fallback pixels.");
                        File.WriteAllBytes(Path.Combine(output, "embedded-mecanim-" + replay + "-" + frame + ".png"), pixels.EncodeToPNG());
                        Destroy(pixels);
                    }
                }
                Require(native.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime > firstClock,
                    "The original Animator clock did not advance.");
                native.gameObject.SetActive(false);
                native.gameObject.SetActive(true);
                Require(native.State != null, "Pooling discarded the native skeleton.");
            }
            Require(visibleFrames > 30, "The original moving geometry did not remain visible.");
            foreach (var instance in instances) Destroy(instance);
            scope.Dispose(); scope = null;
            resources.Dispose(); resources = null;
            yield return null;
            Require(natives.All(value => !value), "Native graph resources survived release.");
            Debug.Log("[Embedded Mecanim] Original 3205 source identity, authored dormancy, two independent component instances, Low atlas, finite geometry, Animator clocks and release passed.");
            var hitCheck = CheckHitEntity();
            while (hitCheck.MoveNext()) yield return hitCheck.Current;
        }

        private void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                failure = failure ?? new InvalidOperationException(message + "\n" + stack);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Embedded Mecanim: " + message); }

        [Serializable] private sealed class Result { public bool passed; public string error; }
    }
}
