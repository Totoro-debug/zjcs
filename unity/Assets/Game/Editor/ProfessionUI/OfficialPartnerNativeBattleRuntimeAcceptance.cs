using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class OfficialPartnerNativeBattleRuntimeAcceptance
    {
        private const string Active = "Zjcs.PartnerNativeRuntimeAssets";
        static OfficialPartnerNativeBattleRuntimeAcceptance()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    new GameObject("Original partner runtime assets validation").AddComponent<PartnerNativeBattleRuntimeRunner>();
            };
        }
        public static void Import()
        {
            OfficialPartnerNativeBattleDataValidation.ImportRuntimeTextures();
            var catalog = PartnerNativeBattleAssetCatalog.Load();
            if (catalog.NpcIds.Count() != 34 || !catalog.Shader.isSupported)
                throw new InvalidDataException("Original partner runtime assets are unavailable.");
            Debug.Log("Original complete-skeleton partner runtime assets imported.");
        }
        public static void ImportAndValidate()
        {
            Import();
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
        }
        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (string.IsNullOrEmpty(workspace) || !project.StartsWith(Path.GetFullPath(workspace) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use an isolated partner runtime validation project.");
            Import(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
        }
        public static void Finish(Exception error)
        {
            SessionState.SetBool(Active, false);
            if (error != null) Debug.LogException(error);
            else Debug.Log("PARTNER_NATIVE_RUNTIME_ASSETS_PASSED: all 34 original local actors, controllers, transforms and material textures. Formal battle invitation and Windows Player remain unvalidated.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class PartnerNativeBattleRuntimeRunner : MonoBehaviour
    {
        private int errors;
        private IEnumerator Start()
        {
            Application.logMessageReceived += (_, __, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            var steps = Check();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { OfficialPartnerNativeBattleRuntimeAcceptance.Finish(error); yield break; }
                yield return current;
            }
            OfficialPartnerNativeBattleRuntimeAcceptance.Finish(errors == 0 ? null : new InvalidDataException("Original partner runtime errors: " + errors));
        }
        private IEnumerator Check()
        {
            var catalog = PartnerNativeBattleAssetCatalog.Load();
            var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            var camera = new GameObject("Original runtime partner camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .18f, .2f);
            var target = new RenderTexture(768, 1365, 24); camera.targetTexture = target;
            var light = new GameObject("Validation global 2D light", typeof(Light2D)).GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
            int renderers = 0, sampled = 0;
            foreach (int id in catalog.NpcIds.OrderBy(value => value))
            {
                using var actor = new PartnerNativeBattleActor(catalog, id, null, "Original runtime partner " + id);
                renderers += actor.Renderers.Count;
                var controller = (AnimatorController)catalog.Controllers.Find(id);
                var skill = controller.layers[0].stateMachine.states.Select(value => value.state)
                    .First(value => value.motion is AnimationClip clip && clip.name.Contains("/skill_01", StringComparison.Ordinal));
                foreach (var state in new[] { controller.layers[0].stateMachine.defaultState, skill })
                {
                    actor.Animator.Rebind(); actor.Animator.Play(controller.layers[0].name + "." + state.name, 0, .25f);
                    actor.Advance(0); CheckTransforms(actor);
                    if (state == skill && actor.LastFrame.Current.Clip.name != ((AnimationClip)skill.motion).name)
                        throw new InvalidDataException("Original runtime partner did not enter its authored skill state: " + id);
                    Bounds? bounds = null;
                    foreach (var renderer in actor.Renderers)
                    {
                        Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        Vector3[] vertices = mesh.vertices; Color32[] colors = mesh.colors32;
                        if (renderer.sharedMaterials.Any(material => !material || material.shader != catalog.Shader || !material.mainTexture))
                            throw new InvalidDataException("Original runtime partner material or texture was lost.");
                        for (int vertex = 0; vertex < vertices.Length; vertex++)
                        {
                            Vector3 point = renderer.transform.TransformPoint(vertices[vertex]);
                            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                                throw new InvalidDataException("Original runtime partner vertex is not finite.");
                            if (colors.Length == vertices.Length && colors[vertex].a == 0) continue;
                            if (bounds.HasValue) { var b = bounds.Value; b.Encapsulate(point); bounds = b; }
                            else bounds = new Bounds(point, Vector3.zero);
                        }
                    }
                    if (!bounds.HasValue) throw new InvalidDataException("Original runtime partner has no visible geometry: " + id);
                    camera.transform.position = new Vector3(bounds.Value.center.x, bounds.Value.center.y, bounds.Value.min.z - 10);
                    camera.orthographicSize = Mathf.Max(bounds.Value.extents.y, bounds.Value.extents.x / camera.aspect) * 1.12f;
                    yield return null;
                    PartnerNativeBattlePoseRunner.Capture(camera, target, "runtime-" + id + "-" + (state == skill ? "skill" : "idle"));
                    for (int frame = 0; frame < 5; frame++) actor.Advance(1f / 60f);
                    sampled++;
                }
            }
            if (renderers != 35 || sampled != 68) throw new InvalidDataException("Original runtime partner coverage differs.");
            Debug.Log("Original runtime partner coverage: actors=34, renderers=35, sampled states=68.");
            Destroy(target); Destroy(pipeline);
        }
        private static void CheckTransforms(PartnerNativeBattleActor actor)
        {
            var source = (List<object>)actor.OriginalRow["transforms"];
            Transform node = actor.Animator.transform;
            for (int index = source.Count - 1; index >= 0; index--)
            {
                var row = (Dictionary<string, object>)source[index];
                if (!node || node.name != (string)row["name"] || node.gameObject.layer != Convert.ToInt32(row["layer"]))
                    throw new InvalidDataException("Original runtime partner hierarchy differs.");
                var position = (Dictionary<string, object>)row["position"]; var scale = (Dictionary<string, object>)row["scale"];
                if ((node.localPosition - Vector(position)).sqrMagnitude > .0000001f || (node.localScale - Vector(scale)).sqrMagnitude > .0000001f)
                    throw new InvalidDataException("Original runtime partner local transform differs.");
                node = node.parent;
            }
        }
        private static Vector3 Vector(Dictionary<string, object> row) => new Vector3(Convert.ToSingle(row["x"]), Convert.ToSingle(row["y"]), Convert.ToSingle(row["z"]));
    }
}
