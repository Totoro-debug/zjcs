using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSceneMixNext()
        {
            RunSceneMecanimParts(); SessionState.SetBool(Key + ".sceneMixNext", true);
        }

        private static void SolveSceneMixNext(ProfessionNativeAppearanceSkeleton skeleton,
            ProfessionNativeAnimatorPose.MixNextFrame frame) =>
            ProfessionNativeConstraintPose.EvaluateMixNextBeforePhysics(skeleton,
                frame.Outgoing.Animation, frame.Outgoing.Time, frame.Outgoing.Loop,
                frame.Incoming.Animation, frame.Incoming.Time, frame.Incoming.Loop, frame.Weight);

        private static void CheckSceneMixNext(PlayerAnimatorAppearance player)
        {
            var renderer = player.GetComponent<ProfessionNativeSceneRenderer>();
            var state = renderer.State; var animator = player.Animator;
            var controller = animator.runtimeAnimatorController;
            var parts = player.GetComponent<AppearanceRendererPartHandler>();
            var weaponRoot = parts.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject;
            var weapon = weaponRoot.GetComponentInChildren<ProfessionNativeSceneWeaponRenderer>(true);
            var sampler = new ProfessionNativeAnimatorPose(animator, ProfessionNativeConstraintPose.EvaluateBeforePhysics, SolveSceneMixNext);
            var label = "mix-next-" + state.Source.Sex + "-" + controller.name + (player.AnimController ? "-controller" : "-direct");
            void Draw()
            {
                renderer.LateUpdateMesh(); weaponRoot.GetComponent<SkeletonRendererFollower>().ForceUpdate();
                weapon.UpdateAnimationAndMesh();
            }
            void Reset()
            {
                foreach (var parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger) animator.ResetTrigger(parameter.nameHash);
                animator.runtimeAnimatorController = controller; animator.Rebind(); animator.Update(0);
                animator.Play("Idle", 0, 0); animator.Update(0);
            }
            Matrix4x4[] Pose() => Enumerable.Range(0, state.Pose.Count).Select(state.Pose.World).ToArray();
            try
            {
                Reset(); sampler.ApplyUnmixed(state); Draw();
                // The original follower ignores invisible renderers. Prime its
                // actual visibility, then update the independent weapon before
                // computing stable pose bounds or comparing rendered snapshots.
                CaptureSceneRendererCamera(SceneCharacterBounds(renderer, weapon), label + "-visibility"); Draw();
                animator.CrossFadeInFixedTime("Damage", .2f, 0, 0); animator.Update(.03f);
                var first = sampler.ApplyMixNext(state); Draw();
                Require(first.Outgoing.Animation.Name.EndsWith("/idle_01") && first.Incoming.Animation.Name.EndsWith("/hit_01")
                    && first.Weight > 0 && first.Weight < 1, "actual original idle/hit transition supplies two native motions and nontrivial weight");
                CheckMixedLocalChannels(state, first);
                var bounds = SceneCharacterBounds(renderer, weapon);
                animator.Update(.05f);
                var next = sampler.ApplyMixNext(state); Draw();
                CheckMixedLocalChannels(state, next);
                Require(next.Weight > first.Weight && next.Outgoing.Time > first.Outgoing.Time && next.Incoming.Time > first.Incoming.Time,
                    "actual Unity transition advances both original motion clocks and incoming weight");
                bounds.Encapsulate(SceneCharacterBounds(renderer, weapon));
                void Mix(float weight) => ProfessionNativeConstraintPose.EvaluateMixNextBeforePhysics(state,
                    next.Outgoing.Animation, next.Outgoing.Time, next.Outgoing.Loop,
                    next.Incoming.Animation, next.Incoming.Time, next.Incoming.Loop, weight);
                Mix(0); Draw(); bounds.Encapsulate(SceneCharacterBounds(renderer, weapon));
                Mix(.65f); Draw(); bounds.Encapsulate(SceneCharacterBounds(renderer, weapon));
                byte[] Capture(string suffix)
                {
                    var pixels = CaptureSceneRendererCamera(bounds, label + suffix);
                    for (int x = 0; x < 675; x++)
                        Require(pixels[x * 4 + 3] == 0 && pixels[((1463 - 1) * 675 + x) * 4 + 3] == 0,
                            "actual mixed body and weapon fit the complete portrait vertical edges");
                    for (int y = 0; y < 1463; y++)
                        Require(pixels[(y * 675) * 4 + 3] == 0 && pixels[(y * 675 + 674) * 4 + 3] == 0,
                            "actual mixed body and weapon fit the complete portrait horizontal edges");
                    return pixels;
                }
                SolveSceneMixNext(state, first); Draw(); var early = Capture("-early");
                SolveSceneMixNext(state, next); Draw(); var middle = Capture("-middle");
                Require(!early.SequenceEqual(middle), "actual MixNext changes the rendered original body pose");
                var pose = Pose(); sampler.ApplyMixNext(state); Draw();
                Require(pose.SequenceEqual(Pose()), "identical Animator snapshot does not accumulate previous pose or constraints");

                // Hold both actual graph times fixed. Weight zero must preserve
                // the outgoing pose exactly; partial weights must not reset
                // properties missing from the incoming source timelines.
                ProfessionNativeConstraintPose.EvaluateBeforePhysics(state, next.Outgoing.Animation, next.Outgoing.Time, next.Outgoing.Loop);
                Draw(); var outgoing = Capture("-outgoing"); var outgoingPose = Pose();
                Mix(0); Draw();
                Require(outgoingPose.SequenceEqual(Pose()), "zero incoming weight retains exact outgoing solved matrices");
                Mix(.65f); Draw();
                var mixed = Capture("-fixed-weight");
                Require(!mixed.SequenceEqual(outgoing), "same source times with incoming weight change actual pixels");
                Mix(0); Draw();
                Require(outgoingPose.SequenceEqual(Pose()), "resetting incoming weight restores original pose without accumulated drift");
                bool rejected = false;
                try { Mix(float.NaN); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected && outgoingPose.SequenceEqual(Pose()), "invalid mixed weight rejects before pose mutation");
                rejected = false;
                try { new ProfessionNativeAnimatorPose(animator, ProfessionNativeConstraintPose.EvaluateBeforePhysics).ApplyMixNext(state); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected && outgoingPose.SequenceEqual(Pose()), "mixing requires an explicit solver and does not install a fallback");
            }
            finally
            {
                Reset(); sampler.ApplyUnmixed(state);
                Draw();
            }
            Debug.Log("[Native MixNext] " + label + ": actual two-motion weights/clocks, keyed/unkeyed local properties, repeatability, zero weight, rejection and original body pixels passed before physics.");
        }

        private static void CheckMixedLocalChannels(ProfessionNativeAppearanceSkeleton actual, ProfessionNativeAnimatorPose.MixNextFrame frame)
        {
            var outgoing = new ProfessionNativeAppearanceSkeleton(actual.Source); outgoing.SetSkin(actual.Skin);
            var incoming = new ProfessionNativeAppearanceSkeleton(actual.Source); incoming.SetSkin(actual.Skin);
            outgoing.EvaluateNativeAnimation(frame.Outgoing.Animation, frame.Outgoing.Time, frame.Outgoing.Loop);
            incoming.EvaluateNativeAnimation(frame.Incoming.Animation, frame.Incoming.Time, frame.Incoming.Loop);
            var keyed = new HashSet<int>();
            foreach (var track in frame.Incoming.Animation.BoneTracks)
                if (track.Curve.TryEvaluate(frame.Incoming.Time, out _)) keyed.Add(track.Bone * 7 + track.Channel);
            var ikBones = new HashSet<int>(frame.Outgoing.Animation.Constraints.SampleIk(frame.Outgoing.Time, actual.SkinConstraints).SelectMany(value => value.Bones));
            int checkedChannels = 0, unkeyed = 0;
            for (int bone = 0; bone < actual.Pose.Count; bone++)
            {
                if (!actual.Pose.IsActive(bone)) continue;
                for (int axis = 0; axis < 5; axis++)
                {
                    if (axis == 0 && ikBones.Contains(bone)) continue;
                    float Value(ProfessionNativeBonePose pose) => axis == 0 ? pose.LocalRotation(bone)
                        : axis <= 2 ? pose.LocalPosition(bone)[axis - 1] : pose.LocalScale(bone)[axis - 3];
                    float a = Value(outgoing.Pose), b = Value(incoming.Pose), expected = a;
                    if (keyed.Contains(bone * 7 + axis))
                    {
                        if (axis >= 3) a = Mathf.Abs(a) * Math.Sign(b);
                        expected = a + (b - a) * frame.Weight;
                    }
                    else unkeyed++;
                    Require(Mathf.Abs(Value(actual.Pose) - expected) < .002f,
                        "MixNext local channel follows actual Animator weight and incoming key presence: " + bone + "/" + axis);
                    checkedChannels++;
                }
            }
            Require(checkedChannels > 100 && unkeyed > 0, "actual current skin exercises keyed and held local channels");
            var fields = frame.Incoming.Animation.OriginalFields;
            var slotTracks = fields.TryGetValue("slots", out object rawSlots) ? (IReadOnlyDictionary<string, object>)rawSlots : null;
            bool Reached(object rows) => ((List<object>)rows).Any(value =>
                !((IReadOnlyDictionary<string, object>)value).TryGetValue("time", out object time) || Convert.ToSingle(time) <= frame.Incoming.Time);
            for (int slot = 0; slot < actual.SlotCount; slot++)
            {
                bool replace = actual.Pose.IsActive(actual.SlotPose.Bone(slot)) && slotTracks != null
                    && slotTracks.TryGetValue(actual.SlotPose.Name(slot), out object raw)
                    && ((IReadOnlyDictionary<string, object>)raw).TryGetValue("attachment", out object keys) && Reached(keys);
                Require(actual.SlotPose.Attachment(slot) == (replace ? incoming : outgoing).SlotPose.Attachment(slot),
                    "incoming reached attachment keys switch discretely while unkeyed slots retain outgoing attachments");
            }
            bool replaceOrder = fields.TryGetValue("drawOrder", out object order) && Reached(order);
            for (int index = 0; index < actual.SlotCount; index++)
                Require(actual.SlotPose.DrawSlot(index) == (replaceOrder ? incoming : outgoing).SlotPose.DrawSlot(index),
                    "MixNext draw order switches at incoming source keys without interpolating slot indices");
        }
    }
}
