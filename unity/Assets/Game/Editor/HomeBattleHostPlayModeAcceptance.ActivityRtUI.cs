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
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.BattleSpinePlayables;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator ExerciseActivityRtUI(string output)
        {
            string path = Environment.GetEnvironmentVariable("ZJCS_ACTIVITY_RT_PREFAB");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab, "Import the verified original TempRTUI component fixture.");
            var pipeline = Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets");
            Require(pipeline, "Bind the existing original Universal render pipeline.");
            using var pipelineScope = pipeline.Enter();
            bool previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            ValidateActivitySharedUiPhysics();
            ValidateActivitySharedMonsterPhysics();
            var fixture = new GameObject("Original activity RT component fixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            fixture.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = fixture.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var main = new GameObject("RT component fixture clear camera", typeof(Camera));
            main.transform.SetParent(fixture.transform, false);
            main.tag = "MainCamera";
            var camera = main.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            var staging = new GameObject("Inactive original RT component staging", typeof(RectTransform));
            staging.transform.SetParent(fixture.transform, false);
            staging.SetActive(false);
            int modelId = Environment.GetEnvironmentVariable("ZJCS_ACTIVITY_RT_MODEL") == "3706" ? 3706 : 3709;
            var assets = modelId == 3706
                ? Resources.Load<BattleNativeMonsterAssets>("DailyChallenges/ActivityRtMonsters/catalog")
                : BattleNativeMonsterAssets.Load();
            Require(assets, "The selected original native RT model inputs are unavailable.");
            // Exercise the original model inputs through the shared native mesh
            // backend. The full source prefab hierarchy is a separate binding.
            bool complete = Environment.GetEnvironmentVariable("ZJCS_ACTIVITY_RT_COMPLETE") == "1";
            GameObject template = complete
                ? Resources.Load<GameObject>("DailyChallenges/ActivityRtPrefab/MonsterModel_3706_RT")
                : BattleNativeMonsterRenderer.Create(assets, modelId, staging.transform, "Original native test model").gameObject;
            Require(template, "Bind the complete original RT target.");
            var config = ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>();
            using var appearanceManager = new AppearanceAssetManager(config, action => new ActivityMonsterSubscription(() => { }));
            var appearanceAssets = complete ? Resources.Load<PartnerNativeDetailActorAssets>("DailyChallenges/ActivityRtPrefab/AppearanceAssets") : null;
            var textures = complete ? new OfflinePartnerUiTextureResources(appearanceAssets, appearanceManager) : null;
            var previousTextures = ProfessionAppearanceTextureBindings.Current;
            if (complete) ProfessionAppearanceTextureBindings.Current = textures;
            var seats = new SpineRTUIManager();
            var bindings = new ActivityRtBindings(assets, modelId, template, seats, textures);
            GameObject first = null, second = null;
            RTHandle firstHandle = null, secondHandle = null;
            Camera firstCamera = null, secondCamera = null;
            try
            {
                first = CreateActivityRtIcon(prefab, fixture.transform, staging.transform, bindings, template);
                var a = first.GetComponent<TempRTUI>();
                first.SetActive(true);
                yield return null;
                firstHandle = a.RTColorTextureHandle;
                firstCamera = bindings.Cameras[0];
                Require(firstHandle.rt && firstHandle.rt.width == 640 && firstHandle.rt.height == 512
                    && a.RawImage.texture == firstHandle.rt && firstCamera.gameObject.activeSelf,
                    "The original RT size, texture assignment or initial seat differs.");
                Require(firstCamera.orthographic && firstCamera.orthographicSize == 3.5f
                    && firstCamera.aspect == 1.25f && !firstCamera.enabled
                    && firstCamera.transform.position == new Vector3(-5000, -5000, -100),
                    "The original disabled offscreen camera projection differs.");
                Require(a.LocalPos == new Vector3(0, -2.7f, 10) && a.LocalScale == new Vector3(.3f, .3f, 1)
                    && bindings.Models[0].transform.localPosition == a.LocalPos
                    && bindings.Models[0].transform.localScale == a.LocalScale,
                    "Original RT target transforms differ.");
                for (int frame = 0; frame < 6; frame++)
                { bindings.Advance(.04f); yield return null; }
                yield return new WaitForEndOfFrame();
                byte[] before = ReadActivityRt(firstHandle.rt, out int visible);
                Vector3[] followerPositions = complete ? ActivityRtFollowerPositions(bindings.Models[0]) : null;
                Debug.Log("[Battle hosts] RT diagnostic pixels=" + visible + " camera=" + firstCamera.cullingMask
                    + " model=" + bindings.Models[0].GetComponent<MeshRenderer>().bounds
                    + " materials=" + string.Join(",", bindings.Models[0].GetComponent<MeshRenderer>().sharedMaterials
                        .Distinct().Select(value => value.name + "/" + value.shader.name + "/supported=" + value.shader.isSupported
                            + "/texture=" + (value.mainTexture ? value.mainTexture.name + ":" + value.mainTexture.width : "null")
                            + "/passes=" + string.Join(",", Enumerable.Range(0, value.passCount).Select(value.GetPassName))))
                    + " requested=" + textures?.LoadedResourceTextureCount
                    + " layer=" + bindings.Models[0].gameObject.layer);
                Require(visible > 500, "The original Universal2D render pass produced no native model pixels.");
                if (complete)
                {
                    var root = bindings.Models[0].gameObject;
                    Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(value => value), "Missing original RT application script.");
                    var followers = root.GetComponentsInChildren<AttachmentFollower>(true);
                    Require(followers.Length == 20 && followers.All(value => value.SkeletonRenderer == bindings.Models[0]),
                        "The complete original RT follower references differ.");
                    Require(root.GetComponentsInChildren<ParticleSystem>(true).Length > 0 && textures.LoadedResourceTextureCount == 4,
                        "The original RT particles or four Low atlas pages are unbound.");
                    Debug.Log("[Battle hosts] Complete RT hierarchy: followers=" + followers.Length
                        + " particles=" + root.GetComponentsInChildren<ParticleSystem>(true).Length
                        + " alive=" + root.GetComponentsInChildren<ParticleSystem>(true).Sum(value => value.particleCount));
                }
                for (int frame = 0; frame < 8; frame++)
                { bindings.Advance(.04f); yield return null; }
                yield return new WaitForEndOfFrame();
                Require(!before.SequenceEqual(ReadActivityRt(firstHandle.rt, out _)),
                    "The original offscreen pass did not render the advanced native Animator pose.");
                if (complete) Require(!followerPositions.SequenceEqual(ActivityRtFollowerPositions(bindings.Models[0])),
                    "Original RT followers did not advance with their native bones.");
                var screen = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(output, "activity-rt-native.png"), screen.EncodeToPNG()); }
                finally { UnityEngine.Object.Destroy(screen); }

                second = CreateActivityRtIcon(prefab, fixture.transform, staging.transform, bindings, template);
                var b = second.GetComponent<TempRTUI>();
                second.SetActive(true); yield return null;
                secondHandle = b.RTColorTextureHandle;
                secondCamera = bindings.Cameras[1];
                Require(b.RawImage.texture == firstHandle.rt && !secondCamera.gameObject.activeSelf,
                    "A matching target must reuse the occupied seat's texture without rendering twice.");
                seats.ReturnRenderSeat(int.MaxValue);
                Require(!secondCamera.gameObject.activeSelf, "A foreign instance returned the original render seat.");
                first.SetActive(false); yield return null;
                Require(!firstCamera.gameObject.activeSelf && secondCamera.gameObject.activeSelf
                    && b.RawImage.texture == secondHandle.rt && bindings.BloomOwners.Count == 1,
                    "Disabling the owner did not transfer its render seat and unregister its bloom requirement.");
                first.SetActive(true); yield return null;
                Require(!firstCamera.gameObject.activeSelf && a.RawImage.texture == secondHandle.rt,
                    "Reopening the first icon must reuse the current owner's texture.");
                UnityEngine.Object.Destroy(second); second = null;
                yield return null; yield return null;
                Require(!secondCamera && !secondHandle.rt && firstCamera.gameObject.activeSelf
                    && a.RawImage.texture == firstHandle.rt && bindings.BloomOwners.Count == 1,
                    "Destroying the owner did not release its camera/RT and hand back the seat.");
                UnityEngine.Object.Destroy(first); first = null;
                yield return null; yield return null;
                Require(!firstCamera && !firstHandle.rt && bindings.BloomOwners.Count == 0
                    && bindings.Registers == 3 && bindings.Unregisters == 3,
                    "Original RT destruction leaked a camera, texture, or bloom registration.");
                appearanceManager.Update();
                if (complete) Require(textures.LoadedResourceTextureCount == 0, "Original RT destruction retained Low atlas requests.");
                Debug.Log("[Battle hosts] Original TempRTUI passed: native " + modelId + " pixels and Animator changes, original 3706 icon geometry/camera/RT settings, same-target sharing, foreign-seat guard, disable/reopen handover and camera/RT disposal. "
                    + (complete ? "Complete original hierarchy, 20 follower references/positions and moving poses, particles, four Low atlas requests and release covered. "
                        : "Full native hierarchy and particles not covered. ")
                    + "Bloom output and production entry are not covered.");
            }
            finally
            {
                if (second) UnityEngine.Object.DestroyImmediate(second);
                if (first) UnityEngine.Object.DestroyImmediate(first);
                seats.Dispose();
                ProfessionAppearanceTextureBindings.Current = previousTextures;
                ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
                UnityEngine.Object.Destroy(config);
                UnityEngine.Object.Destroy(fixture);
            }
        }

        private static GameObject CreateActivityRtIcon(GameObject prefab, Transform parent, Transform staging,
            ActivityRtBindings bindings, GameObject template)
        {
            var icon = UnityEngine.Object.Instantiate(prefab, staging, false);
            icon.SetActive(false);
            icon.transform.SetParent(parent, false);
            var view = icon.GetComponent<TempRTUI>();
            view.Bindings = bindings;
            view.RTTarget = template;
            return icon;
        }

        private static void ValidateActivitySharedUiPhysics()
        {
            int validated = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:PartnerNativeUiSkeletonAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<PartnerNativeUiSkeletonAsset>(AssetDatabase.GUIDToAssetPath(guid));
                var data = asset.Data;
                if (data.Sex != "partner-ui:8402") continue;
                bool model = data.AnimationNames.Contains("npc_8402/idle_01");
                string idle = model ? "npc_8402/idle_01" : "idle_01";
                var skeleton = new ProfessionNativeAppearanceSkeleton(data);
                skeleton.SetSkin(model
                    ? data.CreateCombinedNpcSkin(new[] { "body_8402" })
                    : data.SkinCatalog.FindSkin("default") as ProfessionNativeSkin);
                skeleton.SetToSetupPose(); skeleton.UpdateCache();
                var physics = new PartnerNativeUiPhysicsPose(data);
                string touch = model ? "npc_8402/idle_02" : "click_01";
                var a = new ProfessionNativeAnimationChannels(data, idle, originalBezierSegments: true);
                var b = new ProfessionNativeAnimationChannels(data, touch, originalBezierSegments: true);
                for (int frame = 0; frame < 120; frame++)
                {
                    physics.Apply(skeleton, b, frame / 60f, false, 1f / 60, a, frame / 60f, true,
                        Mathf.Clamp01(frame / 15f));
                    for (int bone = 0; bone < data.BoneCount; bone++)
                    {
                        if (!skeleton.Pose.IsActive(bone)) continue;
                        var pose = skeleton.Pose.World(bone);
                        Require(float.IsFinite(pose.m00) && float.IsFinite(pose.m11)
                            && float.IsFinite(pose.m03) && float.IsFinite(pose.m13), "Shared UI physics produced a non-finite pose.");
                    }
                }
                Require(Math.Abs(physics.PhysicsTime - 2f) < .00001f, "Shared UI physics advances once per frame.");
                using var meshes = new ProfessionNativeMeshBatches(data.SlotCount);
                meshes.Build(skeleton.Pose, skeleton.CreateDrawSlots(), true, false);
                Require(meshes.Batches.Sum(batch => batch.Mesh.vertexCount) > 0, "Shared UI physics lost original geometry.");
                validated++;
            }
            Require(validated >= 2, "Regress both original 8402 model and illustration rigs.");
            Debug.Log("[Battle hosts] Shared ordered physics passed original 8402 UI rigs: " + validated);
        }

        private static void ValidateActivitySharedMonsterPhysics()
        {
            var assets = BattleNativeMonsterAssets.Load();
            foreach (int id in new[] { 3903, 3925 })
            {
                var row = assets.Find(id);
                var data = ProfessionNativeSkeletonData.LoadBattleMonster(assets.Resolve<TextAsset>((string)row["package"]).bytes, id);
                var a = new ProfessionNativeAppearanceSkeleton(data);
                var b = new ProfessionNativeAppearanceSkeleton(data);
                var skins = ((List<object>)row["skins"]).Cast<string>().ToArray();
                a.SetSkin(data.CreateCombinedNpcSkin(skins)); b.SetSkin(data.CreateCombinedNpcSkin(skins));
                var original = new ProfessionNativePlayerPhysicsPose(data);
                var shared = new ProfessionNativeOrderedPhysicsPose(data);
                var animation = new ProfessionNativeAnimationChannels(data, "idle_01", originalBezierSegments: true);
                for (int frame = 0; frame < 120; frame++)
                {
                    original.Apply(a, animation, frame / 60f, true, 1f / 60);
                    shared.Apply(b, animation, frame / 60f, true, 1f / 60);
                    for (int bone = 0; bone < data.BoneCount; bone++)
                    {
                        if (!a.Pose.IsActive(bone)) continue;
                        var x = a.Pose.World(bone); var y = b.Pose.World(bone);
                        for (int axis = 0; axis < 16; axis++)
                            Require(Math.Abs(x[axis] - y[axis]) < .0001f,
                                "Shared ordered physics changed the existing monster pose: " + id + "/" + bone + "/" + frame);
                    }
                }
            }
            Debug.Log("[Battle hosts] Shared ordered physics preserved original 3903/3925 poses over 240 frames.");
        }

        private static byte[] ReadActivityRt(RenderTexture source, out int visible)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = source;
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                image.Apply();
                visible = image.GetPixels32().Count(pixel => pixel.a > 0 && (pixel.r > 0 || pixel.g > 0 || pixel.b > 0));
                return image.GetRawTextureData<byte>().ToArray();
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(image); }
        }

        private static Vector3[] ActivityRtFollowerPositions(BattleNativeMonsterRenderer model)
        {
            var followers = model.GetComponentsInChildren<AttachmentFollower>(true);
            int checkedBones = 0;
            foreach (var follower in followers)
            {
                var skeleton = (IProfessionFollowerSkeleton)model.State;
                var slot = string.IsNullOrEmpty(follower.SlotName) ? null : skeleton.FindSlot(follower.SlotName);
                var bone = string.IsNullOrEmpty(follower.BoneName) ? slot?.Bone : skeleton.FindBone(follower.BoneName);
                Require(bone != null, "Original RT follower bone is unbound: " + follower.name);
                if (!bone.Active || !follower.isActiveAndEnabled) continue;
                if (slot != null && !string.IsNullOrEmpty(follower.AttachmentName)
                    && slot.Attachment != skeleton.GetAttachment(slot.Index, follower.AttachmentName)) continue;
                var expected = model.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0));
                Require(Mathf.Abs(expected.x - follower.transform.position.x) < .002f
                    && Mathf.Abs(expected.y - follower.transform.position.y) < .002f,
                    "Original RT follower differs from its completed native bone: " + follower.name);
                checkedBones++;
            }
            Require(checkedBones > 0, "No original RT follower had an active matching attachment.");
            return followers.Select(value => value.transform.localPosition).ToArray();
        }

        private sealed class ActivityRtBindings : IActivityRtUIBindings
        {
            private readonly BattleNativeMonsterAssets assets;
            private readonly int modelId;
            private readonly GameObject template;
            private readonly OfflinePartnerUiTextureResources textures;
            public ISpineRTUIManager RenderSeats { get; }
            public readonly List<BattleNativeMonsterRenderer> Models = new List<BattleNativeMonsterRenderer>();
            public readonly List<Camera> Cameras = new List<Camera>();
            public readonly HashSet<IUIBloomRequirer> BloomOwners = new HashSet<IUIBloomRequirer>();
            public int Registers, Unregisters;
            public ActivityRtBindings(BattleNativeMonsterAssets value, int id, GameObject original, ISpineRTUIManager seats,
                OfflinePartnerUiTextureResources appearance)
            { assets = value; modelId = id; template = original; RenderSeats = seats; textures = appearance; }
            public GameObject InstantiateTarget(GameObject source, Transform parent)
            {
                Require(source == template, "The RT target identity differs.");
                if (textures != null)
                {
                    var instance = BattleNativeMonsterRenderer.CreatePrefab(source, parent, textures);
                    Models.Add(instance); Cameras.Add(parent.GetComponent<Camera>());
                    return instance.gameObject;
                }
                var model = BattleNativeMonsterRenderer.Create(assets, modelId, parent, "Original native " + modelId + " RT test mesh");
                model.BindOriginalAnimator(assets, modelId);
                // Select the actual original model node from the battle entity
                // carrier; TempRTUI then applies its own serialized transforms.
                model.transform.SetParent(parent, false);
                if (model.EntityRoot != model.transform) UnityEngine.Object.Destroy(model.EntityRoot.gameObject);
                Models.Add(model); Cameras.Add(parent.GetComponent<Camera>());
                return model.gameObject;
            }
            public Material[] GetIndependentMaterials(GameObject instance)
            { return textures != null ? instance.GetComponent<AppearanceRendererLite>().GetIndependentMaterials()
                : instance.GetComponent<MeshRenderer>().sharedMaterials; }
            public void RegisterBloom(IUIBloomRequirer requirer)
            { Require(BloomOwners.Add(requirer), "Duplicate original bloom registration."); Registers++; }
            public void UnregisterBloom(IUIBloomRequirer requirer)
            { Require(BloomOwners.Remove(requirer), "Unbalanced original bloom unregistration."); Unregisters++; }
            public void Advance(float delta)
            { if (textures == null) foreach (var model in Models) if (model) model.Advance(delta); }
        }
    }
}
