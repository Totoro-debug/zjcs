using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        [Serializable] private sealed class PhysicsCases { public PhysicsBody[] bodies; public PhysicsSetup[] setups; public PhysicsSkin[] skins; public PhysicsCase[] cases; }
        [Serializable] private sealed class PhysicsBody { public string sex, setupAnimation; public int animations, constraints, tracks; }
        [Serializable] private sealed class PhysicsSetup { public string sex, constraint; public int index, order, bone; public bool skin; public float[] values; public bool[] globals; }
        [Serializable] private sealed class PhysicsSkin { public string sex, name; public int[] indices; }
        [Serializable] private sealed class PhysicsCase { public string sex, animation, constraint; public float time, value; }

        public static void RunPhysicsChannels()
        {
            Run();
            SessionState.SetBool(Key + ".physicsChannels", true);
        }

        private static IEnumerator PhysicsChannelSteps()
        {
            var folder = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            var fixture = JsonUtility.FromJson<PhysicsCases>(File.ReadAllText(Path.Combine(folder, "physics-cases.json")));
            Require(fixture != null && fixture.setups.Length == 3301 && fixture.cases.Length > 3000, "original physics fixture");
            foreach (var body in fixture.bodies)
            {
                var data = ProfessionNativeSkeletonData.LoadResource(body.sex);
                var names = (System.Collections.Generic.IReadOnlyDictionary<string, object>)data.SourceFields["animations"];
                var bound = names.Keys.ToDictionary(name => name, name => new ProfessionNativePhysicsAnimation(data, name));
                Require(bound.Count == body.animations && bound.Values.Sum(animation => animation.TrackCount) == body.tracks, "all original physics timelines bind");
                var setup = bound[body.setupAnimation];
                int setupCount = 0, skinCount = 0, caseCount = 0;
                foreach (var row in fixture.setups.Where(row => row.sex == body.sex))
                {
                    var state = setup.Sample(row.constraint, 0);
                    Require(state.ConstraintIndex == row.index && state.Order == row.order && state.Bone == row.bone && state.SkinRequired == row.skin,
                        "original physics index/order/bone/skin: " + row.constraint);
                    Require(ReferenceEquals(state.OriginalFields, data.Constraints[row.index].Fields), "original setup field identity");
                    float[] values = { state.X, state.Y, state.Rotate, state.ScaleX, state.ShearX, state.Limit, state.Step, state.Inertia,
                        state.Strength, state.Damping, state.MassInverse, state.Wind, state.Gravity, state.Mix };
                    bool[] globals = { state.InertiaGlobal, state.StrengthGlobal, state.DampingGlobal, state.MassGlobal, state.WindGlobal, state.GravityGlobal, state.MixGlobal };
                    Require(values.Length == row.values.Length && globals.SequenceEqual(row.globals), "original setup parameter layout and global flags");
                    for (int axis = 0; axis < values.Length; axis++) Require(Mathf.Abs(values[axis] - row.values[axis]) < .00002f, "original setup axis " + axis + ": " + row.constraint);
                    setupCount++;
                }
                Require(setupCount == body.constraints, "complete original physics setup count");
                foreach (var row in fixture.skins.Where(row => row.sex == body.sex))
                {
                    var skin = (ProfessionNativeSkin)data.SkinCatalog.FindSkin(row.name);
                    Require(skin != null, "original physics skin exists");
                    var states = setup.Sample(0, skin.Constraints);
                    Require(states.Select(state => state.ConstraintIndex).OrderBy(index => index).SequenceEqual(row.indices), "original physics skin memberships: " + row.name);
                    Require(states.Select(state => state.Order).SequenceEqual(states.Select(state => state.Order).OrderBy(order => order)), "original physics execution order");
                    skinCount++;
                }
                foreach (var row in fixture.cases.Where(row => row.sex == body.sex))
                {
                    Require(Mathf.Abs(bound[row.animation].Sample(row.constraint, row.time).Mix - row.value) < .00002f,
                        "original physics mix: " + body.sex + "/" + row.animation + "/" + row.constraint + " at " + row.time);
                    caseCount++;
                }
                var skill = bound["100/skill_06"];
                const string hair = "hair_F_01_01a";
                Require(skill.Sample(hair, 0).Mix == 1 && skill.Sample(hair, .3333333432674408f).Mix == 0
                    && skill.Sample(hair, .7f).Mix == 0 && skill.Sample(hair, 1.2000000476837158f).Mix == 1,
                    "original skill physical mix switches off at its own boundary and restores");
                var snapshot = skill.Sample(hair, .7f); snapshot.Mix = 99;
                Require(skill.Sample(hair, .7f).Mix == 0 && skill.Sample(hair, 0).Mix == 1 && setup.Sample(hair, 0).Mix == 1,
                    "sampling/rewinding/animation changes do not retain modified snapshots");
                Require(skill.Sample(0, Array.Empty<int>()).Length == 0, "all original physics constraints require their selected skins");
                bool rejected = false;
                try { skill.Sample(hair, float.NaN); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "invalid physics time rejected");
                rejected = false;
                try { skill.Sample(0, new[] { data.Constraints.Count }); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "foreign physics membership rejected");
                rejected = false;
                try { skill.Sample("missing original constraint", 0); } catch (ArgumentException) { rejected = true; }
                Require(rejected, "missing source physics constraint rejected");
                var channels = new ProfessionNativeAnimationChannels(data, "100/skill_06");
                Require(channels.Physics.Name == channels.Name && ReferenceEquals(channels.Physics, channels.Physics), "shared native physics channel binding");
                Debug.Log("[Original physics channels] " + body.sex + ": " + bound.Count + " animations, " + setupCount + " setups, " + skinCount + " skins, " + caseCount + " mix samples.");
                yield return null;
            }
        }
    }
}
