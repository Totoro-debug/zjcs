using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Spine;
using Spine.Unity;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleFlippedBoneFollowerAcceptance
    {
        private sealed class BoneView : IProfessionFollowerBone
        {
            internal Bone Value;
            public bool Active => Value.Active;
            public float WorldX => Value.WorldX;
            public float WorldY => Value.WorldY;
            public float WorldRotationX => Value.WorldRotationX;
            public float ScaleX => Value.ScaleX;
            public float ScaleY => Value.ScaleY;
            public float A => Value.A;
            public float B => Value.B;
            public float C => Value.C;
            public float D => Value.D;
        }
        public static void Run()
        {
            int count = 0;
            var apply = typeof(PartnerNativeBattleAttachments).GetMethod("ApplyFlippedBoneFollower", BindingFlags.Static | BindingFlags.NonPublic);
            var json = new TextAsset("{\"skeleton\":{\"spine\":\"4.2.0\",\"hash\":\"numerical-oracle\"},\"bones\":[{\"name\":\"root\"},{\"name\":\"target\",\"parent\":\"root\",\"x\":2,\"y\":3,\"rotation\":37}]}");
            var data = SkeletonDataAsset.CreateRuntimeInstance(json, new AtlasAssetBase[0], true, 1f);
            try
            {
                foreach (bool direct in new[] { false, true })
                foreach (int sx in new[] { -1, 1 })
                foreach (int sy in new[] { -1, 1 })
                foreach (int px in new[] { -1, 1 })
                foreach (int py in new[] { -1, 1 })
                foreach (bool flip in new[] { false, true })
                foreach (bool local in new[] { false, true })
                foreach (bool rotate in new[] { false, true })
                {
                    var root = new GameObject("Original BoneFollower oracle"); root.SetActive(false);
                    var parent = new GameObject("Parent");
                    var actual = new GameObject("Migrated");
                    try
                    {
                        root.transform.localScale = new Vector3(sx * 1.3f, sy * .7f, 1);
                        root.transform.rotation = Quaternion.Euler(0, 0, 19);
                        parent.transform.localScale = new Vector3(px * .8f, py * 1.2f, 1);
                        parent.transform.rotation = Quaternion.Euler(0, 0, -23);
                        var renderer = root.AddComponent<SkeletonRenderer>(); renderer.skeletonDataAsset = data; renderer.Initialize(false, false);
                        renderer.Skeleton.ScaleX = flip ? -1 : 1;
                        var bone = renderer.Skeleton.FindBone("target"); bone.ScaleX = -1.1f; bone.ScaleY = .9f;
                        renderer.Skeleton.UpdateWorldTransform(Skeleton.Physics.None);
                        var oracleObject = new GameObject("Original"); oracleObject.transform.SetParent(direct ? root.transform : parent.transform, false);
                        actual.transform.SetParent(oracleObject.transform.parent, false);
                        var follower = oracleObject.AddComponent<BoneFollower>(); follower.skeletonRenderer = renderer; follower.boneName = "target";
                        follower.followXYPosition = follower.followZPosition = true;
                        follower.followBoneRotation = rotate; follower.followLocalScale = local;
                        follower.followSkeletonFlip = true; follower.followParentWorldScale = false;
                        follower.maintainedAxisOrientation = (BoneFollower.AxisOrientation)1;
                        follower.Initialize(); follower.LateUpdate();
                        var fields = new Dictionary<string, object> { { "followXYPosition", true }, { "followZPosition", true },
                            { "followBoneRotation", rotate }, { "followLocalScale", local } };
                        apply.Invoke(null, new object[] { actual.transform, root.transform, new BoneView { Value = bone }, fields, flip ? -1f : 1f });
                        if (Vector3.Distance(actual.transform.position, oracleObject.transform.position) > .0001f
                            || Quaternion.Angle(actual.transform.rotation, oracleObject.transform.rotation) > .05f
                            || Vector3.Distance(actual.transform.localScale, oracleObject.transform.localScale) > .0001f)
                            throw new InvalidOperationException("Original BoneFollower differs at case " + count);
                        count++;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(actual); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(parent); }
                }
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "flipped-bone-result.json"), "{\"passed\":true,\"cases\":" + count + "}");
                Debug.Log("Original BoneFollower reflection and parent geometry passed: " + count);
            }
            finally { UnityEngine.Object.DestroyImmediate(data); UnityEngine.Object.DestroyImmediate(json); }
        }
    }
}
