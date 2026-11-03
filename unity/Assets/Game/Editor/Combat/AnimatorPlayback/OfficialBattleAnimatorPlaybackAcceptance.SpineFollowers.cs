using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleSpinePlayables;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSpineFollowers()
        {
            RunScenePlayerAppearance(); SessionState.SetBool(Key + ".spineFollowers", true);
        }

        private static IEnumerator CheckSpineFollowers(Zjcs.Official.CombatAppearance.PlayerAppearance player)
        {
            var actor = player.gameObject;
            var native = actor.GetComponent<ProfessionNativeSceneRenderer>();
            var scope = new GameObject("Complete original native attachment follower graphs");
            var center = new MyPlayableCenter();
            var factory = new StatusMaterialFactory(center);
            var previousFactory = StoryPlayableInstantiation.Resolve;
            StoryPlayableInstantiation.Resolve = _ => factory;
            var manager = new MyPlayableManager { ScopeRoot = scope, MyPlayableCenter = center,
                InstanceFactory = factory, LowMemory = new Zjcs.Official.StoryPlayables.GAction() };
            var attachment = actor.AddComponent<AttachAgent>();
            attachment.AddBoneNode("PlayerModel_Game", actor.transform);
            IEnumerator Pump() { for (int i = 0; i < 24; i++) { yield return null; center.Update(.02f); center.LateUpdate(); } }
            void Draw()
            {
                native.LateUpdateMesh();
                foreach (var follower in actor.GetComponentsInChildren<SkeletonRendererFollower>(true)) follower.ForceUpdate();
                foreach (var weapon in actor.GetComponentsInChildren<ProfessionNativeSceneWeaponRenderer>(true)) weapon.UpdateAnimationAndMesh();
            }
            Draw();
            var geometry = actor.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            var bounds = geometry[0].bounds; foreach (var r in geometry.Skip(1)) bounds.Encapsulate(r.bounds);
            string label = "spine-follow-" + native.State.Source.Sex + "-" + player.AppearanceRuntimeData.SkinMap[SkinType.pWeapon];
            // The existing weapon follower waits for real camera visibility.
            // Render once before measuring the final composed character.
            CaptureSceneRendererCamera(bounds, label + "-visibility"); Draw();
            bounds = geometry[0].bounds; foreach (var r in geometry.Skip(1)) bounds.Encapsulate(r.bounds);
            try
            {
                manager.Initialize();
                foreach (var entry in new[] { (24207, "playable_24207"), (24227, "playable24227") })
                {
                    var prefab = Resources.Load<GameObject>("Combat/StatusSpineFollowers/Prefabs/EcEntity/Fight/FightSkill/" + entry.Item1 + "/" + entry.Item2);
                    Require(prefab, "complete original attachment follower graph " + entry.Item1);
                    var config = prefab.GetComponentInChildren<MyPlayableFollowSpinePart>(true);
                    Require(config && config.Duration == -1 && config.FollowDuration == -1 && !config.FollowEnable
                        && !config.IsFollowShowHideNode && config.AttachmentFollower, "original serialized follower fields retained");
                    var graph = manager.Play(prefab, actor, scope); yield return Pump();
                    var leaf = graph.GetComponentInChildren<MyPlayableFollowSpinePart>(true);
                    var follower = leaf.AttachmentFollower;
                    Require(leaf.mySource == actor && leaf.myTarget == scope
                        && leaf.myApplyComponent == native && follower.SkeletonRenderer == native && follower.enabled
                        && follower.gameObject.activeInHierarchy, "original ApplyByBone resolves the actual native scene renderer");
                    CheckAttachmentTransform(follower, native);
                    Draw();
                    CaptureSceneRendererCamera(bounds, label + "-" + entry.Item1 + "-active");
                    var oldScale = actor.transform.localScale;
                    try
                    {
                        actor.transform.localScale = new Vector3(-oldScale.x, oldScale.y, oldScale.z);
                        follower.ForceUpdate(); CheckAttachmentTransform(follower, native);
                    }
                    finally { actor.transform.localScale = oldScale; follower.ForceUpdate(); }
                    graph.Stop(); yield return Pump();
                    Require(!follower.enabled && !follower.gameObject.activeSelf && !follower.SkeletonRenderer,
                        "original stop hides, disables and unbinds attachment follower");
                    var stopped = follower.transform.position;
                    var oldPosition = actor.transform.position;
                    actor.transform.position += Vector3.right;
                    follower.ForceUpdate();
                    Require(follower.transform.position == stopped, "unbound original follower cannot follow old source");
                    actor.transform.position = oldPosition;
                    var replay = manager.Play(prefab, actor, scope); yield return Pump();
                    Require(ReferenceEquals(graph, replay) && follower.SkeletonRenderer == native && follower.enabled,
                        "actual native graph pool replay rebinds its attachment source");
                    CheckAttachmentTransform(follower, native);
                    replay.Stop(); yield return Pump();
                    Draw();
                    CaptureSceneRendererCamera(bounds, label + "-" + entry.Item1 + "-stopped");
                }
                CheckAttachmentVisibility(native, scope);
                Debug.Log("[Original attachment followers] " + label + ": two complete original graphs, actual ApplyByBone, native bone transform/mirror, stop and pool replay; original attachment/alpha tracker fixture passed.");
            }
            finally
            {
                manager.Dispose(); center.RemoveAll(); StoryPlayableInstantiation.Resolve = previousFactory;
                Object.DestroyImmediate(attachment); Object.DestroyImmediate(scope);
            }
        }

        private static void CheckAttachmentTransform(AttachmentFollower follower, ProfessionNativeSceneRenderer native)
        {
            var bone = ((IProfessionFollowerSkeleton)native.State).FindBone(follower.BoneName);
            Require(bone != null, "original configured follower bone exists in actual source");
            var expected = native.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0));
            Require(Vector2.Distance(follower.transform.position, expected) < .0001f, "original world attachment position");
            float rotation = native.transform.eulerAngles.z + (native.transform.lossyScale.x * native.transform.lossyScale.y < 0 ? -bone.WorldRotationX : bone.WorldRotationX);
            Require(!follower.FollowRotation || Mathf.Abs(Mathf.DeltaAngle(follower.transform.eulerAngles.z, rotation)) < .003f,
                "original mirrored world attachment rotation");
            if (follower.FollowLocalScale)
            {
                float x = bone.ScaleX * (follower.FollowSkeletonTransformFlipX && native.transform.lossyScale.x <= 0 ? -1 : 1);
                Require(Vector2.Distance(follower.transform.localScale, new Vector2(x, bone.ScaleY)) < .0001f, "original local attachment scale");
            }
        }

        // Explicit branch fixture on the actual native pose. It changes only
        // temporary component configuration and native color input, never an official asset.
        private static void CheckAttachmentVisibility(ProfessionNativeSceneRenderer native, GameObject scope)
        {
            var state = native.State;
            var skeleton = (IProfessionFollowerSkeleton)state;
            int index = Enumerable.Range(0, state.SlotPose.Count).First(i =>
                !string.IsNullOrEmpty(state.SlotPose.Attachment(i)) && skeleton.FindSlot(state.SlotPose.Name(i)).Attachment != null
                && ReferenceEquals(skeleton.FindSlot(state.SlotPose.Name(i)).Attachment, skeleton.GetAttachment(i, state.SlotPose.Attachment(i))));
            string slotName = state.SlotPose.Name(index), attachmentName = state.SlotPose.Attachment(index);
            var slot = skeleton.FindSlot(slotName); var original = slot.Attachment;
            var host = new GameObject("Original attachment visibility fixture"); host.SetActive(false);
            host.transform.SetParent(native.transform, false);
            var visible = new GameObject("Original attachment follower visible target"); visible.transform.SetParent(host.transform, false);
            var follower = host.AddComponent<AttachmentFollower>(); follower.InitializeOnAwake = false;
            follower.SkeletonRenderer = native; follower.SlotName = slotName; follower.AttachmentName = attachmentName;
            follower.Gos = new[] { visible };
            var tracked = new GameObject("Original alpha tracker target"); tracked.transform.SetParent(scope.transform, false);
            var tracker = native.gameObject.AddComponent<SkeletonAttachmentActiveTracker>();
            tracker.SkeletonRenderer = native; tracker.SlotName = slotName; tracker.AttachmentName = attachmentName;
            tracker.TargetList = new List<GameObject> { tracked };
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var colors = Private<Color[]>(state, "animationColors"); var originalColor = colors[index];
            var colorMode = state.GetType().GetField("nativeColors", flags); bool originalMode = (bool)colorMode.GetValue(state);
            var update = typeof(SkeletonAttachmentActiveTracker).GetMethod("Update", flags);
            try
            {
                host.SetActive(true); follower.Initialize(); follower.ForceUpdate(); tracker.Initialize();
                Require(visible.activeSelf && tracked.activeSelf && tracker.IsActive, "original matching attachment enables both trackers");
                Require(Vector2.Distance(host.transform.localPosition, new Vector2(slot.Bone.WorldX, slot.Bone.WorldY)) < .0001f,
                    "original direct child attachment follower uses local bone coordinates");
                state.SetSlotAttachment(index, null); follower.ForceUpdate(); update.Invoke(tracker, null);
                Require(!visible.activeSelf && !tracked.activeSelf && !tracker.IsActive, "missing actual attachment hides both targets");
                state.SetSlotAttachment(index, original); follower.ForceUpdate(); update.Invoke(tracker, null);
                Require(visible.activeSelf && tracked.activeSelf, "restored actual attachment resumes targets");
                colorMode.SetValue(state, true); colors[index] = new Color(1, 1, 1, 0);
                update.Invoke(tracker, null);
                Require(!tracker.IsActive && !tracked.activeSelf && ((IProfessionFollowerSlotAlpha)slot).Alpha == 0,
                    "live native zero alpha hides original active tracker");
                tracker.Initialize();
                Require(tracker.IsActive, "original Initialize checks attachment before per-frame alpha filtering");
                update.Invoke(tracker, null); Require(!tracker.IsActive, "original next Update applies zero alpha");
                colors[index] = new Color(1, 1, 1, .0001f); update.Invoke(tracker, null);
                Require(tracker.IsActive && Mathf.Approximately(((IProfessionFollowerSlotAlpha)slot).Alpha, .0001f),
                    "native float alpha remains nonzero below byte quantization");
                follower.SetSkeletonRenderer(null); var before = host.transform.localToWorldMatrix;
                follower.ForceUpdate(); Require(host.transform.localToWorldMatrix == before, "detached child follower stops updates");
            }
            finally
            {
                colors[index] = originalColor; colorMode.SetValue(state, originalMode); state.SetSlotAttachment(index, original);
                Object.DestroyImmediate(tracker); Object.DestroyImmediate(host); Object.DestroyImmediate(tracked);
            }
        }
    }
}
