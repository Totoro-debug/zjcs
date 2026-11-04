using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattlePlayerModelClipValidation
    {
        // Public API numerical oracle only. No SDK implementation is inspected,
        // copied, or executed by the independent production follower.
        private sealed class BoneFollowerOracle : IDisposable
        {
            private readonly GameObject root, target;
            private readonly TextAsset json;
            private readonly SkeletonDataAsset data;
            internal readonly SkeletonRenderer Renderer;
            internal readonly BoneFollower Follower;
            internal Spine.Bone Bone => Renderer.Skeleton.FindBone("target");

            internal BoneFollowerOracle()
            {
                json = new TextAsset("{\"skeleton\":{\"spine\":\"4.2.0\",\"hash\":\"numerical-oracle\"},\"bones\":[{\"name\":\"root\"},{\"name\":\"parent\",\"parent\":\"root\"},{\"name\":\"target\",\"parent\":\"parent\"}]}");
                data = SkeletonDataAsset.CreateRuntimeInstance(json, new AtlasAssetBase[0], true, 1f);
                root = new GameObject("Public API numerical oracle"); root.SetActive(false);
                Renderer = root.AddComponent<SkeletonRenderer>(); Renderer.skeletonDataAsset = data;
                Renderer.Initialize(false, false);
                target = new GameObject("Original direct-child follower"); target.transform.SetParent(root.transform, false);
                Follower = target.AddComponent<BoneFollower>(); Follower.skeletonRenderer = Renderer;
                Follower.boneName = "target";
                Follower.followXYPosition = Follower.followZPosition = Follower.followBoneRotation = true;
                Follower.followSkeletonFlip = Follower.followLocalScale = Follower.followParentWorldScale = true;
                Follower.maintainedAxisOrientation = (BoneFollower.AxisOrientation)1;
                Follower.Initialize();
            }
            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(data); UnityEngine.Object.DestroyImmediate(json);
            }
        }

        private sealed class FollowerBoneSnapshot : IProfessionFollowerBone
        {
            internal Spine.Bone Source;
            public bool Active => Source.Active;
            public float WorldX => Source.WorldX;
            public float WorldY => Source.WorldY;
            public float WorldRotationX => Source.WorldRotationX;
            public float ScaleX => Source.ScaleX;
            public float ScaleY => Source.ScaleY;
            public float A => Source.A;
            public float B => Source.B;
            public float C => Source.C;
            public float D => Source.D;
        }

        private static Matrix4x4 FollowerParentMatrix(Spine.Bone bone)
        {
            var matrix = Matrix4x4.identity;
            matrix.m00 = bone.Parent.A; matrix.m01 = bone.Parent.B;
            matrix.m10 = bone.Parent.C; matrix.m11 = bone.Parent.D;
            return matrix;
        }

        private static void RequireFollowerPose(Transform actual, Transform expected, string label)
        {
            Require(Vector3.Distance(actual.localPosition, expected.localPosition) < .00002f
                && Quaternion.Angle(actual.localRotation, expected.localRotation) < .04f
                && Vector3.Distance(actual.localScale, expected.localScale) < .00002f,
                "public API bone follower local pose agrees: " + label + "; actual=" + actual.localPosition + "/" + actual.localEulerAngles + "/" + actual.localScale
                + "; expected=" + expected.localPosition + "/" + expected.localEulerAngles + "/" + expected.localScale);
        }

        private static void CheckPlayerBoneFollowerGeometry()
        {
            using (var oracle = new BoneFollowerOracle())
            {
                var target = new GameObject("Independent follower geometry");
                target.transform.SetParent(oracle.Renderer.transform, false);
                try
                {
                    var bone = oracle.Bone;
                    for (int sample = 0; sample < 128; sample++)
                    {
                        bone.Parent.ScaleX = (sample % 2 == 0 ? 1 : -1) * (1.4f + sample * .001f);
                        bone.Parent.ScaleY = ((sample & 2) == 0 ? 1 : -1) * .6f;
                        bone.Parent.Rotation = sample * 13;
                        bone.Parent.ShearY = sample % 3 * 8;
                        bone.ScaleX = ((sample & 4) == 0 ? 1 : -1) * 1.2f;
                        bone.ScaleY = ((sample & 8) == 0 ? 1 : -1) * .8f;
                        bone.Rotation = sample * 7 - 190; bone.X = 2; bone.Y = 3;
                        oracle.Renderer.transform.localScale = new Vector3((sample & 16) == 0 ? .3f : -.3f, .4f, 1);
                        oracle.Renderer.transform.SetPositionAndRotation(new Vector3(4, -2, .8f), Quaternion.Euler(0, 0, sample));
                        oracle.Renderer.Skeleton.UpdateWorldTransform(Skeleton.Physics.None);
                        oracle.Follower.LateUpdate();
                        ProfessionNativePlayerBoneFollower.ApplyPose(target.transform,
                            new FollowerBoneSnapshot { Source = bone }, FollowerParentMatrix(bone));
                        RequireFollowerPose(target.transform, oracle.Follower.transform, "synthetic signed/nonuniform/sheared " + sample);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(target); }
            }
            Debug.Log("Original 3100 bone follower public API oracle passed: 128 signed/nonuniform/sheared parent and local poses; direct-child axis orientation 1.");
        }

        private static void CheckPlayerBoneFollowers(ProfessionNativeBattlePlayerActor actor)
        {
            Require(actor.Attachments.BoneFollowers.Count == 2, "both original 3100 bone followers bound");
            float time = actor.Renderer.PlayerPhysicsTime;
            using (var oracle = new BoneFollowerOracle())
            foreach (var follower in actor.Attachments.BoneFollowers)
            {
                Require(follower.enabled && follower.Renderer == actor.Renderer && follower.transform.parent == actor.Renderer.transform,
                    "original enabled follower and exact renderer/parent");
                Require(follower.BoneName == (follower.name == "sWeapon_01_bone" ? "sWeapon_02" : "pWeapon_R2"),
                    "original follower bone binding preserves differently named GameObject");
                if (!follower.gameObject.activeInHierarchy)
                {
                    var original = (Dictionary<string, object>)Zjcs.Offline.Equipment.OfficialEquipmentJson.Parse(Resources.Load<TextAsset>("Combat/player-attachments").text);
                    var rows = (List<object>)original["nodes"];
                    var row = rows.Find(value => (string)((Dictionary<string, object>)value)["name"] == follower.name);
                    var fields = (Dictionary<string, object>)((Dictionary<string, object>)row)["fields"];
                    var position = (Dictionary<string, object>)fields["m_LocalPosition"];
                    var scale = (Dictionary<string, object>)fields["m_LocalScale"];
                    var rotation = (Dictionary<string, object>)fields["m_LocalRotation"];
                    Require(Vector3.Distance(follower.transform.localPosition, new Vector3(Convert.ToSingle(position["x"]), Convert.ToSingle(position["y"]), Convert.ToSingle(position["z"]))) < .000001f
                        && Vector3.Distance(follower.transform.localScale, new Vector3(Convert.ToSingle(scale["x"]), Convert.ToSingle(scale["y"]), Convert.ToSingle(scale["z"]))) < .000001f
                        && Quaternion.Angle(follower.transform.localRotation, new Quaternion(Convert.ToSingle(rotation["x"]), Convert.ToSingle(rotation["y"]), Convert.ToSingle(rotation["z"]), Convert.ToSingle(rotation["w"]))) < .04f,
                        "original inactive follower retains serialized pose until activation");
                    continue;
                }
                var state = actor.Renderer.State; var source = state.FindBone(follower.BoneName);
                int index = state.Source.FindBone(follower.BoneName), parent = state.Pose.Parent(index);
                var matrix = parent < 0 ? Matrix4x4.identity : state.Pose.World(parent);
                var bone = oracle.Bone;
                bone.WorldX = source.WorldX; bone.WorldY = source.WorldY;
                bone.A = source.A; bone.B = source.B; bone.C = source.C; bone.D = source.D;
                bone.ScaleX = source.ScaleX; bone.ScaleY = source.ScaleY;
                bone.Parent.A = matrix.m00; bone.Parent.B = matrix.m01;
                bone.Parent.C = matrix.m10; bone.Parent.D = matrix.m11;
                oracle.Follower.LateUpdate();
                RequireFollowerPose(follower.transform, oracle.Follower.transform, actor.Root.name + "/" + follower.name);
                follower.ApplyCurrentPose();
                RequireFollowerPose(follower.transform, oracle.Follower.transform, "repeat zero-time refresh");
            }
            Require(actor.Renderer.PlayerPhysicsTime == time, "bone follower comparison and repeat redraw do not advance physics");
        }
    }
}
