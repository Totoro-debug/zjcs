using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        [Serializable] private sealed class BoneOverrideInput
        { public string bone; public bool controller, enabled, delta, ui; public int mode; public float world_z; }
        [Serializable] private sealed class BoneOverrideCase
        { public string name, skin, motion, hierarchy_file; public BoneOverrideInput[] overrides; }
        [Serializable] private sealed class BoneOverrideCases { public BoneOverrideCase[] cases; }

        public static void RunBoneOverrides()
        {
            Run(); SessionState.SetBool(Key + ".weapon-bones", true);
        }

        private static IEnumerator CheckWeaponBoneOverrides()
        {
            var work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            var fixture = JsonUtility.FromJson<BoneOverrideCases>(File.ReadAllText(Path.Combine(work, "weapon-bone-overrides.json")));
            var weaponCatalog = Resources.Load<ProfessionNativeWeaponCatalog>("ProfessionUI/weapon-resources");
            var textures = Resources.Load<ProfessionNativeWeaponTextureResources>("Combat/Weapons/texture-resources");
            var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
            using var manager = new AppearanceAssetManager(textures.ManagerConfig, _ => new Subscription());
            var binding = new OfflineProfessionWeaponTextureResources(config, manager, textures);
            var previous = ProfessionAppearanceTextureBindings.Current;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            ProfessionAppearanceTextureBindings.Current = binding; ShaderUtil.allowAsyncCompilation = false;
            int overrides = 0, orphans = 0;
            try
            {
                CheckRotationBranches();
                foreach (var item in fixture.cases)
                {
                    var source = new TextAsset(File.ReadAllText(item.hierarchy_file));
                    var catalog = ScriptableObject.CreateInstance<ProfessionNativeSceneWeaponCatalog>();
                    var initial = WeaponSkeleton(weaponCatalog, item.skin);
                    string page = ((ProfessionNativeSkin)initial.Skin).Entries.Select(e => e.Attachment)
                        .OfType<ProfessionNativeRenderAttachment>().Select(a => a.Page).Distinct().Single();
                    var material = Enumerable.Range(0, textures.Atlas.MaterialCount).Select(textures.Atlas.MaterialAt)
                        .Single(m => m.mainTexture.name == page + "_placeholder");
                    var serialized = new SerializedObject(catalog);
                    serialized.FindProperty("weapon").objectReferenceValue = weaponCatalog;
                    serialized.FindProperty("textures").objectReferenceValue = textures;
                    var entries = serialized.FindProperty("entries"); entries.arraySize = 1;
                    var entry = entries.GetArrayElementAtIndex(0);
                    entry.FindPropertyRelative("InitialSkin").stringValue = item.skin;
                    entry.FindPropertyRelative("InitialAnimation").stringValue = item.motion;
                    entry.FindPropertyRelative("Hierarchy").objectReferenceValue = source;
                    entry.FindPropertyRelative("OriginalMaterial").objectReferenceValue = material;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    var root = new GameObject("Original weapon override diagnostic " + item.name); root.SetActive(false);
                    var native = root.AddComponent<ProfessionNativeSceneWeaponRenderer>(); native.enabled = false;
                    native.Configure(catalog, item.skin); native.Initialize(false, false);
                    var controller = root.AddComponent<SkeletonBoneController>();
                    var children = new List<SkeletonBoneRotOverride>();
                    foreach (var input in item.overrides)
                    {
                        var go = new GameObject(input.bone); go.transform.SetParent(root.transform, false);
                        var rot = go.AddComponent<SkeletonBoneRotOverride>();
                        rot.Controller = input.controller ? controller : null; rot.BoneName = input.bone;
                        rot.RotMode = (OverrideRotMode)input.mode; rot.UseDelta = input.delta; rot.enabled = input.enabled;
                        var locker = go.AddComponent<TransformEulerZLocker>(); locker.WorldEulerZ = input.world_z; locker.IsUI = input.ui;
                        children.Add(rot);
                    }
                    try
                    {
                        root.transform.rotation = Quaternion.Euler(0, 0, 37); root.SetActive(true);
                        // Let the actual MonoBehaviour Update reapply the world
                        // angle after a controlled parent movement.
                        yield return null;
                        root.transform.rotation = Quaternion.Euler(0, 0, 79); yield return null; yield return null;
                        var phases = new List<string>();
                        native.UpdateLocal += _ => phases.Add("local");
                        native.UpdateComplete += _ => phases.Add("complete");
                        var playback = (ProfessionNativeSceneWeaponPlayback)typeof(ProfessionNativeSceneWeaponRenderer)
                            .GetField("playback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(native);
                        playback.Advance(.2f);
                        Require(phases.SequenceEqual(new[] { "local", "complete" }), "one ordered local/complete dispatch per weapon Advance");
                        float clock = playback.PhysicsTime;
                        native.UpdateAnimationAndMesh();
                        Require(playback.PhysicsTime == clock, "bone callbacks during redraw never advance physics");
                        for (int i = 0; i < children.Count; i++)
                        {
                            var input = item.overrides[i]; var child = children[i];
                            Require(Mathf.Abs(Mathf.DeltaAngle(child.transform.eulerAngles.z, input.world_z)) < .002f,
                                "actual map locker preserves original world rotation after parent motion");
                            int bone = native.State.Source.FindBone(input.bone);
                            Require(bone >= 0, "actual source override bone exists");
                            if (input.controller)
                            {
                                float angle = child.transform.localEulerAngles.z; if (angle > 180) angle -= 360;
                                Require(Mathf.Abs(native.State.Pose.LocalRotation(bone) - angle) < .002f,
                                    "original rotation override reaches local pose before world constraints: " + item.name);
                                Require(Mathf.Abs(native.FindOverrideBone(input.bone).AppliedRotation - angle) < .002f,
                                    "paired applied-local rotation reaches original callback boundary");
                                overrides++;
                            }
                            else orphans++;
                        }
                        Require(WeaponMatrices(native.State).Length > 0, "finite completed original weapon matrices after overrides");
                        var before = WeaponMatrices(native.State);
                        for (int redraw = 0; redraw < 3; redraw++) { native.State.SetToSetupPose(); native.UpdateAnimationAndMesh(); }
                        Require(WeaponMatrixDelta(before, WeaponMatrices(native.State)) < .0001f && playback.PhysicsTime == clock,
                            "original Override mode is stable across setup redraws");
                        var shared = new Material[1]; IAppearanceAssetManager assets = manager;
                        assets.GetLoaderSharedMaterials(textures.Loader, new[] { material }, ref shared);
                        try
                        {
                            using var batches = new ProfessionNativeMeshBatches(native.State.SlotCount);
                            batches.Build(native.State.Pose, native.State.CreateDrawSlots(), pmaVertexColors: true, allowConcaveClipping: true);
                            Require(batches.Batches.All(b => b.Page == page), "original override skin retains its actual atlas page");
                            CaptureHighWeapon(batches, Enumerable.Repeat(shared[0], batches.Batches.Count).ToArray(), "bones-" + item.name, work);
                        }
                        finally { assets.ReturnLoaderSharedMaterials(textures.Loader, new[] { material }, true); }
                        controller.enabled = false; native.UpdateAnimationAndMesh();
                        var plain = WeaponSkeleton(weaponCatalog, item.skin);
                        ProfessionNativeConstraintPose.EvaluateBeforePhysics(plain, new ProfessionNativeAnimationChannels(plain.Source, item.motion), .2f);
                        foreach (var input in item.overrides)
                        {
                            int bone = plain.Source.FindBone(input.bone);
                            Require(native.State.Pose.LocalRotation(bone) == plain.Pose.LocalRotation(bone),
                                "disabled controller unsubscribes both callbacks; null Controller remains unbound");
                        }
                        controller.enabled = true; root.SetActive(false); root.SetActive(true); native.UpdateAnimationAndMesh();
                        foreach (var child in children.Where(c => c.Controller))
                        {
                            float angle = child.transform.localEulerAngles.z; if (angle > 180) angle -= 360;
                            Require(Mathf.Abs(native.State.Pose.LocalRotation(native.State.Source.FindBone(child.BoneName)) - angle) < .002f,
                                "pool disable/enable restores original override registration");
                        }
                    }
                    finally { root.SetActive(false); Object.DestroyImmediate(root); Object.DestroyImmediate(catalog); Object.DestroyImmediate(source); }
                }
                Require(fixture.cases.Length == 15 && overrides == 17 && orphans == 1 && binding.LoadedResourceTextureCount == 0,
                    "all 18 original overrides, including the original null controller, with every High request returned");
                Debug.Log("Original weapon bone callbacks passed: 15 source prefabs, 18 rotations (17 bound/one original null), "
                    + "actual map-locker Update, local/complete ordering, paired local values, finite world poses, setup redraw, controller disable and pool enable; 15 actual High skeletal captures. Complete prefab particles/sprites and production integration remain separate.");
            }
            finally { ProfessionAppearanceTextureBindings.Current = previous; ShaderUtil.allowAsyncCompilation = previousAsync; }
        }

        private sealed class RotationBone : IProfessionLocalOverrideBone { public float Rotation { get; set; } public float AppliedRotation { get; set; } }
        private sealed class RotationAnimation : IProfessionBoneOverrideAnimation
        {
            internal readonly RotationBone Bone = new RotationBone();
            public IProfessionLocalOverrideBone FindOverrideBone(string name) => name == "diagnostic" ? Bone : null;
            public event Action<IProfessionBoneOverrideAnimation> UpdateLocal { add { } remove { } }
            public event Action<IProfessionBoneOverrideAnimation> UpdateComplete { add { } remove { } }
        }
        private static void CheckRotationBranches()
        {
            var go = new GameObject("Original rotation branch diagnostic"); go.SetActive(false);
            try
            {
                var rot = go.AddComponent<SkeletonBoneRotOverride>(); rot.BoneName = "diagnostic";
                var animation = new RotationAnimation();
                animation.Bone.Rotation = 10; animation.Bone.AppliedRotation = 20;
                rot.RotMode = OverrideRotMode.Offset; go.transform.localEulerAngles = Vector3.forward * 350;
                rot.OverrideUpdate(animation);
                Require(Mathf.Abs(animation.Bone.Rotation) < .002f && Mathf.Abs(animation.Bone.AppliedRotation - 10) < .002f,
                    "original offset normalizes angles above 180 and updates both independent local inputs");
                rot.UseDelta = true; go.transform.localEulerAngles = Vector3.forward * 20; rot.OverrideUpdate(animation);
                Require(Mathf.Abs(animation.Bone.Rotation - 30) < .002f && Mathf.Abs(animation.Bone.AppliedRotation - 40) < .002f,
                    "original delta offset retains last angle across calls");
                rot.RotMode = OverrideRotMode.Override; rot.OverrideUpdate(animation);
                Require(Mathf.Abs(animation.Bone.Rotation - 20) < .002f && Mathf.Abs(animation.Bone.AppliedRotation - 20) < .002f,
                    "original absolute override updates both rotations");
                rot.RotMode = (OverrideRotMode)99; bool rejected = false;
                try { rot.OverrideUpdate(animation); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "original invalid rotation mode throws");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
