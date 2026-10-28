using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13Skeletons;

namespace Zjcs.Editor.Map13Skeletons
{
    public static class OfficialMap13MecanimAcceptance
    {
        [Serializable] private sealed class Receipt { public Binary native_windows_bundle; }
        [Serializable] private sealed class Binary { public string sha256; }
        [Serializable] private sealed class Proof
        {
            public string manifest = "871_156598", unity_version, graphics_api, native_source_sha256, skeleton_bindings_sha256;
            public string adapter_sha256, geometry_sha256, acceptance_sha256;
            public int original_profiles, state_samples, matrix_fields, transitions, transition_samples, unique_visual_transitions;
            public string public_semantics = "https://us.esotericsoftware.com/spine-unity-main-components-4.2";
            public string coverage = "Original native controller clocks driving original planar geometry; Mix Next transition sampling and actual camera output. Formal prefab parts, lifecycle/input, NPC constraints, saves and Player remain pending.";
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        private static string ModelKey(NativeMap13AnimatorLibrary.Model model) => OfficialMap13SkeletonTools.Hash(
            Encoding.UTF8.GetBytes(model.cab.ToLowerInvariant() + ":" + model.path_id)).Substring(0, 24);
        public static IEnumerator Verify(NativeMap13SkeletonCatalog catalog, Camera camera, RenderTexture target, Texture2D readback)
        {
            OfficialMap13AnimatorAcceptance.VerifyNativeControllers();
            string root = OfficialMap13SkeletonTools.Repository;
            string nativePath = Path.Combine(root, "assets/reference/official-map/map13-native-prop-animators-source-manifest.json");
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(nativePath));
            const string assets = "Assets/Official/World/Map/13/NativePropAnimators/";
            string output = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Missing validation session."), "native-mecanim");
            Directory.CreateDirectory(output);
            using (var library = new NativeMap13AnimatorLibrary(AssetDatabase.LoadAssetAtPath<TextAsset>(assets + "map13-prop-animators.windows.bundle.bytes"),
                AssetDatabase.LoadAssetAtPath<TextAsset>(assets + "execution-cases.json"), receipt.native_windows_bundle.sha256))
            {
                var models = library.Source.models.ToDictionary(ModelKey);
                var entries = library.Source.entries.ToDictionary(e => e.key);
                int profiles = 0, samples = 0, fields = 0, transitions = 0, transitionSamples = 0;
                var visualTransitions = new Dictionary<string, int>();
                var gallery = new Texture2D(768, 768, TextureFormat.RGBA32, false, false);
                gallery.SetPixels(Enumerable.Repeat(Color.clear, 768 * 768).ToArray());
                foreach (var profile in catalog.renderers)
                {
                    var model = models[profile.model]; var entry = entries[model.controller_key]; var data = catalog.Require(profile.model);
                    using (var view = new NativeMap13SkeletonView(data, profile.fields, null, data.name + " original Mecanim"))
                    using (var reference = new NativeMap13SkeletonView(data, profile.fields, null, data.name + " curve reference"))
                    {
                        reference.Transform.gameObject.SetActive(false);
                        var animator = view.Transform.gameObject.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        animator.runtimeAnimatorController = library.Controller(entry.key); animator.Update(0);
                        // This probe advances the native clock explicitly. Camera capture
                        // yields must not add automatic Animator frames between samples.
                        // The prop lifecycle probe separately exercises real engine updates.
                        animator.enabled = false;
                        var pose = new NativeMap13MecanimPose(animator, view, data, profile.fields);
                        pose.Sample(); CheckFinite(view); // Empty controllers retain original setup pose.
                        foreach (var state in entry.states)
                        {
                            OfficialMap13AnimatorAcceptance.Reset(animator, entry);
                            OfficialMap13AnimatorAcceptance.HoldState(animator, entry, Array.IndexOf(entry.states, state));
                            animator.Play(unchecked((int)state.hash), 0, .37f); animator.Update(0); pose.Sample();
                            var current = animator.GetCurrentAnimatorStateInfo(0);
                            Require(unchecked((uint)current.fullPathHash) == state.hash, "Native geometry state changed before sampling.");
                            var expected = string.IsNullOrEmpty(state.clip_key) ? null : data.Source.animations.Single(a => a.name == entries[state.clip_key].name);
                            float time = expected == null || expected.duration <= 0 ? 0 : current.normalizedTime * expected.duration;
                            if (expected != null) time = state.loop ? Mathf.Repeat(time, expected.duration) : Mathf.Clamp(time, 0, expected.duration);
                            reference.ApplyPose(expected, time);
                            for (int bone = 0; bone < view.BoneMatrices.Count; bone++) for (int i = 0; i < 16; i++)
                            {
                                Require(Mathf.Abs(view.BoneMatrices[bone][i] - reference.BoneMatrices[bone][i]) < .00001f,
                                    "Original native clip clock selected different planar geometry: " + entry.name + "/" + state.name);
                                fields++;
                            }
                            samples++;
                        }
                        foreach (var transition in entry.transitions.Where(t => t.duration > 0))
                        {
                            string key = entry.key + ":" + transition.source + ":" + transition.destination;
                            bool capture = !visualTransitions.ContainsKey(key);
                            int row = capture ? visualTransitions.Count : visualTransitions[key];
                            if (capture) visualTransitions.Add(key, row);
                            Require(row < 3, "Unexpected additional original nonzero transition.");
                            OfficialMap13AnimatorAcceptance.Reset(animator, entry);
                            int from = transition.source < 0 ? 0 : transition.source;
                            animator.Play(unchecked((int)entry.states[from].hash), 0, .99f); animator.Update(0); pose.Sample();
                            if (capture) yield return Capture(camera, target, readback, view, colors => gallery.SetPixels(0, (2 - row) * 256, 256, 256, colors));
                            foreach (var condition in transition.conditions)
                            {
                                var parameter = entry.parameters.Single(p => p.name == condition.name);
                                if (parameter.type == 9) animator.SetTrigger(condition.name); else animator.SetBool(condition.name, condition.mode == 1);
                            }
                            animator.Update(transition.has_exit_time ? animator.GetCurrentAnimatorStateInfo(0).length * .02f + .001f : .001f);
                            Require(animator.IsInTransition(0) && animator.GetAnimatorTransitionInfo(0).duration == transition.duration,
                                "Original native geometry transition clock differs: " + entry.name + "/" + transition.source + "->" + transition.destination + "; current=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash + "; transition=" + animator.IsInTransition(0));
                            float weight = -1;
                            for (int step = 0; step <= 10; step++)
                            {
                                pose.Sample(); CheckFinite(view); transitionSamples++;
                                if (animator.IsInTransition(0))
                                {
                                    var next = animator.GetNextAnimatorClipInfo(0);
                                    Require(next.Length == 1 && next[0].weight >= weight, "Original incoming native clip weight did not progress.");
                                    weight = next[0].weight;
                                }
                                if (capture && (step == 5 || step == 10))
                                {
                                    int column = step == 5 ? 1 : 2;
                                    yield return Capture(camera, target, readback, view, colors => gallery.SetPixels(column * 256, (2 - row) * 256, 256, 256, colors));
                                }
                                if (step < 10) animator.Update(transition.duration / 10 + .0001f);
                            }
                            Require(unchecked((uint)animator.GetCurrentAnimatorStateInfo(0).fullPathHash) == entry.states[transition.destination].hash,
                                "Original native geometry transition did not finish.");
                            Debug.Log("Original Mecanim geometry transition: " + entry.name + ", source=" + transition.source + ", duration=" + transition.duration);
                            transitions++;
                        }
                        profiles++;
                    }
                    yield return null;
                }
                gallery.Apply(); File.WriteAllBytes(Path.Combine(output, "original-transitions.png"), gallery.EncodeToPNG()); UnityEngine.Object.Destroy(gallery);
                string Hash(string path) => OfficialMap13SkeletonTools.Hash(File.ReadAllBytes(path));
                var proof = new Proof { unity_version = Application.unityVersion, graphics_api = SystemInfo.graphicsDeviceType.ToString(),
                    native_source_sha256 = Hash(nativePath), skeleton_bindings_sha256 = Hash(Path.Combine(root, "assets/reference/official-map/map13-native-skeleton-model-bindings-source-manifest.json")),
                    adapter_sha256 = Hash("Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13MecanimPose.cs"),
                    geometry_sha256 = Hash("Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13SkeletonView.cs"),
                    acceptance_sha256 = Hash("Assets/Game/Editor/Map13Skeletons/OfficialMap13MecanimAcceptance.cs"),
                    original_profiles = profiles, state_samples = samples, matrix_fields = fields, transitions = transitions,
                    transition_samples = transitionSamples, unique_visual_transitions = visualTransitions.Count };
                File.WriteAllText(Path.Combine(root, "assets/reference/official-map/map13-native-mecanim-pose-source-manifest.json"), JsonUtility.ToJson(proof, true) + "\n", new UTF8Encoding(false));
                Debug.Log($"Original Map13 Mecanim geometry passed: {profiles} profiles, {samples} state samples, {fields} matrix fields, {transitions} transitions, {transitionSamples} mixed samples.");
            }
        }
        private static void CheckFinite(NativeMap13SkeletonView view)
        {
            foreach (var matrix in view.BoneMatrices) for (int i = 0; i < 16; i++)
                Require(!float.IsNaN(matrix[i]) && !float.IsInfinity(matrix[i]), "Native mixed geometry is not finite.");
        }
        private static IEnumerator Capture(Camera camera, RenderTexture target, Texture2D readback, NativeMap13SkeletonView view, Action<Color[]> consume)
        {
            yield return null; // Let Unity refresh renderer bounds after the native mesh update.
            camera.aspect = (float)target.width / target.height;
            Bounds bounds = default; bool first = true;
            foreach (var renderer in view.Transform.GetComponentsInChildren<MeshRenderer>()) if (renderer.enabled)
            { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
            camera.orthographicSize = Mathf.Max(.001f, Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.15f);
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 20);
            yield return null; yield return null;
            var previous = RenderTexture.active; RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); readback.Apply(); RenderTexture.active = previous;
            consume(readback.GetPixels());
        }
    }
}
