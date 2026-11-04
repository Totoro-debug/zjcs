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
using Zjcs.Official.StoryAttachment;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class OfficialPartnerNativeAttachmentAcceptance
    {
        private const string Active = "Zjcs.PartnerNativeAttachments";
        static OfficialPartnerNativeAttachmentAcceptance()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    new GameObject("Original partner attachment validation").AddComponent<PartnerNativeAttachmentRunner>();
            };
        }
        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (string.IsNullOrEmpty(workspace) || !project.StartsWith(Path.GetFullPath(workspace) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use an isolated partner attachment validation project.");
            OfficialPartnerNativeBattleRuntimeAcceptance.Import();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
        }
        public static void Finish(Exception error)
        {
            SessionState.SetBool(Active, false);
            if (error != null) Debug.LogException(error);
            else Debug.Log("PARTNER_NATIVE_ATTACHMENTS_PASSED: original Transform hierarchy, registries, application attachment followers and attachment offsets. Weapon SDK followers, prefab HUD/effects and formal battle remain unvalidated.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class PartnerNativeAttachmentRunner : MonoBehaviour
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
                catch (Exception error) { OfficialPartnerNativeAttachmentAcceptance.Finish(error); yield break; }
                yield return current;
            }
            OfficialPartnerNativeAttachmentAcceptance.Finish(errors == 0 ? null : new InvalidDataException("Partner attachment errors: " + errors));
        }
        private IEnumerator Check()
        {
            var catalog = PartnerNativeBattleAssetCatalog.Load();
            var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            var camera = new GameObject("Original partner attachment camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .18f, .2f);
            var target = new RenderTexture(768, 1365, 24); camera.targetTexture = target;
            var light = new GameObject("Validation global 2D light", typeof(Light2D)).GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
            int nodes = 0, entries = 0, samples = 0;
            foreach (int id in catalog.NpcIds.OrderBy(value => value))
            {
                using var actor = new PartnerNativeBattleActor(catalog, id, null, "Original attachment partner " + id);
                var binding = actor.Attachments;
                var sourceNodes = Arr(binding.OriginalRow["nodes"]).Select(Obj).ToArray();
                Require(sourceNodes.Length == actor.OriginalTransforms.Count, "Original hierarchy node count differs.");
                foreach (var row in sourceNodes)
                {
                    Transform node = actor.OriginalTransforms[(string)row["sourceKey"]];
                    string parent = (string)row["parentKey"];
                    Require(node.parent == (parent.Length == 0 ? actor.Root : actor.OriginalTransforms[parent]), "Original hierarchy parent differs.");
                    Require(node.name == (string)row["name"] && node.GetSiblingIndex() == Int(row, "siblingIndex"), "Original hierarchy order/name differs.");
                    Require(node.gameObject.activeSelf == Convert.ToBoolean(row["active"]) && node.gameObject.layer == Int(row, "layer"), "Original hierarchy visibility/layer differs.");
                    if (row.TryGetValue("rect", out object rectValue))
                    {
                        var rect = node as RectTransform; Require(rect, "Original RectTransform was lost.");
                        var data = Obj(rectValue);
                        Near(rect.anchorMin, Vec2(data["m_AnchorMin"])); Near(rect.anchorMax, Vec2(data["m_AnchorMax"]));
                        Near(rect.pivot, Vec2(data["m_Pivot"])); Near(rect.sizeDelta, Vec2(data["m_SizeDelta"]));
                        Near(rect.anchoredPosition, Vec2(data["m_AnchoredPosition"]));
                    }
                    nodes++;
                }
                var agentSource = sourceNodes.SelectMany(row => Arr(row["components"]).Select(Obj)).Single(row => Equals(row["type"], "AttachAgent"));
                var fields = Obj(agentSource["fields"]); var references = Obj(agentSource["references"]);
                Require(binding.Agent.myBoneNameList.SequenceEqual(Arr(fields["myBoneNameList"]).Select(value => (string)value)), "Original registry names/order differ.");
                for (int index = 0; index < binding.Agent.myBoneNameList.Count; index++)
                {
                    Transform expected = references.TryGetValue("/myBoneList/" + index, out object value)
                        ? actor.OriginalTransforms[(string)Obj(value)["sourceKey"]] : null;
                    Require(binding.Agent.myBoneList[index] == expected && binding.Agent.GetBone(binding.Agent.myBoneNameList[index]) == expected,
                        "Original registry identity/null entry differs."); entries++;
                }
                Require(binding.Followers.Count == 1, "Original shadow follower count differs.");
                var controller = (AnimatorController)catalog.Controllers.Find(id);
                var skill = controller.layers[0].stateMachine.states.Select(value => value.state)
                    .First(value => value.motion is AnimationClip clip && clip.name.Contains("/skill_01", StringComparison.Ordinal));
                foreach (var state in new[] { controller.layers[0].stateMachine.defaultState, skill })
                {
                    actor.Animator.Play(state.name, 0, 0); actor.Advance(0);
                    for (int frame = 0; frame < 40; frame++)
                    {
                        actor.Advance(1f / 60f);
                        var follower = binding.Followers[0];
                        var port = (PartnerNativeBattleRendererPort)follower.SkeletonRenderer;
                        var bone = ((Zjcs.Official.ProfessionUI.IProfessionFollowerAnimation)port).Skeleton.FindBone(follower.BoneName);
                        Require(bone != null, "Original shadow bone is unavailable.");
                        if (bone.Active && follower.isActiveAndEnabled)
                        {
                            Near(follower.transform.position, port.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0)));
                            Near(new Vector2(follower.transform.localScale.x, follower.transform.localScale.y), new Vector2(bone.ScaleX, bone.ScaleY));
                        }
                        samples++;
                    }
                }
                var attached = new GameObject("Original attachment offset probe");
                var config = new AttachPosRotCfg { myBoneName = "body_bottom", myOffset = new Vector3(.02f, .03f, .04f),
                    myRotation = Quaternion.Euler(0, 0, 12), myScale = .7f };
                typeof(AttachPosRotCfg).GetMethod("Attach", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                    null, new[] { typeof(GameObject), typeof(Transform) }, null).Invoke(config, new object[] { binding.Agent.gameObject, attached.transform });
                Require(attached.transform.parent == binding.Agent.GetBone("body_bottom"), "Original effect attachment parent differs.");
                Near(attached.transform.localPosition, config.myOffset); Near(attached.transform.localScale, config.ScaleV3);
                actor.Root.localPosition = new Vector3(.6f, -.2f, .1f); actor.Root.localRotation = Quaternion.Euler(0, 0, 11);
                actor.Root.localScale = new Vector3(-1.15f, .8f, 1); actor.Advance(0);
                foreach (var follower in binding.Followers)
                {
                    var port = (PartnerNativeBattleRendererPort)follower.SkeletonRenderer;
                    var bone = ((Zjcs.Official.ProfessionUI.IProfessionFollowerAnimation)port).Skeleton.FindBone(follower.BoneName);
                    if (bone.Active && follower.isActiveAndEnabled)
                        Near(follower.transform.position, port.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0)));
                }
                Bounds? bounds = null;
                foreach (var renderer in actor.Renderers)
                {
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Vector3[] vertices = mesh.vertices; Color32[] colors = mesh.colors32;
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        if (colors.Length == vertices.Length && colors[index].a == 0) continue;
                        Vector3 point = renderer.transform.TransformPoint(vertices[index]);
                        if (bounds.HasValue) { var b = bounds.Value; b.Encapsulate(point); bounds = b; }
                        else bounds = new Bounds(point, Vector3.zero);
                    }
                }
                Require(bounds.HasValue, "Original actor has no visible geometry.");
                camera.transform.position = new Vector3(bounds.Value.center.x, bounds.Value.center.y, bounds.Value.min.z - 10);
                camera.orthographicSize = Mathf.Max(bounds.Value.extents.y, bounds.Value.extents.x / camera.aspect) * 1.12f;
                yield return null;
                Capture(camera, target, "attachments-" + id);
                Destroy(attached);
            }
            Require(nodes == 1085 && samples == 2720, "Original attachment coverage differs.");
            Debug.Log($"Original attachment coverage: actors=34, transforms={nodes}, registry entries={entries}, shadow pose samples={samples}.");
            Destroy(target); Destroy(pipeline);
        }
        private static void Capture(Camera camera, RenderTexture target, string name)
        {
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try { camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"), pixels.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Destroy(pixels); }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        private static void Near(Vector3 a, Vector3 b) { Require(Vector3.Distance(a, b) < .0001f, $"Original attachment vector differs: {a} != {b}"); }
        private static void Near(Vector2 a, Vector2 b) { Require(Vector2.Distance(a, b) < .0001f, $"Original attachment vector differs: {a} != {b}"); }
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static int Int(Dictionary<string, object> value, string key) => Convert.ToInt32(value[key]);
        private static Vector2 Vec2(object value) { var data = Obj(value); return new Vector2(Convert.ToSingle(data["x"]), Convert.ToSingle(data["y"])); }
    }
}
