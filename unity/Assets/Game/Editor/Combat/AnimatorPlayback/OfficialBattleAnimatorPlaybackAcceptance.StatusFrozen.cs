using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleMaterialPlayables;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunStatusFrozen()
        {
            RunScenePlayerAnimator(); SessionState.SetBool(Key + ".statusFrozen", true);
        }

        private static IEnumerator CheckStatusFrozen(PlayerAnimatorAppearance player)
        {
            var actor = player.gameObject; var animator = player.Animator; var controller = player.AnimController;
            var renderer = actor.GetComponent<ProfessionNativeSceneRenderer>();
            var parts = actor.GetComponent<AppearanceRendererPartHandler>();
            var weaponRoot = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
            var weapon = weaponRoot.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
            var sampler = new ProfessionNativeAnimatorPose(animator, ProfessionNativeConstraintPose.EvaluateBeforePhysics);
            var root = new GameObject("Complete original global frozen graphs scope");
            var center = new MyPlayableCenter(); var factory = new StatusMaterialFactory(center);
            var previousFactory = StoryPlayableInstantiation.Resolve; StoryPlayableInstantiation.Resolve = _ => factory;
            var manager = new MyPlayableManager { ScopeRoot = root, MyPlayableCenter = center,
                InstanceFactory = factory, LowMemory = new Zjcs.Official.StoryPlayables.GAction() };
            var properties = actor.AddComponent<MeshMaterialProperty>(); properties.SetAppearanceTarget(actor); properties.Init();
            var attachment = actor.AddComponent<AttachAgent>();
            attachment.AddBoneNode("body", actor.transform); attachment.AddBoneNode("body_bottom", actor.transform);
            var prefabs = Enumerable.Range(1, 2).Select(index => Resources.Load<GameObject>(
                "Combat/StatusFrozen/Prefabs/EcEntity/Fight/FightSkill/GlobalPlayable/playable_Frozen_" + index)).ToArray();
            Require(prefabs.All(prefab => prefab), "both complete native global frozen graph resources");
            var configs = prefabs.Select(prefab => prefab.GetComponentInChildren<MyPlayableMaterialProperty>(true)).ToArray();
            Require(configs.All(config => config && config.Keyword == "_FROZEN" && config.FloatPropertyName == "_BaseStrength"
                && config.Duration == -1 && config.NeedResume), "original global freeze material configuration");
            Require(configs[0].PropertyProviderList.Count == 0 && configs[1].PropertyProviderList.Count == 1
                && configs[1].PropertyProviderList[0] is PlayableMaterialBoundingBoxProvider,
                "preserve original unreferenced first provider and referenced second provider");
            var materials = Private<IEnumerable<Material>>(properties, "mMaterials").ToArray();
            Require(materials.Length > 0 && properties.HasFloat("_BaseStrength") && materials.All(m => m.HasProperty("_BoundingBox")),
                "actual native character materials support original frozen parameters");
            var defaults = materials.Select(m => m.GetFloat("_BaseStrength")).ToArray();
            var keywords = materials.Select(m => m.IsKeywordEnabled("_FROZEN")).ToArray();
            string label = "status-frozen-" + renderer.State.Source.Sex + "-" + animator.runtimeAnimatorController.name;
            float originalSpeed = animator.speed;
            void Draw() { renderer.LateUpdateMesh(); weaponRoot.GetComponent<SkeletonRendererFollower>().ForceUpdate(); weapon.UpdateAnimationAndMesh(); }
            Matrix4x4[] Pose() => Enumerable.Range(0, renderer.State.Pose.Count).Select(renderer.State.Pose.World).ToArray();
            IEnumerator Pump() { for (int i = 0; i < 4; i++) { yield return null; center.Update(.05f); center.LateUpdate(); } }
            void Frozen() => Require(animator.speed == 0 && materials.All(m => m.IsKeywordEnabled("_FROZEN")
                && Mathf.Approximately(m.GetFloat("_BaseStrength"), .5f)), "native pause and frozen material execute together");
            void Restored() => Require(Mathf.Approximately(animator.speed, .65f)
                && materials.Select(m => m.GetFloat("_BaseStrength")).SequenceEqual(defaults)
                && materials.Select(m => m.IsKeywordEnabled("_FROZEN")).SequenceEqual(keywords), "last graph restores speed, keyword and float defaults");
            try
            {
                manager.Initialize();
                animator.Play("Idle", 0, 0); animator.Update(0); animator.speed = .65f; animator.Update(.2f);
                sampler.ApplyUnmixed(renderer.State); Draw();
                var bounds = SceneCharacterBounds(renderer, weapon);
                CaptureSceneRendererCamera(bounds, label + "-visibility"); Draw();
                var baseline = CaptureSceneRendererCamera(bounds, label + "-before");
                var pose = Pose(); float clock = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                foreach (var prefab in prefabs)
                {
                    var graph = manager.Play(prefab, actor, actor); yield return Pump(); Frozen();
                    animator.Update(.4f); sampler.ApplyUnmixed(renderer.State); Draw();
                    Require(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime - clock) < .00001f
                        && Pose().SequenceEqual(pose), "global freeze holds actual Animator clock and original native bones");
                    if (prefab == prefabs[1]) Require(materials.All(m => m.GetVector("_BoundingBox") == new Vector4(1, 1, 0, 0)),
                        "appearance collector's original default bounds is passed to the second graph provider");
                    foreach (var particle in graph.GetComponentsInChildren<ParticleSystem>()) particle.Simulate(.25f, false, true, true);
                    Require(!baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-" + prefab.name)),
                        "complete original global freeze graph changes actual character GPU pixels");
                    graph.Stop(true); yield return Pump(); Restored();
                    animator.Play("Idle", 0, clock); animator.Update(0); sampler.ApplyUnmixed(renderer.State); Draw();
                }
                var first = manager.Play(prefabs[0], actor, actor);
                var second = manager.Play(prefabs[1], actor, actor); yield return Pump(); Frozen();
                Require(Private<int>(controller, "mPauseCounter") == 2, "different frozen graphs share original pause leases");
                first.Stop(true); yield return Pump(); Frozen();
                Require(Private<int>(controller, "mPauseCounter") == 1, "one frozen graph ending retains the other's pause");
                CaptureSceneRendererCamera(bounds, label + "-overlap");
                second.Stop(true); yield return Pump(); Restored();
                foreach (var prefab in prefabs)
                {
                    var replay = manager.Play(prefab, actor, actor); yield return Pump(); Frozen();
                    Require(ReferenceEquals(replay, prefab == prefabs[0] ? first : second), "both full graph pools reuse and reapply native nodes");
                    replay.Stop(true); yield return Pump(); Restored();
                }
                CheckFrozenRendererBounds(configs[1].PropertyProviderList[0], materials);
                manager.RemoveAllPlayable(true); center.RemoveAll(); Object.DestroyImmediate(root);
                // Compare the same actual Animator sample, after all native effects have returned.
                animator.Play("Idle", 0, clock); animator.Update(0); sampler.ApplyUnmixed(renderer.State); Draw();
                Require(baseline.SequenceEqual(CaptureSceneRendererCamera(bounds, label + "-restored")), "same-clock native character pixels exactly restore");
                animator.Update(.3f); sampler.ApplyUnmixed(renderer.State); Draw();
                Require(!Pose().SequenceEqual(pose), "Animator and native bones advance after both frozen effects end");
                CaptureSceneRendererCamera(bounds, label + "-resumed");
                Debug.Log("[Original global frozen] " + label + "; both complete graphs, material/pause overlap, independent pool replay, original bounds provider, exact same-clock GPU restore and resumed native motion passed.");
            }
            finally
            {
                controller.ForceResumeAllPause(); animator.speed = originalSpeed;
                manager.Dispose(); center.RemoveAll(); StoryPlayableInstantiation.Resolve = previousFactory;
                Object.DestroyImmediate(properties); Object.DestroyImmediate(attachment); if (root) Object.DestroyImmediate(root);
                animator.Play("Idle", 0, 0); animator.Update(0);
                string profession = animator.runtimeAnimatorController.name.Contains("Fashi") ? "200" : "100";
                ProfessionNativeConstraintPose.EvaluateBeforePhysics(renderer.State,
                    new ProfessionNativeAnimationChannels(renderer.State.Source, profession + "/idle_01"), .4f, false); Draw();
            }
        }

        private static void CheckFrozenRendererBounds(PlayableMaterialPropertyProvider provider, Material[] sources)
        {
            var go = new GameObject("Original bounds provider renderer branch");
            var materials = sources.Select(source => new Material(source)).ToArray();
            try
            {
                var meshRenderer = go.AddComponent<MeshRenderer>(); meshRenderer.sharedMaterials = materials;
                meshRenderer.localBounds = new Bounds(Vector3.zero, new Vector3(4, 7, 2));
                var properties = go.AddComponent<MeshMaterialProperty>(); properties.MeshRenderer = meshRenderer; properties.Init();
                provider.Play(properties);
                var expected = new Vector4(4, 7, 0, 0);
                Require(materials.All(m => m.GetVector("_BoundingBox") == expected), "original renderer local bounds size is converted from Vector2 to Vector4");
                meshRenderer.localBounds = new Bounds(Vector3.zero, new Vector3(8, 9, 2));
                provider.ProcessFrame(properties); provider.Resume(properties);
                Require(materials.All(m => m.GetVector("_BoundingBox") == expected), "original provider only samples on Play; inherited ProcessFrame and Resume leave the vector intact");
            }
            finally { Object.DestroyImmediate(go); foreach (var material in materials) Object.DestroyImmediate(material); }
        }
    }
}
