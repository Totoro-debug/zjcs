using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13EntityAssets;
using Zjcs.Offline.World.NativeMap13EntityViews;
using Zjcs.Offline.World.NativeMap13Skeletons;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.World.Map13Attachment;

namespace Zjcs.Editor.Map13TreeModels
{
    // Resource compatibility verification. Full source entity lifecycles and
    // map interaction verification are deliberately outside this fixture.
    [InitializeOnLoad]
    public static class OfficialMap13TreeModelAcceptance
    {
        private const string Key = "Map13.NativeTreeModels";
        private const string SourcePath = "assets/reference/official-map/map13-native-tree-models-source-manifest.json";
        private static bool started, error;
        private static string Repository => Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT") ?? Path.GetFullPath("..");
        private static string Output => Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Validation directory must be explicit.");
        static OfficialMap13TreeModelAcceptance()
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
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(Repository, SourcePath)));
            foreach (var model in source.models) AssetDatabase.ImportAsset(model.path.Substring(6), ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            SessionState.SetString(Key + ".pipeline", AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key + ".quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key + ".color", (int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline = null; PlayerSettings.colorSpace = ColorSpace.Linear;
            SessionState.SetString(Key + ".source", Hash(File.ReadAllBytes(Path.Combine(Repository, SourcePath))));
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.isPlaying = true;
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = true; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Native Map13 tree Model verification timed out."); EditorApplication.Exit(1); return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; new GameObject("Original Map13 native Sprite resource verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            Application.logMessageReceived += Observe;
            var stack = new Stack<IEnumerator>(); stack.Push(Steps());
            while (stack.Count != 0)
            {
                object next;
                try
                {
                    var step = stack.Peek(); if (!step.MoveNext()) { stack.Pop(); continue; }
                    next = step.Current;
                    if (next is IEnumerator nested && !(next is CustomYieldInstruction)) { stack.Push(nested); continue; }
                }
                catch (Exception e) { Debug.LogException(e); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (!error) SessionState.SetInt(Key + ".result", 0);
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            byte[] bytes = File.ReadAllBytes(Path.Combine(Repository, SourcePath));
            Require(Hash(bytes) == SessionState.GetString(Key + ".source", ""), "Tree Model source changed during entry.");
            var source = JsonUtility.FromJson<Source>(System.Text.Encoding.UTF8.GetString(bytes));
            foreach (var output in source.inputs.Concat(source.outputs))
                Require(Hash(File.ReadAllBytes(Path.Combine(Repository, output.path))) == output.sha256, "Tree Model dependency changed: " + output.path);
            var anchors = JsonUtility.FromJson<Hierarchy>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-attachment-followers.json"));
            var originals = JsonUtility.FromJson<Originals>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-prop-hierarchies.json"));
            var indexed = originals.objects.ToDictionary(o => o.key);
            var catalog = Resources.Load<NativeMap13EntitySpriteCatalog>("Map13/Native/entity-sprite-catalog");
            var animatorBytes = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativePropAnimators/map13-prop-animators.windows.bundle.bytes");
            var animatorCatalog = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativePropAnimators/execution-cases.json");
            var modelBytes = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativeTreeModelAnimator/model-animator.windows.bundle.bytes");
            var modelCatalog = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativeTreeModelAnimator/catalog.json");
            int transforms = 0, components = 0, spriteBindings = 0, followerSamples = 0, colorSamples = 0, fadeEvents = 0, pixels = 0;
            var light = new GameObject("Tree Model diagnostic illumination").AddComponent<Light2D>(); light.lightType = Light2D.LightType.Global; light.intensity = 1;
            var camera = new GameObject("Original tree Model camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .08f, .08f, 0);
            camera.nearClipPlane = .01f; camera.farClipPlane = 1000; camera.aspect = 1; camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var target = new RenderTexture(512, 512, 24); target.Create(); camera.targetTexture = target;
            var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            using (var animations = new NativeMap13AnimatorLibrary(animatorBytes, animatorCatalog, "65211c5a2dd5a677d26a0f37f88a518c306e7968425048cab263b54e0951bd4f"))
            using (var sprites = new NativeMap13EntitySpriteLibrary(catalog))
            using (var dissolve = new NativeMap13TreeModelAnimatorLibrary(modelBytes, modelCatalog))
            {
                Require(dissolve.Controllers.Count == 1 && dissolve.Clip.name == "RONGjie" && Mathf.Abs(dissolve.Clip.length - .8333333134651184f) < 1e-7f, "Original Model dissolve clip differs.");
                int modelIndex = 0;
                foreach (var model in source.models)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(model.path.Substring(6)); Require(prefab, "Original Model prefab is absent.");
                    // The original parent/UI transforms are restored as inert
                    // diagnostic anchors. Their full UI/EC behavior is separate.
                    var nodes = new Dictionary<string, Transform>();
                    foreach (var node in anchors.nodes)
                    { var go = new GameObject(node.name); go.SetActive(false); nodes.Add(node.transformKey, go.transform); }
                    foreach (var node in anchors.nodes)
                    {
                        var tf = nodes[node.transformKey]; tf.SetParent(string.IsNullOrEmpty(node.parentTransformKey) ? null : nodes[node.parentTransformKey], false);
                        tf.SetLocalPositionAndRotation(node.pose.m_LocalPosition, node.pose.m_LocalRotation); tf.localScale = node.pose.m_LocalScale; tf.gameObject.layer = node.layer;
                    }
                    var selected = anchors.nodes.Single(n => n.key == model.source); var parent = nodes[selected.parentTransformKey];
                    var old = nodes[selected.transformKey]; UnityEngine.Object.DestroyImmediate(old.gameObject);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); instance.name = prefab.name;
                    var binding = instance.GetComponent<NativeMap13EntityModelBinding>(); Require(binding && binding.SourceKey == model.source, "Original Model binding is absent.");
                    var mapped = new Dictionary<string, UnityEngine.Object>();
                    var identities = model.original_keys.Select((k, i) => new { Key = k, Id = 100001L + i }).ToDictionary(p => p.Id, p => p.Key);
                    foreach (var tf in instance.GetComponentsInChildren<Transform>(true))
                    {
                        var items = new UnityEngine.Object[] { tf.gameObject }.Concat(tf.GetComponents<Component>().Cast<UnityEngine.Object>());
                        foreach (var value in items)
                        {
                            var original = PrefabUtility.GetCorrespondingObjectFromSource(value);
                            if (original && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string guid, out long id) && identities.TryGetValue(id, out var key)) mapped[key] = value;
                        }
                    }
                    Require(mapped.Count == model.original_keys.Length && instance.GetComponentsInChildren<Transform>(true).Length == model.gameObjects, "Original Model object/component inventory differs: mapped=" + mapped.Count + "/" + model.original_keys.Length + ", transforms=" + instance.GetComponentsInChildren<Transform>(true).Length + "/" + model.gameObjects);
                    var external = new Dictionary<string, GameObject>();
                    foreach (var targetBinding in binding.ExternalFadeTargets)
                    {
                        var node = anchors.nodes.Single(n => n.key == targetBinding.GameObjectKey);
                        external.Add(node.key, nodes[node.transformKey].gameObject);
                    }
                    binding.Prepare(animations, sprites, external, dissolve.Controllers);
                    foreach (var key in model.original_keys)
                    {
                        var record = indexed[key]; var value = mapped[key];
                        if (record.type == "GameObject")
                        {
                            var go = (GameObject)value; Require(go.name == record.fields.m_Name && go.layer == record.fields.m_Layer && go.activeSelf == record.fields.m_IsActive, "Original Model GameObject fields differ: " + key);
                            var actual = go.GetComponents<Component>(); var expected = record.references.Where(r => r.field.StartsWith(".m_Component[", StringComparison.Ordinal)).ToArray();
                            Require(actual.Length >= expected.Length, "Original Model component order is incomplete.");
                            for (int i = 0; i < expected.Length; i++) Require(ReferenceEquals(actual[i], mapped[Identity(expected[i])]), "Original Model component order differs: " + key);
                        }
                        else if (record.type == "Transform")
                        {
                            var tf = (Transform)value; var f = record.fields;
                            Require((tf.localPosition - f.m_LocalPosition).sqrMagnitude < 1e-12f && (tf.localScale - f.m_LocalScale).sqrMagnitude < 1e-12f && Quaternion.Angle(tf.localRotation, f.m_LocalRotation) < .0001f, "Original Model local transform differs: " + key); transforms++;
                        }
                        else components++;
                    }
                    foreach (var sprite in binding.Sprites)
                    {
                        Require((sprite.Renderer ? sprite.Renderer.sprite : sprite.Mask.sprite) == sprites.ForRenderer(sprite.SourceKey), "Original Model sprite PPtr differs."); spriteBindings++;
                    }
                    // Original AttachAgent registration, using the source
                    // ECAttachAgentView configuration rather than inferred names.
                    var parentNode = anchors.nodes.Single(n => n.transformKey == selected.parentTransformKey);
                    var driverRecord = originals.objects.Single(o => o.type == "ECViewDriver" && o.references.Any(r => r.field == ".m_GameObject" && Identity(r) == parentNode.key));
                    var attach = parent.gameObject.AddComponent<AttachAgent>();
                    var config = driverRecord.fields.references.RefIds.Single(r => r.type.@class == "ECAttachAgentView").data.Configs.Values;
                    foreach (var node in anchors.nodes.Where(n => nodes.ContainsKey(n.transformKey) && nodes[n.transformKey] && !string.IsNullOrEmpty(n.parentTransformKey))) nodes[node.transformKey].gameObject.SetActive(node.active);
                    foreach (var node in anchors.nodes.Where(n => string.IsNullOrEmpty(n.parentTransformKey))) nodes[node.transformKey].gameObject.SetActive(node.active);
                    // Original EC view registration runs after AttachAgent.Awake
                    // initializes its serialized map, before normal world ticks.
                    attach.Clear();
                    foreach (var item in config)
                    {
                        var key = driverRecord.key.Split(':')[0] + ":" + item.Bone.m_PathID;
                        Transform tf;
                        if (mapped.TryGetValue(key, out var value)) tf = value as Transform;
                        else tf = nodes[key];
                        Require(tf, "Original AttachAgent target is absent: " + key); attach.AddBoneNode(item.BoneName, tf);
                    }
                    binding.InitializeActive(); yield return null;
                    var skeleton = binding.Skeletons.Single(); var list = instance.GetComponent<SpriteRendererList>();
                    if (list && !list.SkeletonRenderer)
                    {
                        Debug.Log("Imported original fade field: " + EditorJsonUtility.ToJson(prefab.GetComponent<SpriteRendererList>()) + "; instance=" + EditorJsonUtility.ToJson(list) + "; bridge=" + EditorJsonUtility.ToJson(instance.GetComponentInChildren<NativeMap13PropFadeRenderer>(true)));
                    }
                    Require(list && list.SkeletonRenderer && list.SkeletonRenderer.Skeleton != null, "Original application fade target is not connected: list=" + (bool)list + ", target=" + (list ? list.SkeletonRenderer : null) + ", bridge=" + instance.GetComponentInChildren<NativeMap13PropFadeRenderer>(true) + ", skeleton=" + skeleton + ", valid=" + skeleton.IsValid);
                    var rootAnimator = instance.GetComponent<Animator>(); rootAnimator.enabled = false; skeleton.Animator.enabled = false; skeleton.enabled = false;
                    // Execute the untouched native compiled Model state/clip.
                    rootAnimator.Rebind(); rootAnimator.Update(0); rootAnimator.Play("Base Layer.RONGjie", 0, .5f); rootAnimator.Update(0);
                    Require(rootAnimator.GetCurrentAnimatorClipInfo(0).Single().clip == dissolve.Clip, "Original Model controller did not execute native dissolve clip.");
                    rootAnimator.Rebind(); rootAnimator.Update(0);
                    foreach (var animation in skeleton.Data.Source.animations)
                    foreach (float time in new[] { 0f, animation.duration * .37f, animation.duration })
                    {
                        skeleton.View.ApplyPose(animation, time); yield return null;
                        foreach (var follower in instance.GetComponentsInChildren<AttachmentFollower>(true))
                        {
                            var bone = skeleton.Skeleton.FindBone(follower.BoneName);
                            Require(bone != null && (follower.transform.position - skeleton.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0))).sqrMagnitude < 1e-10f, "Original apple LateUpdate anchor differs."); followerSamples++;
                        }
                    }
                    skeleton.View.ApplyPose(skeleton.Data.Source.animations.First(), 0); yield return null;
                    var meshes = skeleton.Parts.Renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh).ToArray();
                    var colors = meshes.Select(m => m.colors).ToArray();
                    list.SetColor(new Color(.7f, .7f, .7f, 1)); list.Alpha = .5f;
                    for (int i = 0; i < meshes.Length; i++)
                    {
                        var current = meshes[i].colors; Require(current.Length == colors[i].Length, "Original application fade changed native geometry.");
                        for (int c = 0; c < current.Length; c++)
                        {
                            var expected = colors[i][c] * new Color(.7f, .7f, .7f, .5f);
                            if (skeleton.Profile.pmaVertexColors) { expected.r *= .5f; expected.g *= .5f; expected.b *= .5f; }
                            Require(Mathf.Abs(current[c].r - expected.r) < .005f && Mathf.Abs(current[c].g - expected.g) < .005f && Mathf.Abs(current[c].b - expected.b) < .005f && Mathf.Abs(current[c].a - expected.a) < .005f, "Original application fade/PMA color differs."); colorSamples++;
                        }
                    }
                    list.SetColor(Color.white); list.Alpha = 1;
                    int events = 0; list.OnFadeOutStart += () => events++; list.OnFadeOutCompleted += () => events++; list.OnFadeInStart += () => events++; list.OnFadeInCompleted += () => events++;
                    list.FadeOut(.08f); for (int frame = 0; events < 2 && frame < 60; frame++) yield return null;
                    Require(list.Alpha == 0 && !skeleton.gameObject.activeSelf && events == 2, "Original fade-out lifecycle differs.");
                    list.FadeIn(.08f); for (int frame = 0; events < 4 && frame < 60; frame++) yield return null;
                    Require(list.Alpha == 1 && skeleton.gameObject.activeSelf && events == 4 && list.IsIn, "Original fade-in lifecycle differs."); fadeEvents += events;
                    var bounds = skeleton.View.LocalBounds; camera.transform.position = skeleton.transform.TransformPoint(bounds.center) + new Vector3(0, 0, -50);
                    camera.orthographicSize = Mathf.Max(bounds.extents.y * skeleton.transform.lossyScale.y, bounds.extents.x * skeleton.transform.lossyScale.x) * 1.3f;
                    yield return null; camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply(); RenderTexture.active = previous;
                    int visible = image.GetPixels32().Count(c => c.a > 0); Require(visible > 100, "Original assembled tree rendered no pixels."); pixels += visible;
                    File.WriteAllBytes(Path.Combine(Output, "native-tree-model-" + modelIndex + ".png"), image.EncodeToPNG());
                    foreach (var node in anchors.nodes.Where(n => string.IsNullOrEmpty(n.parentTransformKey))) if (nodes[node.transformKey]) UnityEngine.Object.Destroy(nodes[node.transformKey].gameObject);
                    yield return null; modelIndex++;
                }
            }
            Require(transforms == 104 && spriteBindings == 26 && fadeEvents == 8 && !error, "Original Model verification coverage/errors differ.");
            var proof = new Proof { source_sha256 = Hash(bytes), transforms = transforms, original_nontransform_components = components, sprite_bindings = spriteBindings, follower_samples = followerSamples, color_samples = colorSamples, fade_events = fadeEvents, rendered_pixels = pixels, editor = Application.unityVersion, backend = SystemInfo.graphicsDeviceType.ToString(), play_mode = Application.isPlaying, coverage = "Two complete original Model subtrees with source components/order/transforms, native sprites and clocks, apple LateUpdate anchors and original application fades/PMA. Source outer/UI transforms are inert diagnostic anchors; full EC, UI, Timeline, map input/save and Player remain pending." };
            File.WriteAllText(Path.Combine(Output, "native-tree-models-execution.json"), JsonUtility.ToJson(proof, true));
            Debug.Log("Map13 original tree Models passed: " + transforms + " transforms, " + spriteBindings + " Sprite bindings, " + followerSamples + " apple poses, " + fadeEvents + " fade events.");
        }
        private static string Identity(Reference reference) => reference.cab.ToLowerInvariant() + ":" + reference.path_id;
        private static string Hash(byte[] bytes) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        private sealed class Runner : MonoBehaviour { }
        [Serializable] private sealed class Source { public Model[] models; public OutputSpec[] inputs, outputs; }
        [Serializable] private sealed class OutputSpec { public string path, sha256; }
        [Serializable] private sealed class Model { public string source, path; public int gameObjects; public string[] original_keys; }
        [Serializable] private sealed class Hierarchy { public Node[] nodes; }
        [Serializable] private sealed class Node { public string key, transformKey, parentTransformKey, name; public int layer; public bool active; public Pose pose; }
        [Serializable] private sealed class Pose { public Vector3 m_LocalPosition, m_LocalScale; public Quaternion m_LocalRotation; }
        [Serializable] private sealed class Originals { public Original[] objects; }
        [Serializable] private sealed class Original { public string key, type; public Fields fields; public Reference[] references; }
        [Serializable] private sealed class Fields { public string m_Name; public int m_Layer; public bool m_IsActive; public Vector3 m_LocalPosition, m_LocalScale; public Quaternion m_LocalRotation; public ManagedReferences references; }
        [Serializable] private sealed class Reference { public string field, cab, path_id; }
        [Serializable] private sealed class ManagedReferences { public RefId[] RefIds; }
        [Serializable] private sealed class RefId { public RefType type; public RefData data; }
        [Serializable] private sealed class RefType { public string @class; }
        [Serializable] private sealed class RefData { public AttachConfigs Configs; }
        [Serializable] private sealed class AttachConfigs { public AttachConfig[] Values; }
        [Serializable] private sealed class AttachConfig { public string BoneName; public Pointer Bone; }
        [Serializable] private sealed class Pointer { public long m_PathID; }
        [Serializable] private sealed class Proof { public string source_sha256, editor, backend, coverage; public int transforms, original_nontransform_components, sprite_bindings, follower_samples, color_samples, fade_events, rendered_pixels; public bool play_mode; }
    }
}
