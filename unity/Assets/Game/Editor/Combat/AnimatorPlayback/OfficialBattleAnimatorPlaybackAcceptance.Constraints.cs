using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        [Serializable] private sealed class ConstraintCases { public ConstraintCase[] cases; public AnimationCount[] counts; }
        [Serializable] private sealed class AnimationCount { public string sex; public int animations; }
        [Serializable] private sealed class ConstraintCase
        {
            public string sex, animation, kind, constraint;
            public float time;
            public float[] values;
            public bool bend, stretch, compress;
        }

        public static void RunConstraintChannels()
        {
            Run();
            SessionState.SetBool(Key + ".constraints", true);
        }

        private static IEnumerator ConstraintChannelSteps()
        {
            string folder = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            var fixture = JsonUtility.FromJson<ConstraintCases>(File.ReadAllText(Path.Combine(folder, "constraint-cases.json")));
            Require(fixture != null && fixture.cases.Length > 6000, "original constraint key fixture");
            foreach (var body in fixture.counts)
            {
                var data = ProfessionNativeSkeletonData.LoadResource(body.sex);
                var names = (IReadOnlyDictionary<string, object>)data.SourceFields["animations"];
                Require(names.Count == body.animations, "original full animation count: " + body.sex);
                var selected = Enumerable.Range(0, data.Constraints.Count).ToArray();
                var bound = names.Keys.ToDictionary(name => name, name => new ProfessionNativeConstraintAnimation(data, name));
                int checkedKeys = 0;
                foreach (var row in fixture.cases.Where(row => row.sex == body.sex))
                {
                    var animation = bound[row.animation];
                    float[] actual;
                    if (row.kind == "ik")
                    {
                        var state = animation.SampleIk(row.time, selected).Single(state => state.Name == row.constraint);
                        Require(state.BendPositive == row.bend && state.Stretch == row.stretch && state.Compress == row.compress,
                            "original discrete constraint flags: " + row.animation + "/" + row.constraint);
                        actual = new[] { state.Mix, state.Softness };
                    }
                    else
                    {
                        var state = animation.SampleTransforms(row.time, selected).Single(state => state.Name == row.constraint);
                        actual = new[] { state.Rotation, state.X, state.Y, state.ScaleX, state.ScaleY, state.Shear };
                    }
                    Require(actual.Length == row.values.Length, "constraint axis count");
                    for (int axis = 0; axis < actual.Length; axis++)
                        Require(Mathf.Abs(actual[axis] - row.values[axis]) < .00002f,
                            "original constraint key: " + body.sex + "/" + row.animation + "/" + row.constraint + " at " + row.time + " axis " + axis);
                    checkedKeys++;
                }
                var skill = bound["100/skill_06"];
                Require(Mathf.Abs(skill.SampleIk(.4f, selected).Single(state => state.Name == "IK_tuiL_2").Mix - 1) < .00001f,
                    "original skill stepped IK remains enabled before boundary");
                Require(skill.SampleIk(.7f, selected).Single(state => state.Name == "IK_tuiL_2").Mix == 0,
                    "original skill disables IK during movement");
                Require(Mathf.Abs(skill.SampleIk(1.1f, selected).Single(state => state.Name == "IK_tuiL_2").Mix - .5f) < .00001f,
                    "original skill interpolates IK restoration");
                var empty = skill.SampleTransforms(0, Array.Empty<int>());
                Require(empty.Length == data.Constraints.Count(definition => definition.Kind == "transform"
                    && !(definition.Fields.TryGetValue("skin", out object value) && (bool)value)), "skin-required constraints stay excluded");
                Require(skill.SampleIk(0, selected).Select(state => state.Order).SequenceEqual(
                    skill.SampleIk(0, selected).Select(state => state.Order).OrderBy(order => order)), "original source constraint order");
                bool rejected = false;
                try { skill.SampleIk(float.NaN, selected); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "invalid sample time rejected");
                rejected = false;
                try { skill.SampleTransforms(0, new[] { data.Constraints.Count }); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "foreign skin constraint rejected");
                // The existing animation channel entry exposes the same bound sampler.
                var channels = new ProfessionNativeAnimationChannels(data, "100/skill_06");
                Require(channels.Constraints.Name == "100/skill_06" && ReferenceEquals(channels.Constraints, channels.Constraints), "shared native channel binding");
                Debug.Log("[Original constraint channels] " + body.sex + ": " + bound.Count + " original animations, " + checkedKeys + " original key/interval samples.");
                yield return null;
            }
        }
    }
}
