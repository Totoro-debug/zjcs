using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static partial class OfficialBattleMonsterRuntimeValidation
    {
        private const string Active = "Zjcs.BattleLiveMonster.Validation";
        private static bool ready;
        private static IEnumerator<int> steps;
        private static BattleNativeMonsterAssets assets;
        private static Camera camera;
        private static RenderTexture target;
        private static Transform root;
        static OfficialBattleMonsterRuntimeValidation()
        {
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
            EditorApplication.update += Update;
        }
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(renderer, "Assets/monster-validation-renderer.asset");
            AssetDatabase.CreateAsset(pipeline, "Assets/monster-validation-pipeline.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorApplication.EnterPlaymode();
        }
        public static void RunAnimator()
        {
            SessionState.SetBool(Active + ".Animator", true);
            Run();
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0))
                    throw new TimeoutException("Original monster Play Mode validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                steps ??= (SessionState.GetBool(Active + ".Animator", false) ? AnimatorSteps() : Steps()).GetEnumerator();
                if (!steps.MoveNext()) Finish(0);
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static IEnumerable<int> Steps()
        {
            string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            assets = BattleNativeMonsterAssets.Load();
            if (assets.EntityIds.Count() != 20) throw new InvalidDataException("Original monster inventory differs.");
            target = new RenderTexture(450, 800, 24, RenderTextureFormat.ARGB32); target.Create();
            camera = new GameObject("Monster validation camera").AddComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .16f, .2f); camera.targetTexture = target;
            var light = new GameObject("Validation light").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
            int models = 0, motions = 0, samples = 0;
            foreach (int id in assets.EntityIds.OrderBy(value => value))
            {
                var row = assets.Find(id);
                foreach (var value in ((Dictionary<string, object>)row["textures"]).Values)
                {
                    var expected = (Dictionary<string, object>)value;
                    var texture = assets.Resolve<Texture2D>((string)expected["file"]);
                    var sampling = (Dictionary<string, object>)expected["settings"];
                    if (texture.width != Convert.ToInt32(expected["width"]) || texture.height != Convert.ToInt32(expected["height"])
                        || texture.mipmapCount != Convert.ToInt32(expected["mipCount"]) || texture.format != TextureFormat.RGBA32
                        || texture.isDataSRGB != (Convert.ToInt32(expected["colorSpace"]) == 1)
                        || (int)texture.filterMode != Convert.ToInt32(sampling["m_FilterMode"])
                        || (int)texture.wrapModeU != Convert.ToInt32(sampling["m_WrapU"])
                        || (int)texture.wrapModeV != Convert.ToInt32(sampling["m_WrapV"])
                        || texture.anisoLevel != Convert.ToInt32(sampling["m_Aniso"]))
                        throw new InvalidDataException("Original monster texture settings differ: " + texture.name);
                }
                var native = BattleNativeMonsterRenderer.Create(assets, id, null, "BattleUnit_" + id);
                root = native.EntityRoot;
                foreach (string name in native.AnimationNames)
                {
                    if (!native.Play(name, false, true)) throw new InvalidDataException("Original monster action rejected: " + name);
                    var channel = new ProfessionNativeAnimationChannels(native.State.Source, name, originalBezierSegments: true);
                    for (int sample = 0; sample <= 8; sample++)
                    {
                        native.Advance(sample == 0 ? 0 : channel.Duration / 8);
                        var mesh = native.GetComponent<MeshFilter>().sharedMesh;
                        if (!mesh || mesh.vertices.Any(v => !float.IsFinite(v.x) || !float.IsFinite(v.y)))
                            throw new InvalidDataException("Original live monster mesh is invalid.");
                        samples++;
                    }
                    motions++;
                }
                native.Play("idle_01", true, true); native.Advance(.25f);
                Bounds bounds = native.GetComponent<MeshRenderer>().bounds;
                if (bounds.size.x <= 0 || bounds.size.y <= 0) throw new InvalidDataException("Original live monster bounds are empty: " + id);
                camera.transform.position = bounds.center + new Vector3(0, 0, -10);
                camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.2f;
                yield return 0; yield return 0;
                RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(work, "monster-" + id + ".png"), pixels.EncodeToPNG());
                var colors = pixels.GetPixels32(); var background = colors[0];
                int colored = colors.Count(p => Math.Abs(p.r - background.r) > 8 || Math.Abs(p.g - background.g) > 8 || Math.Abs(p.b - background.b) > 8);
                if (colors.Count(p => p.r > 240 && p.b > 240 && p.g < 15) > 100)
                    throw new InvalidDataException("Original monster shader failed: " + id);
                UnityEngine.Object.Destroy(pixels); RenderTexture.active = previous;
                if (colored < 100) throw new InvalidDataException("Original monster did not render: " + id);
                Debug.Log("[Original live monster] " + id + " pixels=" + colored + " bones=" + native.State.Pose.Count);
                UnityEngine.Object.Destroy(root.gameObject); root = null; models++;
                yield return 0;
            }
            File.WriteAllText(Path.Combine(work, "monster-runtime-result.json"), "{\"models\":" + models + ",\"motions\":" + motions + ",\"samples\":" + samples + "}");
            Debug.Log("[Original live monster runtime] models=" + models + " motions=" + motions + " samples=" + samples);
        }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            SessionState.SetBool(Active + ".Animator", false);
            if (root) UnityEngine.Object.Destroy(root.gameObject);
            if (camera) { camera.targetTexture = null; UnityEngine.Object.Destroy(camera.gameObject); }
            if (target) { target.Release(); UnityEngine.Object.Destroy(target); }
            EditorApplication.Exit(code);
        }
        private static Vector3 Vector(object value) { var v = (Dictionary<string, object>)value; return new Vector3(Number(v, "x"), Number(v, "y"), Number(v, "z")); }
        private static float Number(Dictionary<string, object> value, string key) => Convert.ToSingle(value[key]);
    }
}
