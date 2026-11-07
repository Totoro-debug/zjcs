using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.BattleSpinePlayables;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator ExerciseDemonResultAssets(string output)
        {
            Combat.BattleGameViewInput.Focus();
            yield return Combat.BattleGameViewInput.ResizePortrait(450);
            var assets = OfficialDemonResultAssets.Load();
            Require(assets.Panel.GetComponent<GameEndPanelDemonKing>() && assets.AwardIcon.GetComponent<ItemAwardIcon>()
                && assets.ItemIcon.GetComponent<Zjcs.Official.ProfessionUI.ItemIcon>()
                && assets.RewardIcon && assets.ItemConfig && assets.Bindings, "Demon result catalog is incomplete.");
            foreach (var prefab in new[] { assets.Panel, assets.AwardIcon, assets.ItemIcon, assets.Boss })
                foreach (var node in prefab.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0,
                        "An original result script is missing on " + node.name);
            var original = assets.Boss.GetComponent<TempRTUI>();
            Require(original.RTSize == new Vector2Int(1024, 1024) && original.OrthoSize == 3.5f
                && original.LocalPos == new Vector3(0, -2.7f, 10) && original.LocalScale == new Vector3(.3f, .3f, 1),
                "Original 3803 RT UI geometry differs.");
            var pipeline = Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets");
            using var pipelineScope = pipeline.Enter();
            bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            var fixture = new GameObject("Original demon result asset fixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            fixture.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = fixture.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var camera = new GameObject("Demon result fixture camera", typeof(Camera)); camera.tag = "MainCamera";
            camera.transform.SetParent(fixture.transform, false);
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = Color.black; camera.GetComponent<Camera>().cullingMask = ~0;
            var inactive = new GameObject("Inactive original UI binding"); inactive.SetActive(false);
            inactive.transform.SetParent(fixture.transform, false);
            var bindings = new DemonResultRtFixtureBindings();
            GameObject icon = null;
            Camera rtCamera = null; RTHandle handle = null;
            try
            {
                icon = UnityEngine.Object.Instantiate(assets.Boss, inactive.transform, false);
                icon.SetActive(false); icon.transform.SetParent(fixture.transform, false);
                var view = icon.GetComponent<TempRTUI>(); view.Bindings = bindings;
                icon.SetActive(true); yield return null;
                handle = view.RTColorTextureHandle; rtCamera = bindings.Model.transform.parent.GetComponent<Camera>();
                Require(handle.rt && handle.rt.width == 1024 && handle.rt.height == 1024 && view.RawImage.texture == handle.rt,
                    "The original demon RT texture is unbound.");
                Require(view.RawImage.uvRect == new Rect(0, 0, 1, 1), "Original RawImage UV serialization differs.");
                Require(bindings.Model.GetComponentsInChildren<Transform>(true).Length == 28,
                    "The complete original 3803 RT hierarchy differs.");
                var renderer = bindings.Model.GetComponent<MeshRenderer>();
                var mesh = bindings.Model.GetComponent<MeshFilter>();
                Require(bindings.Model.Animator && bindings.Model.CustomMaterialOverride.Count == 1,
                    "Original demon Animator or material override is missing.");
                for (int frame = 0; frame < 12; frame++) yield return null;
                yield return new WaitForEndOfFrame();
                byte[] first = ReadActivityRt(handle.rt, out int visible);
                Require(visible > 1000, "The original demon RT model rendered no visible pixels.");
                var vertices = mesh.sharedMesh.vertices;
                for (int frame = 0; frame < 30; frame++) yield return null;
                yield return new WaitForEndOfFrame();
                byte[] second = ReadActivityRt(handle.rt, out int afterVisible);
                Require(afterVisible > 1000 && !first.SequenceEqual(second)
                    && !vertices.SequenceEqual(mesh.sharedMesh.vertices), "The original demon pose did not animate.");
                var copies = bindings.Model.GetComponentsInChildren<BattleNativeMonsterMeshCopy>(true);
                Require(copies.Length == 2 && copies.Count(copy => copy.enabled) == 1,
                    "Original demon mesh-copy enable states differ.");
                var active = copies.Single(copy => copy.enabled);
                Require(active.GetComponent<MeshFilter>().sharedMesh == mesh.sharedMesh
                    && active.GetComponent<MeshRenderer>().sharedMaterials.Length == renderer.sharedMaterials.Length,
                    "Original demon mesh-copy references or material order differ.");
                yield return new WaitForSeconds(1f);
                var particles = bindings.Model.GetComponentsInChildren<ParticleSystem>(true);
                Require(particles.Length == 14 && particles.All(value => value.isPlaying && value.particleCount > 0),
                    "Original demon particle emitters are missing or stopped.");
                // Freeze only the native pose while measuring the particles' contribution.
                bindings.Model.enabled = false;
                yield return null;
                yield return new WaitForEndOfFrame();
                second = ReadActivityRt(handle.rt, out _);
                var particleRenderers = particles.Select(value => value.GetComponent<ParticleSystemRenderer>()).ToArray();
                var enabledStates = particleRenderers.Select(value => value.enabled).ToArray();
                foreach (var value in particleRenderers) value.enabled = false;
                yield return null; yield return new WaitForEndOfFrame();
                var noParticles = ReadActivityRt(handle.rt, out _);
                for (int i = 0; i < particleRenderers.Length; i++) particleRenderers[i].enabled = enabledStates[i];
                bindings.Model.enabled = true;
                int particlePixels = Enumerable.Range(0, handle.rt.width * handle.rt.height).Count(index =>
                    second[index * 4] != noParticles[index * 4] || second[index * 4 + 1] != noParticles[index * 4 + 1]
                    || second[index * 4 + 2] != noParticles[index * 4 + 2]);
                Require(particlePixels > 5000,
                    "The original demon particles made no visible contribution to the RT image.");
                Debug.Log("[Demon RT] Original 14 particle emitters changed " + particlePixels + " pixels with the native pose held fixed.");
                yield return null; yield return new WaitForEndOfFrame();
                second = ReadActivityRt(handle.rt, out _);
                var rtImage = new Texture2D(handle.rt.width, handle.rt.height, TextureFormat.RGBA32, false);
                try { rtImage.LoadRawTextureData(second); rtImage.Apply(); File.WriteAllBytes(Path.Combine(output, "battle-host-demon-result-texture.png"), rtImage.EncodeToPNG()); }
                finally { UnityEngine.Object.Destroy(rtImage); }
                var corners = new Vector3[4]; view.RawImage.rectTransform.GetWorldCorners(corners);
                Debug.Log("[Demon RT] Canvas=" + view.RawImage.canvas + " active=" + view.RawImage.isActiveAndEnabled
                    + " cull=" + view.RawImage.canvasRenderer.cull + " alpha=" + view.RawImage.canvasRenderer.GetAlpha()
                    + " rect=" + view.RawImage.rectTransform.rect + " world=" + string.Join(";", corners.Select(v => v.ToString()))
                    + " canvas=" + ((RectTransform)fixture.transform).rect + " screen=" + Screen.width + "x" + Screen.height);
                Debug.Log("[Demon RT] graphic color=" + view.RawImage.color + " material=" + view.RawImage.materialForRendering
                    + " shader=" + view.RawImage.materialForRendering.shader + " supported=" + view.RawImage.materialForRendering.shader.isSupported
                    + " texture=" + view.RawImage.mainTexture + " overlay=" + SupportedRenderingFeatures.active.rendersUIOverlay
                    + " camera=" + camera.GetComponent<Camera>().targetTexture);
                var screen = ScreenCapture.CaptureScreenshotAsTexture();
                try {
                    File.WriteAllBytes(Path.Combine(output, "battle-host-demon-result-rt.png"), screen.EncodeToPNG());
                    Require(screen.GetPixels32().Count(pixel => pixel.r > 8 || pixel.g > 8 || pixel.b > 8) > 1000,
                        "The original demon RawImage is absent from the actual Game View.");
                }
                finally { UnityEngine.Object.Destroy(screen); }
                icon.SetActive(false); yield return null;
                Require(bindings.Owners.Count == 0 && !rtCamera.gameObject.activeSelf, "Disabled demon RT retained its render seat.");
                icon.SetActive(true); yield return null;
                Require(bindings.Owners.Count == 1 && rtCamera.gameObject.activeSelf, "Demon RT could not reopen.");
                UnityEngine.Object.Destroy(icon); icon = null; yield return null; yield return null;
                Require(!rtCamera && !handle.rt && bindings.Owners.Count == 0, "Demon result RT leaked camera, texture or bloom registration.");
                yield return ExerciseDemonResultSharedPrefabRegression(fixture.transform);
                Debug.Log("[Battle hosts] Original demon result assets passed: complete native prefabs, animated 3803 RT model, original whole-material override, two mesh-copy states, disable/reopen and camera/RT disposal. Full result host and bloom output remain pending.");
            }
            finally
            {
                if (icon) UnityEngine.Object.DestroyImmediate(icon);
                bindings.RenderSeats.Dispose(); ShaderUtil.allowAsyncCompilation = async;
                UnityEngine.Object.Destroy(fixture);
            }
        }

        private static IEnumerator ExerciseDemonResultSharedPrefabRegression(Transform parent)
        {
            var prefab = Resources.Load<GameObject>("DailyChallenges/ActivityRtPrefab/MonsterModel_3706_RT");
            var assets = Resources.Load<PartnerNativeDetailActorAssets>("DailyChallenges/ActivityRtPrefab/AppearanceAssets");
            Require(prefab && assets, "Original 3706 prefab regression inputs are absent.");
            var config = ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>();
            using var manager = new AppearanceAssetManager(config, action => new ActivityMonsterSubscription(() => { }));
            var textures = new OfflinePartnerUiTextureResources(assets, manager);
            var previous = ProfessionAppearanceTextureBindings.Current;
            ProfessionAppearanceTextureBindings.Current = textures;
            BattleNativeMonsterRenderer model = null;
            try
            {
                model = BattleNativeMonsterRenderer.CreatePrefab(prefab, parent, textures);
                yield return null;
                Require(model.GetComponentsInChildren<AttachmentFollower>(true).Length == 20
                    && textures.LoadedResourceTextureCount == 4
                    && model.GetComponent<AppearanceRendererLite>().GetIndependentMaterials().Length > 0,
                    "The existing appearance-bound 3706 prefab failed after direct-atlas support.");
                var mesh = model.GetComponent<MeshFilter>();
                var first = mesh.sharedMesh.vertices;
                for (int frame = 0; frame < 30; frame++) yield return null;
                Require(!first.SequenceEqual(mesh.sharedMesh.vertices), "Existing 3706 native Animator did not advance.");
                Debug.Log("[Battle hosts] Existing 3706 native prefab regression passed: appearance binding, four atlas pages, 20 followers and animated mesh.");
            }
            finally
            {
                if (model) UnityEngine.Object.DestroyImmediate(model.gameObject);
                ProfessionAppearanceTextureBindings.Current = previous;
                UnityEngine.Object.Destroy(config);
            }
        }

        private sealed class DemonResultRtFixtureBindings : IActivityRtUIBindings
        {
            public readonly SpineRTUIManager RenderSeats = new SpineRTUIManager();
            ISpineRTUIManager IActivityRtUIBindings.RenderSeats => RenderSeats;
            public readonly HashSet<IUIBloomRequirer> Owners = new HashSet<IUIBloomRequirer>();
            public BattleNativeMonsterRenderer Model;
            public GameObject InstantiateTarget(GameObject source, Transform parent)
            { Model = BattleNativeMonsterRenderer.CreatePrefab(source, parent, null); return Model.gameObject; }
            public Material[] GetIndependentMaterials(GameObject instance) => null; // The original 3803 has no AppearanceRendererLite.
            public void RegisterBloom(IUIBloomRequirer value) { Require(Owners.Add(value), "Duplicate demon RT bloom owner."); }
            public void UnregisterBloom(IUIBloomRequirer value) { Require(Owners.Remove(value), "Unbalanced demon RT bloom owner."); }
        }
    }
}
