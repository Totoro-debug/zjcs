using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13Skeletons;

namespace Zjcs.Editor.Map13Skeletons
{
    public static class OfficialMap13PropPartsAcceptance
    {
        [Serializable] private sealed class Recipe { public Separator[] separators; }
        [Serializable] private sealed class Separator { public string renderer, model; public Node[] nodes; public string[] parts, separatorSlots; }
        [Serializable] private sealed class Node
        { public string key, name; public int layer; public bool active; public Pose transform; public Flags mesh; public string[] materials; }
        [Serializable] private sealed class Pose { public Vector3 m_LocalPosition, m_LocalScale; public Quaternion m_LocalRotation; }
        [Serializable] private sealed class Flags
        {
            public bool m_Enabled;
            public int m_CastShadows, m_ReceiveShadows, m_DynamicOccludee, m_StaticShadowCaster, m_MotionVectors,
                m_LightProbeUsage, m_ReflectionProbeUsage, m_RayTracingMode, m_RayTraceProcedural,
                m_RenderingLayerMask, m_RendererPriority, m_LightmapIndex, m_LightmapIndexDynamic,
                m_SortingLayerID, m_SortingLayer, m_SortingOrder;
        }
        [Serializable] private sealed class Receipt { public Prefab[] prefabs; public SpriteSource[] sprite_sources; }
        [Serializable] private sealed class Prefab { public string renderer, path; public int objects, gameObjects; }
        [Serializable] private sealed class SpriteSource
        { public TextureSource texture; public Vector2[] vertices; public int[] triangles; public float pixels_per_unit, width, height; public Vector2 pivot; }
        [Serializable] private sealed class TextureSource { public string decoded_rgba_sha256; public Mip[] mips; }
        [Serializable] private sealed class Mip { public int level; public string rgba_sha256; }
        [Serializable] private sealed class Proof
        {
            public string unity_version, graphics_api, source_sha256, geometry_sha256, driver_sha256, acceptance_sha256;
            public int prefabs, original_game_objects, native_renderer_fields, animation_poses, geometry_fields, visible_updates, hidden_frames, gpu_mips;
            public int separation_switches, property_block_copies, sprite_geometry_fields;
            public string coverage = "Original renderer sub-prefabs, slot segmentation, native flags/materials, original snow sprite/texture, real Update visibility and re-enable. No enclosing entity gameplay, pointer input, save or Windows Player acceptance.";
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        public static IEnumerator Verify()
        {
            string root = OfficialMap13SkeletonTools.Repository;
            string sourcePath = Path.Combine(root, "assets/reference/official-map/map13-native-prop-parts-source-manifest.json");
            var source = JsonUtility.FromJson<Receipt>(File.ReadAllText(sourcePath));
            var recipe = JsonUtility.FromJson<Recipe>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-prop-separators.json"));
            string output = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Validation workspace is not set."), "native-prop-parts");
            Directory.CreateDirectory(output);
            var proof = new Proof { unity_version = Application.unityVersion, graphics_api = SystemInfo.graphicsDeviceType.ToString(), source_sha256 = OfficialMap13SkeletonTools.Hash(File.ReadAllBytes(sourcePath)) };
            var previousCameras = UnityEngine.Object.FindObjectsOfType<Camera>().Where(c => c.enabled).ToArray();
            foreach (var previousCamera in previousCameras) previousCamera.enabled = false;
            var camera = new GameObject("Original prop parts camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .08f, .08f, 0);
            camera.nearClipPlane = .01f; camera.farClipPlane = 1000; camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var target = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create(); camera.targetTexture = target; camera.aspect = 1;
            var readback = new Texture2D(384, 384, TextureFormat.RGBA32, false, false);
            var gallery = new Texture2D(384 * 3, 384 * 2, TextureFormat.RGBA32, false, false);
            var catalog = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativePropAnimators/execution-cases.json");
            var binary = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativePropAnimators/map13-prop-animators.windows.bundle.bytes");
            Require(binary, "Original native Windows animation bundle is absent.");
            using (var library = new NativeMap13AnimatorLibrary(binary, catalog, "65211c5a2dd5a677d26a0f37f88a518c306e7968425048cab263b54e0951bd4f"))
            {
                for (int row = 0; row < source.prefabs.Length; row++)
                {
                    var prefab = source.prefabs[row]; var original = recipe.separators.Single(s => s.renderer == prefab.renderer);
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefab.path.Substring(6)); Require(asset, "Original prop sub-prefab did not import.");
                    var instance = UnityEngine.Object.Instantiate(asset);
                    instance.name = original.nodes[0].name;
                    var driver = instance.GetComponent<NativeMap13PropMecanim>(); Require(driver && driver.SourceKey == original.renderer, "Original prop adapter identity differs.");
                    var nodes = instance.GetComponentsInChildren<Transform>(true);
                    Require(nodes.Length == original.nodes.Length && nodes.Length == prefab.gameObjects, "Original renderer hierarchy differs.");
                    foreach (var node in original.nodes)
                    {
                        var actual = nodes.SingleOrDefault(n => n.name == node.name);
                        if (!actual) Debug.Log("Imported original node snapshots: " + string.Join("\n", nodes.Select(n => EditorJsonUtility.ToJson(n.gameObject))));
                        Require(actual, "Original node name is absent: " + node.name + "; actual=" + string.Join(",", nodes.Select(n => n.name)));
                        Require(actual.gameObject.layer == node.layer && actual.gameObject.activeSelf == node.active &&
                            actual.localPosition == node.transform.m_LocalPosition && actual.localScale == node.transform.m_LocalScale &&
                            actual.localRotation == node.transform.m_LocalRotation, "Original renderer transform/layer/activation differs: " + node.name);
                        proof.original_game_objects++;
                        if (node.materials == null) continue;
                        var renderer = actual.GetComponent<MeshRenderer>(); Require(renderer, "Original mesh renderer is absent.");
                        Require(renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath).SequenceEqual(node.materials.Select(p => p.Substring(6))), "Original parts material binding differs.");
                        var serialized = new SerializedObject(renderer);
                        foreach (var field in typeof(Flags).GetFields(BindingFlags.Instance | BindingFlags.Public))
                        {
                            var property = serialized.FindProperty(field.Name); Require(property != null, "Original renderer field is not serialized: " + field.Name);
                            long expected = field.FieldType == typeof(bool) ? ((bool)field.GetValue(node.mesh) ? 1 : 0) : (int)field.GetValue(node.mesh);
                            long value = property.propertyType == SerializedPropertyType.Boolean ? (property.boolValue ? 1 : 0) : property.longValue;
                            Require(value == expected, "Original renderer native field differs: " + node.name + "/" + field.Name + " expected=" + expected + " actual=" + value); proof.native_renderer_fields++;
                        }
                    }
                    Require(driver.Parts.Renderers.Select(r => r.name).SequenceEqual(new[] { "0", "1", "2", "3" }) && driver.Parts.SeparatorSlots.SequenceEqual(original.separatorSlots), "Original parts order/slot binding differs.");
                    int propertyId = Shader.PropertyToID("Map13OriginalPropertyBlockProbe");
                    var propertyBlock = new MaterialPropertyBlock(); propertyBlock.SetFloat(propertyId, .375f);
                    driver.Parts.SourceRenderer.SetPropertyBlock(propertyBlock);
                    driver.Initialize(library); driver.enabled = false;
                    foreach (var renderer in driver.Parts.Renderers)
                    {
                        renderer.GetPropertyBlock(propertyBlock);
                        Require(propertyBlock.GetFloat(propertyId) == .375f, "Original separator property block was not copied."); proof.property_block_copies++;
                    }
                    using (var reference = new NativeMap13SkeletonView(driver.Data, driver.Profile, null, "Source attachment reference"))
                    {
                        reference.Transform.gameObject.SetActive(false);
                        foreach (var animation in driver.Data.Source.animations)
                        foreach (float time in new[] { 0f, animation.duration * .37f, animation.duration })
                        {
                            driver.View.ApplyPose(animation, time); reference.ApplyPose(animation, time);
                            proof.geometry_fields += CompareSegments(driver, reference, animation, time); proof.animation_poses++;
                        }
                        driver.Parts.enabled = false;
                        var unsplit = driver.Parts.SourceRenderer.GetComponent<MeshFilter>().sharedMesh;
                        Require(driver.Parts.SourceRenderer.enabled && driver.Parts.Renderers.All(r => !r.enabled) &&
                            unsplit.subMeshCount == 4 && unsplit.triangles.Length / 3 == driver.View.VisibleTriangles,
                            "Disabling the original separator did not restore the native unsplit mesh."); proof.separation_switches++;
                        driver.Parts.enabled = true;
                        Require(!driver.Parts.SourceRenderer.enabled && driver.Parts.Renderers.Any(r => r.enabled), "Re-enabling the original separator did not restore its parts."); proof.separation_switches++;
                        foreach (var name in new[] { "A_3_loop", "B_6_loop", "C_10_loop" })
                        {
                            var animation = driver.Data.Source.animations.Single(a => a.name == name); driver.View.ApplyPose(animation, animation.duration * .37f);
                            yield return null;
                            Bounds bounds = driver.Parts.Renderers[0].bounds;
                            foreach (var renderer in driver.Parts.Renderers.Where(r => r.enabled)) bounds.Encapsulate(renderer.bounds);
                            foreach (var sprite in instance.GetComponentsInChildren<SpriteRenderer>()) bounds.Encapsulate(sprite.bounds);
                            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 20);
                            camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.12f;
                            yield return null; yield return null;
                            var previous = RenderTexture.active; RenderTexture.active = target;
                            readback.ReadPixels(new Rect(0, 0, 384, 384), 0, 0); readback.Apply(); RenderTexture.active = previous;
                            Require(readback.GetPixels32().Count(c => c.a > 16) > 100, "Original parts did not render.");
                            int column = Array.IndexOf(new[] { "A_3_loop", "B_6_loop", "C_10_loop" }, name);
                            gallery.SetPixels(column * 384, (1 - row) * 384, 384, 384, readback.GetPixels());
                        }
                        driver.Animator.Play("Base Layer.C_10_loop", 0, .13f); driver.Animator.Update(0);
                        driver.enabled = true;
                        var before = driver.View.BoneMatrices.ToArray();
                        for (int frame = 0; frame < 12; frame++) { yield return null; if (driver.Parts.Renderers.Any(r => r.isVisible)) proof.visible_updates++; }
                        Require(!before.SequenceEqual(driver.View.BoneMatrices), "Original Update/native Animator did not advance geometry.");
                        camera.transform.position += Vector3.right * 10000;
                        yield return null; yield return null;
                        Require(!driver.Parts.Renderers.Any(r => r.isVisible), "Hidden original parts remain visible to a camera.");
                        Require(driver.UpdateWhenInvisible == 3, "Original tree FullUpdate-when-invisible mode differs.");
                        var frozen = driver.View.BoneMatrices.ToArray(); float clock = driver.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                        for (int frame = 0; frame < 8; frame++) { yield return null; proof.hidden_frames++; }
                        Require(!frozen.SequenceEqual(driver.View.BoneMatrices), "Original FullUpdate-when-invisible geometry froze.");
                        Require(driver.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime > clock, "Original AlwaysAnimate clock froze while invisible.");
                        driver.Animator.speed = 0; yield return null; yield return null;
                        frozen = driver.View.BoneMatrices.ToArray(); clock = driver.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                        instance.SetActive(false); yield return null;
                        Require(frozen.SequenceEqual(driver.View.BoneMatrices), "Disabled original prop pose changed.");
                        instance.SetActive(true); yield return null;
                        Require(frozen.SequenceEqual(driver.View.BoneMatrices), "Original clearStateOnDisable=false lost its pose.");
                        Require(driver.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime == clock, "Original keepAnimatorStateOnDisable lost its native clock.");
                        foreach (var renderer in driver.Parts.Renderers)
                        {
                            renderer.GetPropertyBlock(propertyBlock);
                            Require(propertyBlock.GetFloat(propertyId) == 0, "Original OnEnable property-block reset did not reach the parts."); proof.property_block_copies++;
                        }
                    }
                    var spriteRenderer = instance.GetComponentInChildren<SpriteRenderer>();
                    if (spriteRenderer)
                    {
                        var sprite = spriteRenderer.sprite; var sourceSprite = source.sprite_sources.Single();
                        Require(sprite && sprite.name == "Building_tree_snow" && sprite.vertices.SequenceEqual(sourceSprite.vertices) &&
                            sprite.triangles.Select(t => (int)t).SequenceEqual(sourceSprite.triangles) && sprite.pixelsPerUnit == sourceSprite.pixels_per_unit &&
                            sprite.rect.width == sourceSprite.width && sprite.rect.height == sourceSprite.height && sprite.pivot == sourceSprite.pivot,
                            "Original snow sprite vertices/indices/rect/pivot differ.");
                        proof.sprite_geometry_fields += sprite.vertices.Length * 2 + sprite.triangles.Length + 5;
                        var texture = sprite.texture; var expected = sourceSprite.texture;
                        Require(texture.mipmapCount == expected.mips.Length, "Original snow mip inventory differs.");
                        foreach (var mip in expected.mips)
                        {
                            var request = AsyncGPUReadback.Request(texture, mip.level, TextureFormat.RGBA32); request.WaitForCompletion();
                            Require(!request.hasError && OfficialMap13SkeletonTools.Hash(request.GetData<byte>().ToArray()) == mip.rgba_sha256, "Original snow GPU mip differs: " + mip.level); proof.gpu_mips++;
                        }
                    }
                    UnityEngine.Object.Destroy(instance); yield return null; proof.prefabs++;
                }
            }
            gallery.Apply(); File.WriteAllBytes(Path.Combine(output, "original-tree-parts.png"), gallery.EncodeToPNG());
            string Hash(string path) => OfficialMap13SkeletonTools.Hash(File.ReadAllBytes(path));
            proof.geometry_sha256 = Hash("Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13SkeletonView.cs");
            proof.driver_sha256 = Hash("Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13PropMecanim.cs");
            proof.acceptance_sha256 = Hash("Assets/Game/Editor/Map13Skeletons/OfficialMap13PropPartsAcceptance.cs");
            File.WriteAllText(Path.Combine(root, "assets/reference/official-map/map13-native-prop-parts-playmode-source-manifest.json"), JsonUtility.ToJson(proof, true) + "\n", new UTF8Encoding(false));
            UnityEngine.Object.Destroy(camera.gameObject); UnityEngine.Object.Destroy(readback); UnityEngine.Object.Destroy(gallery); target.Release(); UnityEngine.Object.Destroy(target);
            foreach (var previousCamera in previousCameras) if (previousCamera) previousCamera.enabled = true;
            Debug.Log($"Original Map13 prop parts passed: {proof.prefabs} prefabs, {proof.original_game_objects} objects, {proof.native_renderer_fields} native flags, {proof.animation_poses} poses, {proof.geometry_fields} fields, {proof.gpu_mips} GPU mips.");
        }
        private static int CompareSegments(NativeMap13PropMecanim driver, NativeMap13SkeletonView reference, NativeMap13Animation animation, float time)
        {
            var data = driver.Data.Source; var order = Enumerable.Range(0, data.slots.Length).ToArray();
            foreach (var key in animation.drawOrders) if (key.time <= time) order = key.slots;
            var cuts = driver.Parts.SeparatorSlots.Select(name => Array.IndexOf(order, Array.FindIndex(data.slots, s => s.name == name))).ToArray();
            int count = 0;
            var draws = reference.Transform.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).OrderBy(r => r.sortingOrder).ToArray();
            var flip = Matrix4x4.Scale(new Vector3(driver.Profile.initialFlipX ? -1 : 1, driver.Profile.initialFlipY ? -1 : 1, 1));
            for (int segment = 0; segment < driver.Parts.Renderers.Length; segment++)
            {
                var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var colors = new List<Color>(); var triangles = new List<int>();
                foreach (var draw in draws.Where(d => cuts.Count(cut => cut < d.sortingOrder) == segment))
                {
                    var mesh = draw.GetComponent<MeshFilter>().sharedMesh; int offset = vertices.Count;
                    vertices.AddRange(mesh.vertices.Select(v => flip.MultiplyPoint3x4(v))); uv.AddRange(mesh.uv); colors.AddRange(mesh.colors);
                    triangles.AddRange(mesh.triangles.Select(t => t + offset));
                }
                var actual = driver.Parts.Renderers[segment].GetComponent<MeshFilter>().sharedMesh;
                Require(actual && actual.vertices.SequenceEqual(vertices) && actual.uv.SequenceEqual(uv) && actual.colors.SequenceEqual(colors) && actual.triangles.SequenceEqual(triangles),
                    "Original dynamic slot segment differs: " + animation.name + "/" + time + "/" + segment);
                count += vertices.Count * 9 + triangles.Count;
            }
            return count;
        }
    }
}
