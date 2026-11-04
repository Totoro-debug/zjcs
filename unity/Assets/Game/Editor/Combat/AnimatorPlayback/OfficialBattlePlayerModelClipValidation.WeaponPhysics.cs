using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        [Serializable] private sealed class WeaponPhysicsSkin
        {
            public string name; public bool scene; public string[] constraints, motions; public int[] physics_bones;
        }
        [Serializable] private sealed class WeaponPhysicsCases { public WeaponPhysicsSkin[] skins; }

        public static void RunWeaponPhysics()
        {
            RunHighTextures(); SessionState.SetBool(Key + ".weapon-physics", true);
        }

        private static void CheckWeaponPhysics()
        {
            string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            var fixture = JsonUtility.FromJson<WeaponPhysicsCases>(File.ReadAllText(Path.Combine(work, "weapon-physics-cases.json")));
            var catalog = Resources.Load<ProfessionNativeWeaponCatalog>("ProfessionUI/weapon-resources");
            var resources = Resources.Load<ProfessionNativeWeaponTextureResources>("Combat/Weapons/texture-resources");
            var config = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
            Require(fixture.skins.Length == 399 && fixture.skins.Count(s => s.constraints.Length > 0) == 56,
                "all original primary weapon skins and constrained skins");
            using var manager = new AppearanceAssetManager(resources.ManagerConfig, _ => new Subscription());
            var binding = new OfflineProfessionWeaponTextureResources(config, manager, resources);
            var previous = ProfessionAppearanceTextureBindings.Current; bool previousAsync = ShaderUtil.allowAsyncCompilation;
            ProfessionAppearanceTextureBindings.Current = binding; ShaderUtil.allowAsyncCompilation = false;
            int motions = 0, captures = 0, displaced = 0;
            try
            {
                foreach (var item in fixture.skins)
                {
                    var skeleton = WeaponSkeleton(catalog, item.name);
                    Require(skeleton.SkinConstraints.Select(i => skeleton.Source.Constraints[i].Name).OrderBy(s => s)
                        .SequenceEqual(item.constraints.OrderBy(s => s)), "exact source skin constraint selection: " + item.name);
                    var playback = new ProfessionNativeSceneWeaponPlayback();
                    foreach (string motion in item.constraints.Length == 0 ? item.motions.Take(1) : item.motions)
                    {
                        playback.Play(skeleton, motion, true);
                        float start = playback.PhysicsTime;
                        // Several Update calls can happen before a demand redraw.
                        for (int frame = 0; frame < 12; frame++) playback.Advance(1f / 60);
                        Require(Mathf.Abs(playback.PhysicsTime - start - .2f) < .00001f, "weapon advances physical time once per Update");
                        var matrices = WeaponMatrices(skeleton);
                        for (int redraw = 0; redraw < 4; redraw++)
                        {
                            skeleton.SetToSetupPose(); playback.Apply(skeleton);
                            Require(WeaponMatrixDelta(matrices, WeaponMatrices(skeleton)) < .0001f,
                                "skin/setup redraw preserves completed weapon pose and physical history: " + item.name + "/" + motion);
                        }
                        Require(Mathf.Abs(playback.PhysicsTime - start - .2f) < .00001f, "redraw never advances weapon physics");
                        if (item.physics_bones.Length > 0)
                        {
                            var unphysical = WeaponSkeleton(catalog, item.name);
                            ProfessionNativeConstraintPose.EvaluateBeforePhysics(unphysical,
                                new ProfessionNativeAnimationChannels(catalog.Model.Data, motion), .2f);
                            if (item.physics_bones.Any(b => WeaponMatrixDelta(new[] { skeleton.Pose.World(b) }, new[] { unphysical.Pose.World(b) }) > .0001f)) displaced++;
                        }
                        if (item.scene && item.constraints.Length > 0 && motion == item.motions[0])
                        {
                            using var batches = new ProfessionNativeMeshBatches(skeleton.SlotCount);
                            batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), pmaVertexColors: true, allowConcaveClipping: true);
                            var source = batches.Batches.Select(b => Enumerable.Range(0, resources.Atlas.MaterialCount)
                                .Select(i => resources.Atlas.MaterialAt(i)).Single(m => m.mainTexture.name == b.Page + "_placeholder")).ToArray();
                            var shared = new Material[source.Length]; IAppearanceAssetManager assets = manager;
                            assets.GetLoaderSharedMaterials(resources.Loader, source, ref shared);
                            try { CaptureHighWeapon(batches, shared, "physics-" + item.name, work); captures++; }
                            finally { assets.ReturnLoaderSharedMaterials(resources.Loader, source, true); }
                        }
                        float clock = playback.PhysicsTime;
                        playback.Play(skeleton, motion, true);
                        Require(playback.PhysicsTime == clock, "same-skeleton replay retains weapon physical clock");
                        var replay = WeaponMatrices(skeleton); playback.Apply(skeleton);
                        Require(WeaponMatrixDelta(replay, WeaponMatrices(skeleton)) < .0001f, "replay redraw is stable");
                        motions++;
                    }
                    playback.Play(WeaponSkeleton(catalog, item.name), item.motions[0], true);
                    Require(playback.PhysicsTime == 0, "a new weapon skeleton starts a new physical history");
                }
                Require(displaced > 0 && captures > 0 && binding.LoadedResourceTextureCount == 0,
                    "actual weapon physics changes world poses and every High request is returned");
                Debug.Log("Original weapon physics passed: 399 skins, 56 constrained skins, " + motions + " motions, "
                    + captures + " actual High skeletal captures, " + displaced + " visibly displaced physical samples; "
                    + "all finite matrices, once-per-Update time, stable setup redraw, retained replay and new-skeleton reset. Full prefab effects and production battle remain separate.");
            }
            finally { ProfessionAppearanceTextureBindings.Current = previous; ShaderUtil.allowAsyncCompilation = previousAsync; }
        }

        private static ProfessionNativeAppearanceSkeleton WeaponSkeleton(ProfessionNativeWeaponCatalog catalog, string name)
        {
            var skeleton = new ProfessionNativeAppearanceSkeleton(catalog.Model.Data);
            skeleton.SetSkin(skeleton.Data.FindSkin(name)); skeleton.SetToSetupPose(); skeleton.UpdateCache(); return skeleton;
        }

        private static Matrix4x4[] WeaponMatrices(ProfessionNativeAppearanceSkeleton skeleton)
        {
            var matrices = Enumerable.Range(0, skeleton.Source.BoneCount).Where(skeleton.Pose.IsActive).Select(skeleton.Pose.World).ToArray();
            Require(matrices.All(m => Enumerable.Range(0, 16).All(i => float.IsFinite(m[i]))), "finite completed weapon world matrices");
            return matrices;
        }

        private static float WeaponMatrixDelta(Matrix4x4[] first, Matrix4x4[] second)
        {
            Require(first.Length == second.Length, "same active weapon bones after redraw");
            return first.Select((m, n) => Enumerable.Range(0, 16).Max(i => Mathf.Abs(m[i] - second[n][i]))).DefaultIfEmpty(0).Max();
        }
    }
}
