using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSceneRenderer()
        {
            Run(); SessionState.SetBool(Key + ".sceneRenderer", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 600);
        }

        private static IEnumerator SceneRendererSteps()
        {
            var catalog = Resources.Load<ProfessionNativeSceneAppearanceResources>("Combat/SceneCharacters/appearance-resources");
            var previous = ProfessionAppearanceTextureBindings.Current;
            using (var textures = new OfflineProfessionSceneTextureResources(catalog))
            {
                ProfessionAppearanceTextureBindings.Current = textures;
                catalog.Loader.TryInit();
                // The common battle host groups every character under its body.
                // Keep that real scene contract when testing equal-order parts.
                var host = new GameObject("Current scene character battle body");
                host.AddComponent<SortingGroup>().sortingOrder = 2000;
                var root = new GameObject("Current native scene renderer diagnostic");
                root.transform.SetParent(host.transform, false);
                var native = root.AddComponent<ProfessionNativeSceneRenderer>();
                IProfessionAppearanceSceneRenderer renderer = native;
                IProfessionAppearancePartRenderer parts = native;
                native.Configure(catalog);
                var clones = new Dictionary<Material, Material>();
                var loads = new List<int>();
                var ownedParts = new List<GameObject>();
                int mainCallbacks = 0, partCallbacks = 0, demands = 0;
                Action<Material[]> replace = array =>
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        Material source = array[i];
                        if (!clones.TryGetValue(source, out var material))
                        {
                            material = new Material(source);
                            int index = catalog.Loader.PlaceholderTextureIndexDict[material.mainTexture];
                            Material unused = null;
                            catalog.Loader.RequestLoadMaterialTextures(material, ref unused);
                            catalog.Loader.AddLoadedTextureRef(index); loads.Add(index);
                            Require(material.mainTexture && !material.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal), "current actual Low texture demand");
                            clones.Add(source, material);
                        }
                        array[i] = material;
                    }
                };
                renderer.OnGetUpdatedSharedMaterials += materials => { mainCallbacks++; replace(materials); };
                renderer.OnCustomDemandLoading += _ => demands++;
                try
                {
                    // Exercise the same original renderer/part interfaces used by appearance callers.
                    foreach (var model in catalog.Models)
                    foreach (string group in new[] { "100", "200" })
                    {
                        renderer.skeletonDataAsset = model.Asset;
                        renderer.Initialize(true, false);
                        Require(ReferenceEquals(renderer.Skeleton, parts.Skeleton) && renderer.SeparatorSlotCount == 0, "one skeleton behind both original drawing interfaces");
                        Require(ownedParts.All(part => !part || !part.activeSelf), "reinitialization immediately retires old scene parts");
                        var state = native.State;
                        bool unboundDepth = false;
                        try { _ = ((IProfessionFollowerSlot)state.DrawOrder[0]).zOrder; } catch (NotSupportedException) { unboundDepth = true; }
                        Require(unboundDepth, "unbound original follower slot depth cannot silently become a zero-depth substitute");
                        renderer.Initialize(false, false);
                        Require(ReferenceEquals(state, native.State), "same-asset initialization retains live pose identity");
                        var skin = state.CreateSkin("Current original scene body render diagnostic");
                        foreach (string name in new[] { "default", "body_" + group + "_" + (model.Sex == "male" ? "M" : "F") + "_01", model.Sex == "male" ? "hair_G_01" : "hair_F_01" })
                            skin.AddSkin(state.Data.FindSkin(name));
                        state.SetSkin(skin); state.SetToSetupPose();
                        // Explicit diagnostic input: this is not the production motion driver.
                        ProfessionNativeConstraintPose.EvaluateBeforePhysics(state, new ProfessionNativeAnimationChannels(state.Source, group + "/idle_01"), .4f, false);
                        root.transform.localPosition = new Vector3(2, -3, 0);
                        root.transform.localRotation = Quaternion.Euler(0, 0, 17);
                        root.transform.localScale = new Vector3(-.3f, .27f, 1);
                        var transformBefore = root.transform.localToWorldMatrix;
                        renderer.LateUpdateMesh();
                        var main = root.GetComponent<ProfessionNativeSceneMeshPart>();
                        Require(main.Mesh.vertexCount > 0 && main.MeshRenderer.enabled
                            && main.MeshRenderer.sortingLayerID == 1182434123 && main.MeshRenderer.sortingOrder == 0
                            && main.MeshRenderer.shadowCastingMode == ShadowCastingMode.On && main.MeshRenderer.receiveShadows
                            && main.MeshRenderer.lightProbeUsage == LightProbeUsage.BlendProbes, "actual original scene MeshRenderer fields and geometry");
                        var expectedPages = renderer.SkinMaterials(skin).ToArray();
                        Require(expectedPages.Length > 0 && expectedPages.All(m => Enumerable.Range(0, model.Atlas.MaterialCount).Any(i => model.Atlas.MaterialAt(i) == m)), "skin material enumeration belongs to the current atlas");
                        Bounds viewBounds = main.Mesh.bounds;
                        var baseline = CaptureSceneRenderer(new[] { main }, model.Sex + "-" + group + "-whole", viewBounds);
                        var worldBounds = main.MeshRenderer.bounds;
                        var cameraBaseline = CaptureSceneRendererCamera(worldBounds, model.Sex + "-" + group + "-whole");
                        var triangles = SceneRendererTriangles(new[] { main });
                        int vertices = main.Mesh.vertexCount;
                        parts.SeparatorSlots.Add(state.DrawOrder[state.SlotCount / 2]);
                        var split = (DynamicSkeletonRenderSeparator)parts.AddSeparator(main.MeshRenderer.sortingLayerID, 0, 0, 0);
                        ProfessionPartSharedMaterial partCallback = materials => { partCallbacks++; replace(materials); };
                        split.OnGetSharedMaterial += partCallback;
                        parts.LateUpdateMesh();
                        var active = split.PartsRenderers.Cast<ProfessionNativeSceneMeshPart>().ToArray();
                        ownedParts.AddRange(active.Select(p => p.gameObject));
                        Require(!main.MeshRenderer.enabled && active.Length == 2 && active.Sum(p => p.Mesh.vertexCount) == vertices, "split renderer suppresses whole draw and preserves vertex count");
                        Require(triangles.SequenceEqual(SceneRendererTriangles(active)), "whole and split source triangles retain exact vertex positions, UVs, colors and draw order");
                        Require(baseline.SequenceEqual(CaptureSceneRenderer(active, model.Sex + "-" + group + "-split", viewBounds)), "current character whole and split GPU pixels match exactly");
                        Require(cameraBaseline.SequenceEqual(CaptureSceneRendererCamera(worldBounds, model.Sex + "-" + group + "-split")), "actual portrait camera preserves split renderer sorting and caller transform");
                        parts.SeparatorSlots.Clear();
                        split.ReInitPartRenderers(main.MeshRenderer.sortingLayerID, 0, 0, 0);
                        parts.LateUpdateMesh();
                        parts.SeparatorSlots.Add(state.DrawOrder[state.SlotCount / 4]);
                        parts.SeparatorSlots.Add(state.DrawOrder[state.SlotCount / 2]);
                        split.ReInitPartRenderers(main.MeshRenderer.sortingLayerID, 0, 0, 0);
                        parts.LateUpdateMesh();
                        active = split.PartsRenderers.Cast<ProfessionNativeSceneMeshPart>().ToArray();
                        ownedParts.AddRange(active.Select(p => p.gameObject));
                        Require(triangles.SequenceEqual(SceneRendererTriangles(active))
                            && cameraBaseline.SequenceEqual(CaptureSceneRendererCamera(worldBounds, model.Sex + "-" + group + "-pooled")),
                            "shrinking and growing pooled fragments preserves source triangles and actual portrait pixels");
                        Require(active.All(p => p.MeshRenderer.sharedMaterials.All(m => clones.ContainsValue(m))), "whole and split materials reuse actual current instances");
                        var visible = active.SelectMany(p => p.MeshRenderer.sharedMaterials).First();
                        var original = clones.Single(pair => pair.Value == visible).Key;
                        var temporary = new Material(visible);
                        var replacement = (IProfessionAppearanceMaterialReplacement)native;
                        try
                        {
                            replacement.AddRuntimeReplacementMaterial(visible, temporary);
                            Require(active.Any(p => p.MeshRenderer.sharedMaterials.Contains(temporary)), "runtime replacement reaches existing split meshes");
                            replacement.ResetRuntimeReplacementMaterials();
                            Require(active.All(p => !p.MeshRenderer.sharedMaterials.Contains(temporary)), "existing split material replacement restores");
                            split.OnGetSharedMaterial -= partCallback;
                            renderer.CustomMaterialOverride[original] = visible;
                            parts.LateUpdateMesh();
                            Require(active.Any(p => p.MeshRenderer.sharedMaterials.Contains(visible)), "original custom material override is applied before array callbacks");
                            renderer.CustomMaterialOverride.Clear();
                            split.OnGetSharedMaterial += partCallback;
                            split.ClearSharedMaterials();
                            Require(active.All(p => p.MeshRenderer.sharedMaterials.Length == 0), "original part release clears active arrays");
                            parts.LateUpdateMesh();
                        }
                        finally { Object.Destroy(temporary); }
                        Require(root.transform.localToWorldMatrix == transformBefore, "render, split and material updates preserve caller scale and blood-bar anchor transform");
                        bool rejected = false;
                        parts.SeparatorSlots.Add(state.DrawOrder[state.SlotCount / 2]);
                        try { parts.LateUpdateMesh(); } catch (ArgumentException) { rejected = true; }
                        Require(rejected, "duplicate separator identities cannot silently change segment count");
                        parts.SeparatorSlots.RemoveAt(parts.SeparatorSlots.Count - 1);
                        main.MeshRenderer.sortingOrder = 177;
                        renderer.skeletonDataAsset = catalog.Models.Single(m => m.Sex != model.Sex).Asset;
                        renderer.Initialize(false, false);
                        Require(native.State.Source.Sex != model.Sex && parts.Separator == null && parts.SeparatorSlots.Count == 0
                            && active.All(p => !p.gameObject.activeSelf) && main.MeshRenderer.sortingOrder == 177,
                            "cross-sex asset switch retires old segments and identities while retaining caller renderer sorting");
                        main.MeshRenderer.sortingOrder = 0;
                        native.Release(); native.Release();
                        Require(native.State == null && main.Mesh.vertexCount == 0 && main.MeshRenderer.sharedMaterials.Length == 0, "repeated release clears old geometry and materials");
                        yield return null;
                        Require(ownedParts.All(part => !part), "reinitialization and release destroy every owned scene part");
                    }
                    Require(mainCallbacks == 4 && partCallbacks >= 4 && demands >= 12, "actual whole, split and demand callbacks exercised through original interfaces");
                }
                finally
                {
                    Object.Destroy(host);
                    foreach (var material in clones.Values) Object.Destroy(material);
                    foreach (int index in loads) catalog.Loader.RemoveLoadedTextureRef(index);
                    catalog.Loader.TryUnloadUnusedTextures(true);
                    ProfessionAppearanceTextureBindings.Current = previous;
                }
                yield return null;
                Require(textures.LoadedTextureCount == 0 && !root, "scene teardown releases actual Low and dye requests and renderer objects");
            }
        }

        private static List<(Vector3, Vector2, Color32)> SceneRendererTriangles(ProfessionNativeSceneMeshPart[] parts)
        {
            var result = new List<(Vector3, Vector2, Color32)>();
            foreach (var part in parts)
            {
                var mesh = part.Mesh;
                var vertices = mesh.vertices; var uvs = mesh.uv; var colors = mesh.colors32;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    foreach (int index in mesh.GetTriangles(sub)) result.Add((vertices[index], uvs[index], colors[index]));
            }
            return result;
        }

        private static byte[] CaptureSceneRendererCamera(Bounds bounds, string name)
        {
            var go = new GameObject("Current scene renderer actual portrait camera");
            var camera = go.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.aspect = 675f / 1463;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.15f;
            camera.transform.position = bounds.center + Vector3.back * 10;
            camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            var target = RenderTexture.GetTemporary(675, 1463, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var image = new Texture2D(675, 1463, TextureFormat.RGBA32, false, true);
            try
            {
                if (GraphicsSettings.currentRenderPipeline)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else { camera.targetTexture = target; camera.Render(); }
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 675, 1463), 0, 0); image.Apply();
                Require(image.GetPixels32().Count(p => p.a > 8) > 100, "actual portrait camera sees native scene geometry");
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "scene-camera-" + name + ".png"), image.EncodeToPNG());
                return image.GetRawTextureData<byte>().ToArray();
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.Destroy(image); Object.Destroy(go); }
        }

        private static byte[] CaptureSceneRenderer(ProfessionNativeSceneMeshPart[] parts, string name, Bounds bounds)
        {
            var nonempty = parts.Where(p => p.Mesh.vertexCount > 0).ToArray();
            Require(nonempty.Length > 0, "current native scene has visible geometry");
            float extent = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.15f;
            var inverse = parts[0].transform.parent ? parts[0].transform.parent.worldToLocalMatrix : Matrix4x4.identity;
            // Both paths use the root object's coordinates; draw transforms stay untouched.
            if (parts.Length == 1 && parts[0].GetComponent<ProfessionNativeSceneRenderer>()) inverse = parts[0].transform.worldToLocalMatrix;
            var fit = Matrix4x4.Scale(new Vector3(1 / extent, 1 / extent, 1)) * Matrix4x4.Translate(-bounds.center) * inverse;
            var target = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var image = new Texture2D(640, 640, TextureFormat.RGBA32, false, true);
            try
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(fit, Matrix4x4.identity);
                    foreach (var part in parts)
                        for (int i = 0; i < part.MeshRenderer.sharedMaterials.Length; i++)
                            commands.DrawRenderer(part.MeshRenderer, part.MeshRenderer.sharedMaterials[i], i, 0);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
                Require(image.GetPixels32().Count(p => p.a > 8) > 100, "actual current scene body GPU raster");
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "scene-renderer-" + name + ".png"), image.EncodeToPNG());
                return image.GetRawTextureData<byte>().ToArray();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.Destroy(image); }
        }
    }
}
