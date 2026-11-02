using System;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        // The same original follower used by integrated scene weapon prefabs,
        // bound directly to the production drawing endpoint, without a fake
        // follower renderer or fabricated custom slot depth.
        private sealed class SceneRendererFollowerProbe : IDisposable
        {
            private readonly ProfessionNativeSceneRenderer renderer;
            private readonly IProfessionFollowerRenderer target;
            private readonly SkeletonRendererFollower child, external;
            private readonly GameObject visibility;
            private IProfessionFollowerBone previousBone;
            private int rebuilds;

            internal SceneRendererFollowerProbe(ProfessionNativeSceneRenderer renderer, string group)
            {
                this.renderer = renderer; target = renderer;
                Require(ReferenceEquals(target.Skeleton, renderer.State) && target.ZSpacing == 0
                    && target.UpdateWhenInvisible == 0, "source follower skeleton, zero spacing and invisible mode");
                var catalog = Resources.Load<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
                var hierarchy = JsonUtility.FromJson<SceneHierarchy>(catalog.Find("pWeapon_" + group + "_G_007").Hierarchy.text);
                var fields = hierarchy.nodes[0].components.Single(row => row.type == "SkeletonRendererFollower").runtime_fields;
                Require(fields.Gos.Length == 1 && fields.InitializeOnAwake && fields.FollowZSpacing,
                    "actual integrated weapon follower source binding");
                var go = new GameObject("Original integrated weapon follower contract"); go.SetActive(false);
                go.transform.SetParent(renderer.transform, false);
                visibility = new GameObject("Original attachment visibility target"); visibility.transform.SetParent(go.transform, false);
                child = go.AddComponent<SkeletonRendererFollower>(); child.SkeletonRenderer = renderer;
                child.BoneName = fields.BoneName; child.SlotName = fields.SlotName; child.AttachmentName = fields.AttachmentName;
                child.FollowZSpacing = fields.FollowZSpacing; child.ZFront = fields.ZFront;
                child.FollowLocalScale = fields.FollowLocalScale; child.FollowBoneWorldFlip = fields.FollowBoneWorldFlip;
                child.FollowRotation = fields.FollowRotation; child.FollowSkeletonTransformFlipX = fields.FollowSkeletonTransformFlipX;
                child.InitializeOnAwake = fields.InitializeOnAwake; child.Gos = new[] { visibility };
                go.SetActive(true);
                var outside = new GameObject("Original external world bone follower"); outside.SetActive(false);
                external = outside.AddComponent<SkeletonRendererFollower>(); external.SkeletonRenderer = renderer;
                external.BoneName = "pWeapon_R2";
                outside.SetActive(true);
                previousBone = child.Bone;
                Require(previousBone != null && external.Bone != null, "actual current character weapon bones");
                renderer.OnAnimationRebuild += Rebuilt;
                renderer.Initialize(false, false);
                Require(rebuilds == 0 && ReferenceEquals(previousBone, child.Bone), "same asset retains follower registration and pose");
            }

            private void Rebuilt(IProfessionFollowerAnimation value)
            { Require(ReferenceEquals(value, renderer), "original rebuild sender identity"); rebuilds++; }

            internal void CheckVisible()
            {
                Require(target.UpdateMode == 3, "actual camera visibility reaches follower through whole or split scene geometry");
                var slot = target.Skeleton.FindSlot(child.SlotName);
                var original = slot.Attachment;
                var attachment = target.Skeleton.GetAttachment(slot.Index, child.AttachmentName);
                Require(attachment != null, "original weapon attachment exists in current source skin");
                try
                {
                    renderer.State.SetSlotAttachment(slot.Index, attachment);
                    child.ForceUpdate(); external.ForceUpdate();
                    Require(visibility.activeSelf, "original weapon attachment enables following");
                    var bone = slot.Bone;
                    Near(child.transform.localPosition, new Vector3(bone.WorldX, bone.WorldY, 0));
                    NearAngle(child.transform.localEulerAngles.z, Mathf.Atan2(bone.C, bone.A) * Mathf.Rad2Deg);
                    Near(child.transform.localScale, new Vector3(bone.ScaleX, bone.ScaleY, 1));
                    Near(external.transform.position, renderer.transform.TransformPoint(new Vector3(external.Bone.WorldX, external.Bone.WorldY, 0)));
                    var scale = renderer.transform.lossyScale;
                    NearAngle(external.transform.eulerAngles.z, renderer.transform.eulerAngles.z
                        + (scale.x * scale.y < 0 ? -external.Bone.WorldRotationX : external.Bone.WorldRotationX));
                    renderer.State.SetSlotAttachment(slot.Index, null); child.ForceUpdate();
                    Require(!visibility.activeSelf, "original missing attachment hides weapon part");
                    renderer.State.SetSlotAttachment(slot.Index, attachment); child.ForceUpdate();
                    Require(visibility.activeSelf, "original restored attachment resumes weapon part");
                }
                finally { renderer.State.SetSlotAttachment(slot.Index, original); child.ForceUpdate(); }
            }

            internal void CheckInvisible(MeshRenderer[] geometry)
            {
                var before = external.transform.localToWorldMatrix;
                var state = renderer.State;
                int bone = state.Source.FindBone(external.BoneName);
                var pose = state.Pose.World(bone);
                try
                {
                    foreach (var item in geometry) item.enabled = false;
                    Require(target.UpdateMode == target.UpdateWhenInvisible, "disabled fragments cannot keep original follower visible");
                    var changed = pose; changed.m03 += 50;
                    state.Pose.OverrideWorld(bone, changed); external.ForceUpdate();
                    Require(external.transform.localToWorldMatrix == before, "invisible source mode pauses original follower transform");
                }
                finally
                {
                    state.Pose.OverrideWorld(bone, pose);
                    foreach (var item in geometry) item.enabled = true;
                }
            }

            internal void CheckRebuilt()
            {
                Require(rebuilds == 1 && ReferenceEquals(target.Skeleton, renderer.State)
                    && !ReferenceEquals(previousBone, child.Bone)
                    && ReferenceEquals(child.Bone, target.Skeleton.FindSlot(child.SlotName).Bone)
                    && ReferenceEquals(external.Bone, target.Skeleton.FindBone(external.BoneName)),
                    "cross-sex initialization rebinds both original followers to new live bones");
                previousBone = child.Bone;
            }

            internal void CheckReleased()
            {
                Require(target.Skeleton == null && target.UpdateMode == target.UpdateWhenInvisible,
                    "released scene endpoint has no skeleton or visible update mode");
                var before = external.transform.localToWorldMatrix; external.ForceUpdate();
                Require(external.transform.localToWorldMatrix == before, "released endpoint cannot drive stale bone transforms");
                Debug.Log("[Current scene followers] Original source weapon attachment/rotation/scale, external mirrored transform, whole/split camera visibility, cross-sex rebuild and release passed.");
            }

            public void Dispose()
            {
                renderer.OnAnimationRebuild -= Rebuilt;
                child.Clear(); external.Clear();
                Object.Destroy(child.gameObject); Object.Destroy(external.gameObject);
            }

            private static void Near(Vector3 actual, Vector3 expected)
            { Require(Vector3.Distance(actual, expected) < .0001f, "original native follower transform"); }
            private static void NearAngle(float actual, float expected)
            { Require(Mathf.Abs(Mathf.DeltaAngle(actual, expected)) < .003f, "original native follower rotation"); }
        }
    }
}
