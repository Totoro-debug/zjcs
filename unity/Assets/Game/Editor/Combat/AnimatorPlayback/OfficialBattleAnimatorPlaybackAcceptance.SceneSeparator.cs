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
        public static void RunSceneSeparator()
        {
            Run(); SessionState.SetBool(Key + ".sceneSeparator", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 600);
        }

        private static IEnumerator SceneSeparatorSteps()
        {
            Require(DynamicSkeletonRenderSeparator.AddDynamicToSkeletonRenderer(null) == null, "original null separator source");
            var catalog = Resources.Load<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
            foreach (string group in new[] { "100", "200" })
            {
                var root = new GameObject("Actual original scene weapon separator " + group);
                var endpoint = root.AddComponent<SceneSeparatorEndpoint>();
                var backend = endpoint.Backend = new ProfessionNativeSceneMeshSeparator(() => endpoint.Enables++);
                var state = new ProfessionNativeAppearanceSkeleton(catalog.Weapon.Model.Data);
                var entry = catalog.Find("pWeapon_" + group + "_G_007");
                var animationFields = JsonUtility.FromJson<SceneHierarchy>(entry.Hierarchy.text).nodes
                    .SelectMany(n => n.components).Single(c => c.type == "Spine.Unity.SkeletonAnimation").runtime_fields;
                state.SetSkin(state.Data.FindSkin(entry.InitialSkin)); state.SetToSetupPose();
                state.EvaluateUnmixedWeaponAnimation(entry.InitialAnimation, .4f);
                var source = entry.OriginalMaterial;
                var page = catalog.Weapon.FindPage("pWeapon_" + group + "_007");
                var instance = new Material(source); instance.mainTexture = page.Main;
                Require(instance.HasProperty(AppearanceMaterialUtils.sDyeTexId), "original world shader dye map binding");
                instance.SetTexture(AppearanceMaterialUtils.sDyeTexId, page.Dye);
                endpoint.SeparatorSlots.Add(state.DrawOrder[0]);
                endpoint.SeparatorSlots.Add(state.DrawOrder[state.SlotCount - 1]);
                var separator = DynamicSkeletonRenderSeparator.AddDynamicToSkeletonRenderer(endpoint, 0, 0, 5, 100);
                try
                {
                    Require(endpoint.Initializes == 1 && endpoint.Enables == 1 && backend.OptimizeRender, "original initialization and enable order");
                    var original = separator.PartsRenderers.ToArray();
                    Require(original.Length == 3 && original.Select(p => p.MeshRenderer.sortingOrder).SequenceEqual(new[] { 100, 105, 110 }), "original minimum parts and new-only sorting");
                    foreach (var part in original) Require(part.GameObject.activeSelf && part.GameObject.transform.parent == root.transform, "native part ownership");
                    separator.ReInitPartRenderers(0, 0, 7, 200, false);
                    Require(separator.PartsRenderers.Count == 0 && original.All(p => !p.GameObject.activeSelf), "reverse shrink disables every pooled part");
                    int invisible = endpoint.Invisible;
                    separator.ResetVisibility(); Require(endpoint.Invisible == invisible + 1, "empty active list is invisible");
                    separator.ReInitPartRenderers(0, 3, 7, 200, false);
                    Require(separator.PartsRenderers.SequenceEqual(original.Reverse()) && separator.PartsRenderers.Select(p => p.MeshRenderer.sortingOrder).SequenceEqual(new[] { 110, 105, 100 }), "original reverse enqueue and FIFO reuse retain sorting");
                    separator.ReInitPartRenderers(0, 4, 7, 200, false);
                    var fourth = separator.PartsRenderers[3];
                    Require(fourth.MeshRenderer.sortingOrder == 221 && fourth.GameObject.name == "3", "only newly allocated parts receive requested order and name");

                    int callbacks = 0;
                    separator.OnGetSharedMaterial += materials =>
                    {
                        callbacks++;
                        for (int i = 0; i < materials.Length; i++)
                        { Require(materials[i] == source, "original atlas page reaches array callback"); materials[i] = instance; }
                    };
                    using (var batches = new ProfessionNativeMeshBatches(state.SlotCount))
                    {
                        batches.Build(state.Pose, state.CreateDrawSlots(), animationFields.useClipping, animationFields.pmaVertexColors);
                        Require(batches.Batches.Count > 0, "actual original weapon geometry");
                        backend.Render(batches.Batches, Array.Empty<int>(), _ => source);
                        var baseline = CaptureSeparator(separator, group + "-whole");
                        var cuts = new[] { 0, state.SlotCount - 1 }.Distinct().OrderBy(i => i).ToArray();
                        var slots = cuts.Select(i => state.SlotPose.DrawSlot(i)).ToArray();
                        batches.Build(state.Pose, state.CreateDrawSlots(slots), animationFields.useClipping, animationFields.pmaVertexColors);
                        backend.Render(batches.Batches, cuts, _ => source);
                        var split = CaptureSeparator(separator, group + "-split");
                        Require(baseline.SequenceEqual(split), "whole and split actual MeshRenderer GPU pixels match exactly");
                        Require(callbacks > 1 && separator.PartsRenderers.All(p => p.MeshRenderer.sharedMaterials.All(m => m == instance)), "all actual native parts share the same callback material instance");
                        var sourceVertices = batches.Batches.Sum(b => b.Mesh.vertexCount);
                        Require(separator.PartsRenderers.Sum(p => ((ProfessionNativeSceneMeshPart)p).Mesh.vertexCount) == sourceVertices, "native split preserves source geometry count");

                        var cameraGo = new GameObject("Actual native mesh visibility camera");
                        var camera = cameraGo.AddComponent<Camera>();
                        var bounds = BoundsOfSeparator(separator);
                        camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.2f;
                        camera.transform.position = bounds.center + Vector3.back * 10; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
                        camera.enabled = false;
                        var visibilityTarget = RenderTexture.GetTemporary(640, 640, 24);
                        try
                        {
                            camera.aspect = 1;
                            if (GraphicsSettings.currentRenderPipeline)
                                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = visibilityTarget });
                            else { camera.targetTexture = visibilityTarget; camera.Render(); }
                            int visible = endpoint.Visible;
                            bool visibleNow = separator.PartsRenderers.Any(p => p.MeshRenderer.isVisible);
                            separator.ResetVisibility();
                            Require(visibleNow && endpoint.Visible == visible + 1, "actual camera visibility reaches original visible branch");
                        }
                        finally { camera.targetTexture = null; RenderTexture.ReleaseTemporary(visibilityTarget); Object.Destroy(cameraGo); }
                    }
                    separator.ClearSharedMaterials();
                    Require(separator.PartsRenderers.All(p => p.MeshRenderer.sharedMaterials.Length == 0), "actual active part materials cleared");
                    separator.ReInitPartRenderers(0, 0, 0, 0, false);
                    separator.ClearAndDestroyPartsRenderers();
                    separator.ReInitPartRenderers(0, 2, 0, 0, false);
                    var destroyed = separator.PartsRenderers.Select(p => p.GameObject).ToArray();
                    var destroyedMeshes = separator.PartsRenderers.Select(p => ((ProfessionNativeSceneMeshPart)p).Mesh).ToArray();
                    separator.ClearAndDestroyPartsRenderers();
                    Require(separator.PartsRenderers.Count == 0, "original active-only destroy removes active entries immediately");
                    yield return null; yield return null;
                    Require(destroyed.All(go => !go), "active parts are actually destroyed");
                    Require(destroyedMeshes.All(mesh => !mesh), "destroyed active parts release their owned native meshes");
                    separator.ReInitPartRenderers(0, 2, 0, 0, false);
                    Require(separator.PartsRenderers.Count == 2 && separator.PartsRenderers.All(p => p.GameObject), "original active-only destroy preserves the inactive pool");
                    var remainingMeshes = root.GetComponentsInChildren<ProfessionNativeSceneMeshPart>(true).Select(p => p.Mesh).ToArray();
                    backend.Dispose(); backend.Dispose();
                    yield return null; yield return null;
                    Require(root.GetComponentsInChildren<ProfessionNativeSceneMeshPart>(true).Length == 0, "backend disposal releases pooled and active native mesh owners");
                    Require(remainingMeshes.All(mesh => !mesh), "native backend disposal releases active and pooled meshes");
                    Debug.Log("[Original scene separator] " + group + ": actual original geometry, whole/split GPU identity, shared material array, original reverse/FIFO pool and retained sorting, real camera visibility, active-only destroy and native cleanup.");
                }
                finally { backend.Dispose(); Object.Destroy(root); Object.Destroy(instance); }
                yield return null;
            }
        }

        public sealed class SceneSeparatorEndpoint : MonoBehaviour, IProfessionSceneSeparatorRenderer
        {
            internal ProfessionNativeSceneMeshSeparator Backend;
            internal int Initializes, Enables, Visible, Invisible;
            public GameObject GameObject => gameObject;
            public List<IProfessionSkinClipSlot> SeparatorSlots { get; } = new List<IProfessionSkinClipSlot>();
            public void Initialize(bool overwrite, bool quiet) { Require(!overwrite && !quiet, "original separator initialize flags"); Initializes++; }
            public void OnBecameVisible() { Visible++; }
            public void OnBecameInvisible() { Invisible++; }
            public IProfessionSceneSeparatorBackend CreateSeparatorBackend() => Backend;
        }

        private static Bounds BoundsOfSeparator(DynamicSkeletonRenderSeparator separator)
        {
            var meshes = separator.PartsRenderers.Select(p => ((ProfessionNativeSceneMeshPart)p).Mesh).Where(m => m.vertexCount > 0).ToArray();
            Require(meshes.Length > 0, "native separator has nonempty meshes");
            var bounds = meshes[0].bounds;
            foreach (var mesh in meshes.Skip(1)) bounds.Encapsulate(mesh.bounds);
            return bounds;
        }

        private static byte[] CaptureSeparator(DynamicSkeletonRenderSeparator separator, string name)
        {
            var bounds = BoundsOfSeparator(separator);
            float extent = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.15f;
            var fit = Matrix4x4.Scale(new Vector3(1 / extent, 1 / extent, 1)) * Matrix4x4.Translate(-bounds.center);
            var target = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var image = new Texture2D(640, 640, TextureFormat.RGBA32, false, true);
            try
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(fit, Matrix4x4.identity);
                    foreach (var part in separator.PartsRenderers)
                        for (int i = 0; i < part.MeshRenderer.sharedMaterials.Length; i++)
                            commands.DrawRenderer(part.MeshRenderer, part.MeshRenderer.sharedMaterials[i], i, 0);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
                Require(image.GetPixels32().Count(p => p.a > 8) > 100, "actual original scene weapon separator raster");
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "scene-separator-" + name + ".png"), image.EncodeToPNG());
                return image.GetRawTextureData<byte>().ToArray();
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.Destroy(image); }
        }
    }
}
