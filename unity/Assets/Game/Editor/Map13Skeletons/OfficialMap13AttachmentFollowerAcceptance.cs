using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13Skeletons;
using Zjcs.Official.World.Map13Attachment;

namespace Zjcs.Editor.Map13Skeletons
{
    public static class OfficialMap13AttachmentFollowerAcceptance
    {
        [Serializable] private sealed class Recipe { public Binding[] followers; public Node[] nodes; }
        [Serializable] private sealed class Binding { public string key, gameObject, renderer; public Fields fields; }
        [Serializable] private sealed class Fields
        {
            public int m_Enabled, FollowZSpacing, ZFront, FollowLocalScale, FollowRotation, FollowSkeletonTransformFlipX, InitializeOnAwake;
            public string BoneName, SlotName, AttachmentName;
        }
        [Serializable] private sealed class Node { public string key, transformKey, parentTransformKey, name; public int layer; public bool active; public Pose pose; }
        [Serializable] private sealed class Pose { public Vector3 m_LocalPosition, m_LocalScale; public Quaternion m_LocalRotation; }
        [Serializable] private sealed class PartsRecipe { public Parts[] separators; }
        [Serializable] private sealed class Parts { public string renderer; public PartNode[] nodes; }
        [Serializable] private sealed class PartNode { public string key, name; }
        [Serializable] private sealed class PartsReceipt { public Prefab[] prefabs; }
        [Serializable] private sealed class Prefab { public string renderer, path; }
        [Serializable] private sealed class Proof
        {
            public string unity_version, graphics_api, source_sha256, follower_sha256, pose_sha256, geometry_sha256, driver_sha256, acceptance_sha256;
            public int source_followers, bound_followers, original_transforms, animation_poses, follower_samples, real_late_updates, slot_checks;
            public string coverage = "Original application AttachmentFollower on 20 explicitly linked tree bone anchors; all original ancestor transforms and flags, 120 native poses, actual LateUpdate and enable/disable. Six dynamically assigned shadow targets, apple sprites, Model Animator/view lifecycle, NPCs, input, saves and Windows Player are outside this component check.";
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        public static IEnumerator Verify()
        {
            string root = OfficialMap13SkeletonTools.Repository;
            string source = Path.Combine(root, "assets/reference/official-map/map13-native-attachment-follower-source-manifest.json");
            var recipe = JsonUtility.FromJson<Recipe>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-attachment-followers.json"));
            var parts = JsonUtility.FromJson<PartsRecipe>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-prop-separators.json"));
            var prefabs = JsonUtility.FromJson<PartsReceipt>(File.ReadAllText(Path.Combine(root, "assets/reference/official-map/map13-native-prop-parts-source-manifest.json")));
            var proof = new Proof { unity_version = Application.unityVersion, graphics_api = SystemInfo.graphicsDeviceType.ToString(), source_sha256 = Hash(source), source_followers = recipe.followers.Length };
            Require(recipe.followers.Length == 26 && recipe.followers.Count(f => !string.IsNullOrEmpty(f.renderer)) == 20, "Original attachment source inventory differs.");
            var transforms = new Dictionary<string, Transform>(); var sourceTransforms = new Dictionary<string, Transform>();
            var drivers = new Dictionary<string, NativeMap13PropMecanim>(); var anchors = new Dictionary<string, AttachmentFollower>();
            var roots = new List<GameObject>();
            var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativePropAnimators/execution-cases.json");
            var binary = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativePropAnimators/map13-prop-animators.windows.bundle.bytes");
            using (var library = new NativeMap13AnimatorLibrary(binary, catalog, "65211c5a2dd5a677d26a0f37f88a518c306e7968425048cab263b54e0951bd4f"))
            {
                foreach (var prefab in prefabs.prefabs)
                {
                    var original = parts.separators.Single(p => p.renderer == prefab.renderer);
                    var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab.path.Substring(6)));
                    instance.SetActive(false); instance.name = original.nodes[0].name;
                    var driver = instance.GetComponent<NativeMap13PropMecanim>(); drivers.Add(prefab.renderer, driver);
                    foreach (var node in original.nodes)
                        transforms.Add(node.key, instance.GetComponentsInChildren<Transform>(true).Single(t => t.name == node.name));
                }
                foreach (var node in recipe.nodes)
                {
                    if (!transforms.ContainsKey(node.key))
                    { var go = new GameObject(node.name); go.SetActive(false); transforms.Add(node.key, go.transform); }
                    sourceTransforms.Add(node.transformKey, transforms[node.key]);
                }
                foreach (var node in recipe.nodes)
                {
                    var tf = transforms[node.key]; tf.gameObject.layer = node.layer;
                    tf.SetParent(string.IsNullOrEmpty(node.parentTransformKey) ? null : sourceTransforms[node.parentTransformKey], false);
                    tf.SetLocalPositionAndRotation(node.pose.m_LocalPosition, node.pose.m_LocalRotation); tf.localScale = node.pose.m_LocalScale;
                    if (string.IsNullOrEmpty(node.parentTransformKey)) roots.Add(tf.gameObject);
                    proof.original_transforms++;
                }
                foreach (var binding in recipe.followers.Where(f => !string.IsNullOrEmpty(f.renderer)))
                {
                    var f = transforms[binding.gameObject].gameObject.AddComponent<AttachmentFollower>(); var fields = binding.fields;
                    f.enabled = fields.m_Enabled != 0; f.SkeletonRenderer = drivers[binding.renderer];
                    f.BoneName = fields.BoneName; f.SlotName = fields.SlotName; f.AttachmentName = fields.AttachmentName; f.Gos = Array.Empty<GameObject>();
                    f.FollowZSpacing = fields.FollowZSpacing != 0; f.ZFront = fields.ZFront != 0; f.FollowLocalScale = fields.FollowLocalScale != 0;
                    f.FollowRotation = fields.FollowRotation != 0; f.FollowSkeletonTransformFlipX = fields.FollowSkeletonTransformFlipX != 0; f.InitializeOnAwake = fields.InitializeOnAwake != 0;
                    Require(!f.SkeletonGraphic && f.Gos.Length == 0 && f.SlotName == "" && f.AttachmentName == "", "Unexpected original linked tree follower boundary.");
                    anchors.Add(binding.key, f); proof.bound_followers++;
                }
                // Activate children before roots so original Awake/Update/LateUpdate run normally.
                foreach (var node in recipe.nodes.Where(n => !string.IsNullOrEmpty(n.parentTransformKey))) transforms[node.key].gameObject.SetActive(node.active);
                foreach (var node in recipe.nodes.Where(n => string.IsNullOrEmpty(n.parentTransformKey))) transforms[node.key].gameObject.SetActive(node.active);
                foreach (var driver in drivers.Values) { driver.Initialize(library); driver.enabled = false; driver.Animator.enabled = false; }
                yield return null;
                foreach (var pair in drivers)
                {
                    var driver = pair.Value;
                    foreach (var animation in driver.Data.Source.animations)
                    foreach (float time in new[] { 0f, animation.duration * .37f, animation.duration })
                    {
                        driver.View.ApplyPose(animation, time);
                        yield return null; proof.animation_poses++;
                        foreach (var binding in recipe.followers.Where(f => f.renderer == pair.Key))
                        {
                            var follower = anchors[binding.key]; int bone = Array.FindIndex(driver.Data.Source.bones, b => b.name == binding.fields.BoneName);
                            Require(bone >= 0, "Original tree bone is absent.");
                            var matrix = driver.View.BoneMatrices[bone];
                            Vector3 expected = driver.transform.TransformPoint(new Vector3(matrix.m03 * (driver.Profile.initialFlipX ? -1 : 1), matrix.m13 * (driver.Profile.initialFlipY ? -1 : 1), 0));
                            Require((follower.transform.position - expected).sqrMagnitude < 1e-10f, "Actual original LateUpdate bone anchor differs: " + binding.key + "/" + animation.name + "/" + time);
                            var data = driver.Data.Source.bones[bone]; Vector2 expectedScale = new Vector2(data.scaleX, data.scaleY);
                            foreach (var track in animation.tracks.Where(t => t.kind == 1 && t.index == bone && t.keys.Length > 0 && time >= t.keys[0].time))
                                expectedScale[track.axis] = (track.axis == 0 ? data.scaleX : data.scaleY) * track.Curve.Evaluate(time);
                            Require(Mathf.Abs(follower.transform.localScale.x - expectedScale.x) < 1e-6f && Mathf.Abs(follower.transform.localScale.y - expectedScale.y) < 1e-6f, "Original application local bone scale differs.");
                            proof.follower_samples++; proof.real_late_updates++;
                        }
                        var order = driver.Skeleton.DrawOrder.Items;
                        for (int i = 0; i < order.Length; i++)
                        {
                            int index = driver.View.SlotDrawOrder[i]; var slot = driver.Skeleton.FindSlot(driver.Data.Source.slots[index].name);
                            Require(ReferenceEquals(slot, order[i]), "Application draw-order identity differs.");
                            int attachment = driver.View.SlotAttachments[index];
                            Require(ReferenceEquals(slot.Attachment, attachment < 0 ? null : driver.Data.Source.attachments[attachment]), "Application live attachment identity differs."); proof.slot_checks += 2;
                        }
                    }
                }
                foreach (var go in roots) go.SetActive(false);
                yield return null;
                foreach (var go in roots) go.SetActive(true);
                yield return null;
                foreach (var binding in recipe.followers.Where(f => !string.IsNullOrEmpty(f.renderer)))
                {
                    var driver = drivers[binding.renderer]; var f = anchors[binding.key]; var b = driver.Skeleton.FindBone(f.BoneName);
                    Require((f.transform.position - driver.transform.TransformPoint(new Vector3(b.WorldX, b.WorldY, 0))).sqrMagnitude < 1e-10f, "Re-enabled original follower lost its live target."); proof.real_late_updates++;
                }
                var camera = new GameObject("Original attachment native camera", typeof(Camera)).GetComponent<Camera>();
                camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .08f, .08f, 0); camera.nearClipPlane = .01f; camera.farClipPlane = 1000; camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                var rt = new RenderTexture(512, 512, 24); rt.Create(); camera.targetTexture = rt; camera.aspect = 1;
                var driverView = drivers.Values.First(); var bounds = driverView.View.LocalBounds;
                camera.transform.position = driverView.transform.TransformPoint(bounds.center) + new Vector3(0,0,-50); camera.orthographicSize = Mathf.Max(bounds.extents.y * driverView.transform.lossyScale.y, bounds.extents.x * driverView.transform.lossyScale.x) * 1.2f;
                yield return null; camera.Render();
                RenderTexture previous = RenderTexture.active; RenderTexture.active = rt; var image = new Texture2D(512,512,TextureFormat.RGBA32,false); image.ReadPixels(new Rect(0,0,512,512),0,0); image.Apply(); RenderTexture.active=previous;
                string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Validation directory is unset.");
                File.WriteAllBytes(Path.Combine(output,"native-attachment-runtime.png"),image.EncodeToPNG());
                UnityEngine.Object.Destroy(image); camera.targetTexture=null; rt.Release(); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(camera.gameObject);
                foreach (var go in roots) UnityEngine.Object.Destroy(go); yield return null;
            }
            proof.follower_sha256=Hash(Path.Combine(root,"unity/Assets/Game/Runtime/Official/World/Map13Attachment/AttachmentFollower.cs"));
            proof.pose_sha256=Hash(Path.Combine(root,"unity/Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13ApplicationPose.cs"));
            proof.geometry_sha256=Hash(Path.Combine(root,"unity/Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13SkeletonView.cs"));
            proof.driver_sha256=Hash(Path.Combine(root,"unity/Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13PropMecanim.cs"));
            proof.acceptance_sha256=Hash(Path.Combine(root,"unity/Assets/Game/Editor/Map13Skeletons/OfficialMap13AttachmentFollowerAcceptance.cs"));
            File.WriteAllText(Path.Combine(root,"assets/reference/official-map/map13-native-attachment-follower-playmode-source-manifest.json"),JsonUtility.ToJson(proof,true)+"\n",new UTF8Encoding(false));
            Debug.Log($"Map13 original AttachmentFollower passed: {proof.bound_followers} anchors, {proof.original_transforms} original transforms, {proof.animation_poses} poses, {proof.follower_samples} live samples, {proof.real_late_updates} LateUpdates, {proof.slot_checks} slot identity checks.");
        }
        private static string Hash(string path) => OfficialMap13SkeletonTools.Hash(File.ReadAllBytes(path));
    }
}
