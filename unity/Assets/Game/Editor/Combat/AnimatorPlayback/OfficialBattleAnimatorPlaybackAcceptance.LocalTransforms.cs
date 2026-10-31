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
        public static void RunLocalTransforms()
        {
            Run();
            SessionState.SetBool(Key + ".localTransforms", true);
        }

        private static IEnumerator LocalTransformSteps()
        {
            CheckLocalCoordinateFrames();
            string folder = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            CheckOriginalClothingTextures(folder);
            foreach (string sex in new[] { "male", "female" })
            {
                var data = ProfessionNativeSkeletonData.LoadResource(sex);
                foreach (string name in new[] { "body_0_G_014", "body_0_G_012" })
                {
                    var skeleton = new ProfessionNativeAppearanceSkeleton(data);
                    var combined = skeleton.CreateSkin("original local transform clothing fixture");
                    combined.AddSkin((ProfessionNativeSkin)data.SkinCatalog.FindSkin("default"));
                    combined.AddSkin((ProfessionNativeSkin)data.SkinCatalog.FindSkin(name));
                    skeleton.SetSkin(combined);
                    int samples = 0;
                    foreach (string motion in new[] { "100/skill_06", "200/skill_06", "100/skill_50", "100/skill_42_XX" })
                    {
                        var channels = new ProfessionNativeAnimationChannels(data, motion);
                        foreach (float time in new[] { 0f, .4f, .7f, 1.1f })
                        {
                            ProfessionNativeConstraintPose.EvaluateBeforePhysics(skeleton, channels, time, false);
                            var locals = channels.Constraints.SampleTransforms(time, skeleton.SkinConstraints).Where(value => value.Local).ToArray();
                            Require(locals.Length == 1, "one original local constraint belongs to this clothing skin");
                            var state = locals[0];
                            Require(state.Rotation == 1 && state.X == 1 && state.Y == 1 && !state.Relative,
                                "original full local rotation and XY constraint");
                            Vector2 translation = skeleton.Pose.LocalPosition(state.Target) + state.OffsetPosition;
                            float rotation = skeleton.Pose.LocalRotation(state.Target) + state.OffsetRotation;
                            foreach (int bone in state.Bones)
                            {
                                Require(skeleton.Pose.LocalScale(bone) == Vector2.one && skeleton.Pose.Shear(bone) == Vector2.zero,
                                    "original constrained clothing bone layout");
                                var expected = Matrix4x4.TRS(translation, Quaternion.Euler(0, 0, rotation), Vector3.one);
                                int parent = skeleton.Pose.Parent(bone);
                                if (parent >= 0) expected = skeleton.Pose.World(parent) * expected;
                                CheckMatrix(skeleton.Pose.World(bone), expected, "original local constraint expressed in the clothing parent's frame");
                            }
                            samples++;
                            if (motion == "100/skill_06" && time == .4f)
                                using (var batches = new ProfessionNativeMeshBatches(data.SlotCount))
                                {
                                    batches.Build(skeleton.Pose, skeleton.CreateDrawSlots());
                                    CaptureDeformGeometry(folder, sex + "-" + name, sex, batches);
                                }
                        }
                    }
                    Debug.Log("[Original local transform] " + sex + "/" + name + ": " + samples + " original skill samples, local rotation/XY, parent coordinates and native clothing geometry verified before physics.");
                }
                yield return null;
            }
            var regression = ConstraintPoseSteps();
            while (regression.MoveNext()) yield return regression.Current;
        }

        private static void CheckLocalCoordinateFrames()
        {
            var pose = new ProfessionNativeBonePose(new[] {
                new ProfessionNativeBonePose.Bone { name = "root", rotation = 30, scaleX = 2 },
                new ProfessionNativeBonePose.Bone { name = "other parent", parent = 0, x = 100, y = 200, rotation = 70 },
                new ProfessionNativeBonePose.Bone { name = "target", parent = 1, x = 2, y = 3, rotation = 100 },
                new ProfessionNativeBonePose.Bone { name = "clothing", parent = 0, x = 20, y = -10, rotation = -40 }
            });
            var state = new ProfessionNativeIdleTransform.Constraint { Name = "independent coordinate fixture", Order = 0,
                Bones = new[] { 3 }, Target = 2, Local = true, OffsetPosition = new Vector2(5, -2), OffsetRotation = 8,
                Rotation = .5f, X = .25f, Y = .75f };
            ProfessionNativeIdleTransform.Apply(pose, new[] { state });
            CheckMatrix(pose.World(3), pose.World(0) * Matrix4x4.TRS(new Vector3(16.75f, -1.75f, 0),
                Quaternion.Euler(0, 0, 34), Vector3.one), "independent partial mixes use local target coordinates");
            Require(pose.HasWorldOverride(3), "local constraint marks its applied world matrix");
            bool rejected = false;
            try { ProfessionNativeIdleTransform.Apply(pose, new[] { state }); } catch (NotSupportedException) { rejected = true; }
            Require(rejected, "already constrained local input requires applied-pose decomposition");
            pose.Reset(); Require(!pose.HasWorldOverride(3), "setup reset clears world override state");
            pose.OverrideWorld(2, pose.World(2)); rejected = false;
            try { ProfessionNativeIdleTransform.Apply(pose, new[] { state }); } catch (NotSupportedException) { rejected = true; }
            Require(rejected, "world-constrained target is not substituted with stale animated local values");
            pose.SetRotation(0, 40); Require(!pose.HasWorldOverride(2), "local rotation invalidation clears discarded overrides");
            pose.OverrideWorld(3, pose.World(3)); pose.OverrideWorld(0, pose.World(0));
            Require(!pose.HasWorldOverride(3), "ancestor world recomputation discards descendant overrides");
        }

        private static void CheckMatrix(Matrix4x4 actual, Matrix4x4 expected, string label)
        {
            for (int component = 0; component < 16; component++)
                Require(Mathf.Abs(actual[component] - expected[component]) < .002f, label + " component " + component);
        }
    }
}
