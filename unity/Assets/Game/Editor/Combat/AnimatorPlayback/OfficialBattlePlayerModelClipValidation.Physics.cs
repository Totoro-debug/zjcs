using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        [Serializable] private sealed class PhysicsCases { public PhysicsCase[] cases; }
        [Serializable] private sealed class PhysicsCase
        {
            public string sex, outgoing, incoming, constraint;
            public float from, to, weight, expected;
        }

        public static void RunMixedPhysics()
        {
            Run(); SessionState.SetBool(Key + ".physics", true);
        }

        private static void CheckMixedPhysics(ProfessionNativeSceneRenderer native,
            ProfessionNativeSceneWeaponRenderer weapon, string group)
        {
            var skeleton = native.State;
            var data = skeleton.Source;
            var fixture = JsonUtility.FromJson<PhysicsCases>(File.ReadAllText(Path.Combine(
                Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "mixed-physics-cases.json")));
            int samples = 0;
            var motions = new Dictionary<string, ProfessionNativePhysicsAnimation>();
            ProfessionNativePhysicsAnimation Motion(string name)
            {
                if (!motions.TryGetValue(name, out var value)) motions.Add(name, value = new ProfessionNativePhysicsAnimation(data, name));
                return value;
            }
            foreach (var row in fixture.cases.Where(c => c.sex == data.Sex))
            {
                var outgoing = Motion(row.outgoing);
                var incoming = Motion(row.incoming);
                var state = outgoing.Sample(row.constraint, row.from);
                incoming.ApplyReplace(new[] { state }, row.to, row.weight);
                Require(Mathf.Abs(state.Mix - row.expected) < .00015f,
                    "independent original physics mix expectation: " + row.incoming + "/" + row.constraint);
                samples++;
            }
            Require(samples > 100, "current original physics key/interval/missing-key cases");

            var idle = new ProfessionNativeAnimationChannels(data, group + "/idle_01", originalBezierSegments: true);
            var hit = new ProfessionNativeAnimationChannels(data, group + "/hit_01", originalBezierSegments: true);
            var first = new ProfessionNativeAppearanceSkeleton(data); first.SetSkin(skeleton.Skin);
            var second = new ProfessionNativeAppearanceSkeleton(data); second.SetSkin(skeleton.Skin);
            var a = new ProfessionNativePlayerPhysicsPose(data); var b = new ProfessionNativePlayerPhysicsPose(data);
            for (int frame = 0; frame < 60; frame++)
            {
                float time = frame * .016f;
                a.ApplyMixNext(first, idle, time, true, hit, time, false, 0, .016f);
                b.Apply(second, idle, time, true, .016f);
                Require(Enumerable.Range(0, first.Pose.Count).All(i => first.Pose.World(i) == second.Pose.World(i))
                    && a.PhysicsTime == b.PhysicsTime, "zero incoming weight preserves exact unmixed physics history");
            }
            float clock = a.PhysicsTime;
            var before = Enumerable.Range(0, first.Pose.Count).Select(first.Pose.World).ToArray();
            bool rejected = false;
            try { a.ApplyMixNext(first, idle, 0, true, hit, 0, false, float.NaN, .016f); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected && a.PhysicsTime == clock && before.SequenceEqual(
                Enumerable.Range(0, first.Pose.Count).Select(first.Pose.World)), "invalid mixed weight leaves pose and physical clock intact");
            rejected = false;
            try { a.ApplyMixNext(first, idle, 0, true, hit, 0, false, .5f, -.01f); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected && a.PhysicsTime == clock, "invalid physical delta rejects before advancing");
            for (int repeat = 0; repeat < 10; repeat++) a.Apply(first, idle, 59 * .016f, true, 0, updatePhysics: false);
            Require(a.PhysicsTime == clock && before.SequenceEqual(Enumerable.Range(0, first.Pose.Count).Select(first.Pose.World)),
                "explicit stored physical pose is stable across repeated redraws");
            a.ApplyMixNext(first, idle, .96f, true, hit, .96f, false, 0, .016f);
            b.Apply(second, idle, .96f, true, .016f);
            Require(a.PhysicsTime == b.PhysicsTime && Enumerable.Range(0, first.Pose.Count).All(i => first.Pose.World(i) == second.Pose.World(i)),
                "stored-pose redraw does not alter the next physical update history");

            var animator = native.GetComponent<Animator>();
            // The source prefab intentionally starts with an empty base
            // controller. This lower-level pose probe explicitly supplies the
            // verified Game controller; production appearance refresh is separate.
            var previousController = animator.runtimeAnimatorController;
            var gameController = Resources.Load<RuntimeAnimatorController>("Combat/GameAnimators/Anim/"
                + (data.Sex == "female" ? "Female_" : "Male_") + (group == "100" ? "Zhanshi" : "Fashi") + "_Controller");
            Require(gameController, "exact current original Game controller for physical pose probe");
            animator.runtimeAnimatorController = gameController;
            var physics = new ProfessionNativePlayerPhysicsPose(data);
            const float delta = .01f;
            float sampleDelta = delta;
            var sampler = new ProfessionNativeAnimatorPose(animator,
                (state, motion, time, loop) => physics.Apply(state, motion, time, loop, sampleDelta, updatePhysics: sampleDelta != 0),
                (state, mix) => physics.ApplyMixNext(state, mix.Outgoing.Animation, mix.Outgoing.Time, mix.Outgoing.Loop,
                    mix.Incoming.Animation, mix.Incoming.Time, mix.Incoming.Loop, mix.Weight, sampleDelta,
                    updatePhysics: sampleDelta != 0), originalBezierSegments: true);
            int updates = 0, mixed = 0, physicalChanges = 0;
            var prePhysics = new ProfessionNativeAppearanceSkeleton(data); prePhysics.SetSkin(skeleton.Skin);
            var bounds = native.GetComponent<MeshRenderer>().bounds; bounds.Encapsulate(weapon.GetComponent<MeshRenderer>().bounds); bounds.Expand(.5f);
            string label = data.Sex + "-" + group + "-mixed-physics";
            try
            {
                foreach (var parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.nameHash);
                animator.Rebind(); animator.Play("Idle", 0, 0); animator.Update(0);
                void Step()
                {
                    animator.Update(delta);
                    var applied = sampler.Apply(skeleton); updates++;
                    Require(Mathf.Abs(physics.PhysicsTime - updates * delta) < .0002f,
                        "one physical clock advance per actual Animator pose, including transition frames");
                    if (applied.Incoming.HasValue)
                    {
                        mixed++;
                        var to = applied.Incoming.Value;
                        ProfessionNativeConstraintPose.EvaluateMixNextBeforePhysics(prePhysics,
                            applied.Current.Animation, applied.Current.Time, applied.Current.Loop,
                            to.Animation, to.Time, to.Loop, applied.IncomingWeight);
                    }
                    else ProfessionNativeConstraintPose.EvaluateBeforePhysics(prePhysics,
                        applied.Current.Animation, applied.Current.Time, applied.Current.Loop);
                    if (Enumerable.Range(0, skeleton.Pose.Count).Any(i =>
                        (skeleton.Pose.World(i).GetColumn(3) - prePhysics.Pose.World(i).GetColumn(3)).sqrMagnitude > .00000001f
                        || Mathf.Abs(skeleton.Pose.World(i).m00 - prePhysics.Pose.World(i).m00) > .0001f)) physicalChanges++;
                    Require(Enumerable.Range(0, skeleton.Pose.Count).All(i => Enumerable.Range(0, 16).All(axis =>
                        float.IsFinite(skeleton.Pose.World(i)[axis]))), "finite completed body physics matrices");
                    native.LateUpdateMesh();
                }
                for (int i = 0; i < 60; i++) Step();
                var idlePixels = Capture(bounds, label + "-idle");
                animator.CrossFadeInFixedTime("Damage", .2f, 0, 0);
                for (int i = 0; i < 5; i++) Step();
                var early = Capture(bounds, label + "-early");
                for (int i = 0; i < 10; i++) Step();
                var late = Capture(bounds, label + "-late");
                Require(!early.SequenceEqual(late) && !idlePixels.SequenceEqual(early), "real blended physical body changes actual camera pixels");
                for (int i = 0; i < 60; i++) Step();
                animator.SetBool("die", true);
                for (int i = 0; i < 90; i++) Step();
                Require(animator.GetCurrentAnimatorClipInfo(0)[0].clip.name.Contains("die"), "normal death completes through the same physical history");
                Capture(bounds, label + "-death");
                animator.SetBool("die", false);
                for (int i = 0; i < 90; i++) Step();
                float previous = physics.PhysicsTime;
                animator.Play("Idle", 0, 0); animator.Update(0); Step();
                Require(physics.PhysicsTime > previous && mixed > 5 && physicalChanges > 5,
                    "motion replay preserves physical clock and actual constrained motion across mixed/death/revival frames");
                var repeated = Enumerable.Range(0, skeleton.Pose.Count).Select(skeleton.Pose.World).ToArray();
                previous = physics.PhysicsTime; sampleDelta = 0; sampler.Apply(skeleton);
                Require(physics.PhysicsTime == previous && repeated.SequenceEqual(
                    Enumerable.Range(0, skeleton.Pose.Count).Select(skeleton.Pose.World)), "explicit Physics.Pose does not accumulate simulation");
                Debug.Log("Original player mixed physics passed: " + label + ", " + samples + " independent scalar expectations, "
                    + updates + " completed poses, " + mixed + " transition frames and " + physicalChanges + " physical poses; no transition frame skipped.");
            }
            finally
            {
                animator.SetBool("die", false); animator.Play("Idle", 0, 0); animator.Update(0);
                ProfessionNativeConstraintPose.EvaluateBeforePhysics(skeleton, idle, .4f, false); native.LateUpdateMesh();
                animator.runtimeAnimatorController = previousController; animator.Update(0);
            }
        }
    }
}
