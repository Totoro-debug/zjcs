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
        public static void RunConstraintPose()
        {
            Run();
            SessionState.SetBool(Key + ".constraintPose", true);
        }

        private static IEnumerator ConstraintPoseSteps()
        {
            string folder = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            foreach (string sex in new[] { "male", "female" })
            {
                var data = ProfessionNativeSkeletonData.LoadResource(sex);
                var skeleton = new ProfessionNativeAppearanceSkeleton(data);
                var skin = skeleton.CreateSkin("original body constraint geometry");
                skin.AddSkin((ProfessionNativeSkin)data.SkinCatalog.FindSkin("default"));
                skin.AddSkin((ProfessionNativeSkin)data.SkinCatalog.FindSkin(sex == "male" ? "body_100_M_01" : "body_100_F_01"));
                skeleton.SetSkin(skin);
                foreach (string name in new[] { "100/skill_06", "200/skill_06", "100/skill_50", "100/skill_42_XX" })
                {
                    var channels = new ProfessionNativeAnimationChannels(data, name);
                    foreach (float time in new[] { 0f, .4f, .7f, 1.1f })
                    {
                        skeleton.EvaluateNativeAnimation(channels, time, false);
                        int leg = data.FindBone("tuiL_X35,-9_Y115");
                        float localLeg = skeleton.Pose.LocalRotation(leg);
                        ProfessionNativeConstraintPose.EvaluateBeforePhysics(skeleton, channels, time, false);
                        var ik = channels.Constraints.SampleIk(time, skeleton.SkinConstraints);
                        foreach (var constraint in ik.Where(value => value.Mix == 1 && value.Bones.Length == 2))
                            CheckSolvedPair(skeleton.Pose, constraint);
                        if (name == "100/skill_06" && time == .7f)
                        {
                            Require(ik.Single(value => value.Name == "IK_tuiL_2").Mix == 0, "original disabled leg IK");
                            Require(skeleton.Pose.LocalRotation(leg) == localLeg, "disabled IK preserves animated local leg rotation");
                        }
                        var matrices = Enumerable.Range(0, data.BoneCount).Select(skeleton.Pose.World).ToArray();
                        ProfessionNativeConstraintPose.EvaluateBeforePhysics(skeleton, channels, time, false);
                        Require(matrices.Select((matrix, index) => matrix == skeleton.Pose.World(index)).All(value => value),
                            "re-evaluation starts from original local pose without accumulating constraints");
                        if (name == "100/skill_06" && time == .4f)
                            using (var batches = new ProfessionNativeMeshBatches(data.SlotCount))
                            {
                                batches.Build(skeleton.Pose, skeleton.CreateDrawSlots());
                                CaptureDeformGeometry(folder, sex + "-skill-06-constraints", sex, batches);
                            }
                        Debug.Log("[Original constraint pose] " + sex + "/" + name + " at " + time + ": source IK, transform order, endpoint geometry and reset verified before physics.");
                    }
                    float elapsed = channels.Duration + .413f;
                    float normalized = elapsed % channels.Duration;
                    ProfessionNativeConstraintPose.EvaluateBeforePhysics(skeleton, channels, normalized, false);
                    var first = Enumerable.Range(0, data.BoneCount).Select(skeleton.Pose.World).ToArray();
                    ProfessionNativeConstraintPose.EvaluateBeforePhysics(skeleton, channels, elapsed, true);
                    for (int index = 0; index < first.Length; index++)
                    {
                        var next = skeleton.Pose.World(index);
                        for (int component = 0; component < 16; component++)
                            Require(next[component] == first[index][component], "same normalized loop time drives bones and constraints");
                    }
                }
                bool rejected = false;
                try { ProfessionNativeConstraintPose.EvaluateBeforePhysics(skeleton,
                    new ProfessionNativeAnimationChannels(data, "100/skill_06"), float.NaN); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "nonfinite constraint pose time rejected");
                yield return null;
            }
        }

        private static void CheckSolvedPair(ProfessionNativeBonePose pose, ProfessionNativeConstraintAnimation.IkState state)
        {
            int root = state.Bones[0], child = state.Bones[1];
            var parent = pose.Parent(root) < 0 ? Matrix4x4.identity : pose.World(pose.Parent(root));
            var targetWorld = pose.World(state.Target);
            Vector2 target = parent.inverse.MultiplyPoint3x4(new Vector3(targetWorld.m03, targetWorld.m13, 0));
            target -= pose.LocalPosition(root);
            float first = pose.LocalPosition(child).magnitude;
            float distance = Mathf.Clamp(target.magnitude, Mathf.Abs(first - state.ChildLength), first + state.ChildLength);
            Vector2 expected = target.normalized * distance;
            Vector3 endWorld = pose.World(child).MultiplyPoint3x4(new Vector3(state.ChildLength, 0, 0));
            Vector2 actual = (Vector2)parent.inverse.MultiplyPoint3x4(endWorld) - pose.LocalPosition(root);
            Require(Vector2.Distance(actual, expected) < .002f, "original two-bone endpoint reaches its target or geometric reach boundary: " + state.Name);
        }
    }
}
