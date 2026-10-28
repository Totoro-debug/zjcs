using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13Skeletons;

namespace Zjcs.Editor.Map13Skeletons
{
    [InitializeOnLoad]
    public static class OfficialMap13SkeletonAcceptance
    {
        private const string Key = "Map13.NativeSkeletonExecution";
        private static bool started, error;
        static OfficialMap13SkeletonAcceptance()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
                Application.logMessageReceived -= Observe; SessionState.SetBool(Key, false);
                GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".pipeline", ""));
                QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key + ".quality", ""));
                PlayerSettings.colorSpace = (ColorSpace)SessionState.GetInt(Key + ".color", 0);
                EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
            };
        }
        public static void ImportAndRun()
        {
            try
            {
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                OfficialMap13SkeletonTools.RestoreAndValidate();
                SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
                SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
                SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
                GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
                QualitySettings.renderPipeline = null; PlayerSettings.colorSpace = ColorSpace.Linear;
                SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
                SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.isPlaying = true;
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void RunPropPartsOnly()
        {
            SessionState.SetBool(Key + ".partsOnly", true); ImportAndRun();
        }
        public static void RunAttachmentOnly()
        {
            SessionState.SetBool(Key + ".attachmentOnly", true); ImportAndRun();
        }
        private static void Observe(string text, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = true; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0)) { Debug.LogError("Native Map13 skeleton execution timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Map13 native skeleton execution").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            Application.logMessageReceived += Observe; var stack = new Stack<IEnumerator>(); stack.Push(VerifySteps());
            while (stack.Count > 0)
            {
                object next;
                try
                {
                    var steps = stack.Peek();
                    if (!steps.MoveNext()) { stack.Pop(); continue; }
                    next = steps.Current;
                    if (next is IEnumerator nested && !(next is CustomYieldInstruction)) { stack.Push(nested); continue; }
                }
                catch (Exception e) { Debug.LogException(e); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (!error) SessionState.SetInt(Key + ".result", 0);
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator VerifySteps()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            if (SessionState.GetBool(Key + ".attachmentOnly", false))
            {
                var attachmentLight = new GameObject("Original attachment diagnostic illumination").AddComponent<Light2D>();
                attachmentLight.lightType = Light2D.LightType.Global; attachmentLight.intensity = 1;
                yield return OfficialMap13AttachmentFollowerAcceptance.Verify(); yield break;
            }
            if (SessionState.GetBool(Key + ".partsOnly", false))
            {
                var partLight = new GameObject("Original parts diagnostic illumination").AddComponent<Light2D>();
                partLight.lightType = Light2D.LightType.Global; partLight.intensity = 1;
                yield return OfficialMap13PropPartsAcceptance.Verify(); yield break;
            }
            VerifyAffineAndClipping();
            var catalog = Resources.Load<NativeMap13SkeletonCatalog>("Map13/Native/Skeletons/catalog");
            if (!catalog || catalog.models.Length != 27 || catalog.renderers.Length != 57) throw new InvalidDataException("Runtime native skeleton inventory differs.");
            var camera = new GameObject("Native skeleton verification camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.nearClipPlane = .01f; camera.farClipPlane = 1000;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var light = new GameObject("Native geometry diagnostic global illumination").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1;
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create(); camera.targetTexture = target; camera.aspect = 1;
            var readback = new Texture2D(256, 256, TextureFormat.RGBA32, false, false);
            int poses = 0, animations = 0, finite = 0, pixels = 0, index = 0;
            var best = new Dictionary<string, Tuple<NativeMap13Animation, float>>();
            foreach (var model in catalog.models)
            {
                var profile = catalog.renderers.First(p => p.model == model.key);
                using (var view = new NativeMap13SkeletonView(model.data, profile.fields, null, model.data.name))
                {
                    int most = -1;
                    foreach (var animation in model.data.Source.animations)
                    {
                        animations++;
                        var times = new SortedSet<float> { 0, animation.duration };
                        foreach (var track in animation.tracks) foreach (var k in track.keys) times.Add(k.time);
                        foreach (var k in animation.attachments) times.Add(k.time);
                        foreach (var k in animation.drawOrders) times.Add(k.time);
                        var keys = times.ToArray(); for (int i = 0; i + 1 < keys.Length; i++) times.Add(Mathf.Lerp(keys[i], keys[i + 1], .37f));
                        foreach (float time in times)
                        {
                            try { view.ApplyPose(animation, time); }
                            catch (Exception e) { throw new InvalidDataException(model.data.name + " / " + animation.name + " / " + time, e); }
                            poses++;
                            foreach (var matrix in view.BoneMatrices) for (int k = 0; k < 16; k++)
                            { if (float.IsNaN(matrix[k]) || float.IsInfinity(matrix[k])) throw new InvalidDataException("Non-finite source pose."); finite++; }
                            if (view.VisibleTriangles > most && view.LocalBounds.size.sqrMagnitude > .000001f)
                            { most = view.VisibleTriangles; best[model.key] = Tuple.Create(animation, time); }
                            if (poses % 64 == 0) yield return null;
                        }
                    }
                    // Named playback must naturally finish; looping must remain active.
                    var chosen = best[model.key]; view.Play(chosen.Item1.name, false, true); view.Tick(chosen.Item1.duration + .1f);
                    if (!view.IsAnimationFinished) throw new InvalidDataException("Non-looping source animation did not finish.");
                    view.Play(chosen.Item1.name, true, true); view.Tick(chosen.Item1.duration + .1f);
                    if (chosen.Item1.duration > 0 && view.IsAnimationFinished) throw new InvalidDataException("Looping source animation finished.");
                }
                yield return null;
            }
            var gallery = new Texture2D(1024, 1024, TextureFormat.RGBA32, false, false);
            gallery.SetPixels(Enumerable.Repeat(Color.clear, 1024 * 1024).ToArray());
            foreach (var profile in catalog.renderers)
            {
                var data = catalog.Require(profile.model); var chosen = best[profile.model];
                using (var view = new NativeMap13SkeletonView(data, profile.fields, null, data.name))
                {
                    view.ApplyPose(chosen.Item1, chosen.Item2); yield return null;
                    // Diagnostic framing only. Source vertex positions and original renderer
                    // profile/material are preserved; this does not stand in for the map layout.
                    var renderBounds = new Bounds(); bool first = true;
                    foreach (var renderer in view.Transform.GetComponentsInChildren<MeshRenderer>()) if (renderer.enabled)
                    { if (first) { renderBounds = renderer.bounds; first = false; } else renderBounds.Encapsulate(renderer.bounds); }
                    camera.orthographicSize = Mathf.Max(renderBounds.extents.x, renderBounds.extents.y) * 1.15f;
                    camera.transform.position = new Vector3(renderBounds.center.x, renderBounds.center.y, renderBounds.min.z - 20);
                    yield return null; yield return null;
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); readback.Apply(); RenderTexture.active = previous;
                    Color[] rendered = readback.GetPixels(); int lit = rendered.Count(p => p.a > .01f && Mathf.Max(p.r, p.g, p.b) > .01f);
                    if (lit < 16) throw new InvalidDataException("Original skeleton profile rendered empty: " + data.name + ", " + profile.cab + ":" + profile.path_id + ", bounds=" + renderBounds);
                    pixels += lit; int tile = index % 16;
                    gallery.SetPixels(tile % 4 * 256, (3 - tile / 4) * 256, 256, 256, rendered);
                    Debug.Log($"Original Map13 native draw {index}: {data.name}, {profile.cab}:{profile.path_id}, {lit} pixels, {view.VisibleTriangles} triangles.");
                    index++;
                    if (index % 16 == 0 || index == catalog.renderers.Length)
                    {
                        string session = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                        if (string.IsNullOrEmpty(session)) throw new InvalidDataException("Set the native skeleton validation output session.");
                        gallery.Apply(); string folder = Path.Combine(session, "native-skeletons"); Directory.CreateDirectory(folder);
                        File.WriteAllBytes(Path.Combine(folder, "original-profiles-" + ((index - 1) / 16) + ".png"), gallery.EncodeToPNG());
                        gallery.SetPixels(Enumerable.Repeat(Color.clear, 1024 * 1024).ToArray());
                    }
                }
                yield return null;
            }
            if (error) throw new InvalidDataException("Native skeleton execution logged runtime errors.");
            OfficialMap13SkeletonTools.RecordExecution(new OfficialMap13SkeletonTools.Execution { unity_version = Application.unityVersion,
                graphics_api = SystemInfo.graphicsDeviceType.ToString(), models = 27, animations = animations, poses = poses, finite_matrices = finite,
                original_profiles = index, rendered_pixels = pixels, runtime_errors = 0,
                acceptance_sha256 = OfficialMap13SkeletonTools.Hash(File.ReadAllBytes("Assets/Game/Editor/Map13Skeletons/OfficialMap13SkeletonAcceptance.cs")),
                coverage = "Play Mode: all source key times and an interior time per segment, affine/clipping invariants, named loop/end playback, 57 original material/profile camera draws. Animator transitions, separated prefab parts, full map layout/input and saves are not exercised." });
            Debug.Log($"Original Map13 native skeleton Play Mode passed: 27 models, {animations} animations, {poses} poses, {finite} finite matrix fields, {index} original profile camera draws, {pixels} visible pixels. Formal prefab/Animator/input/save integration remains outstanding.");
            yield return OfficialMap13MecanimAcceptance.Verify(catalog, camera, target, readback);
            yield return OfficialMap13PropPartsAcceptance.Verify();
            yield return OfficialMap13AttachmentFollowerAcceptance.Verify();
            if (error) throw new InvalidDataException("Original native Mecanim geometry execution logged errors.");
            camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(readback); UnityEngine.Object.Destroy(gallery);
        }
        private static void VerifyAffineAndClipping()
        {
            var parent = Matrix4x4.TRS(new Vector3(7, -3), Quaternion.Euler(0, 0, 31), new Vector3(-2, 3, 1));
            var p = NativeMap13SkeletonView.PlanarMatrix(parent, new Vector2(2, 1), 19, new Vector2(4, 5), Vector2.zero, "noScale");
            var x = new Vector2(p.m00, p.m10); var y = new Vector2(p.m01, p.m11);
            if (Mathf.Abs(x.magnitude - 4) > .00001f || Mathf.Abs(y.magnitude - 5) > .00001f || Mathf.Abs(Vector2.Dot(x, y)) > .0001f || p.determinant >= 0)
                throw new InvalidDataException("No-scale inheritance did not preserve lengths, orthogonality and reflection.");
            var only = NativeMap13SkeletonView.PlanarMatrix(parent, new Vector2(2, 1), 0, Vector2.one, Vector2.zero, "onlyTranslation");
            if (Mathf.Abs(only.m00 - 1) > .00001f || Mathf.Abs(only.m11 - 1) > .00001f || Mathf.Abs(only.m01) > .00001f || Mathf.Abs(only.m10) > .00001f)
                throw new InvalidDataException("Translation-only inheritance leaked rotation or scale.");
            var boundary = new[] { Vector2.zero, new Vector2(3, 0), new Vector2(3, 1), new Vector2(1, 1), new Vector2(1, 3), new Vector2(0, 3) };
            foreach (var polygon in new[] { boundary, boundary.Reverse().ToArray() })
            {
                float area = 0;
                foreach (var tri in NativeMap13SkeletonView.ConvexPieces(polygon))
                    area += Mathf.Abs((tri[1].x - tri[0].x) * (tri[2].y - tri[0].y) - (tri[1].y - tri[0].y) * (tri[2].x - tri[0].x)) * .5f;
                if (Mathf.Abs(area - 5) > .00001f) throw new InvalidDataException("Concave clipping changed the source area.");
            }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
