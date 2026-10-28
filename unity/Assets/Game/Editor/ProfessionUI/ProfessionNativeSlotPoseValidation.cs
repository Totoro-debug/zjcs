using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeSlotPoseValidation
    {
        [Serializable] private sealed class Bones { public BoneBody[] cases; }
        [Serializable] private sealed class BoneBody { public string sex; public ProfessionNativeBonePose.Bone[] bones; }
        [Serializable] private sealed class Payload { public Body[] cases; }
        [Serializable] private sealed class Body { public string sex; public ProfessionNativeSlotPose.Slot[] slots; public Clip[] clips; }
        [Serializable] private sealed class Clip
        {
            public string name;
            public ProfessionNativeSlotPose.AttachmentTrack[] tracks;
            public ProfessionNativeSlotPose.DrawOrderKey[] order;
            public Sample[] samples;
        }
        [Serializable] private sealed class Sample { public float time; public string[] attachments; public int[] order; }
        private const string Active = "Zjcs.ProfessionSlotPose.Active";
        private static bool ready;
        static ProfessionNativeSlotPoseValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SessionState.SetBool(Active, true); SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Slot pose fixture did not enter Play Mode.");
                if (!ready || !EditorApplication.isPlaying) return;
                var bones = JsonUtility.FromJson<Bones>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("bone-pose-validation.json")));
                var source = JsonUtility.FromJson<Payload>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("slot-pose-validation.json")));
                Require(source.cases.Length == 2, "both original skeleton slot lists");
                int slots = 0, tracks = 0, keys = 0, drawKeys = 0, samples = 0, checks = 0;
                foreach (Body body in source.cases)
                {
                    var hierarchy = new ProfessionNativeBonePose(bones.cases.Single(b => b.sex == body.sex).bones);
                    hierarchy.SetSkinBones(Enumerable.Range(0, hierarchy.Count).ToArray());
                    var pose = new ProfessionNativeSlotPose(body.slots, hierarchy); slots += pose.Count;
                    for (int i = 0; i < pose.Count; i++)
                    {
                        var slot = body.slots[i];
                        Require(pose.Name(i) == slot.name && pose.Bone(i) == slot.bone && pose.Blend(i) == slot.blend &&
                            pose.Color(i).Equals(slot.color) && pose.HasDarkColor(i) == slot.hasDarkColor && pose.DarkColor(i).Equals(slot.darkColor), "original setup fields");
                    }
                    Require(body.clips.Length == 3, "three original idle clips");
                    foreach (Clip clip in body.clips)
                    {
                        var bound = new ProfessionNativeSlotPose.Clip(pose.Count, clip.tracks, clip.order);
                        tracks += clip.tracks.Length; keys += clip.tracks.Sum(t => t.keys.Length); drawKeys += clip.order.Length;
                        foreach (Sample sample in clip.samples.Reverse())
                        {
                            pose.Apply(bound, sample.time); samples++;
                            Require(sample.attachments.Length == pose.Count && sample.order.Length == pose.Count, "complete source sample");
                            for (int i = 0; i < pose.Count; i++)
                            {
                                checks += 2;
                                Require(pose.Attachment(i) == sample.attachments[i] && pose.DrawSlot(i) == sample.order[i], body.sex + "/" + clip.name + "/" + sample.time.ToString("R") + "/" + i);
                            }
                        }
                        if (clip.order.Length > 0)
                        {
                            int left = Array.FindIndex(body.slots, s => s.name == "W_jian_L/W_jian_L");
                            int down = Array.FindIndex(body.slots, s => s.name == "W_jian_D/W_jian_D");
                            pose.Apply(bound, 0);
                            Require(pose.DrawSlot(left + 4) == left && pose.DrawSlot(down - 6) == down, "original weapon destinations include signed negative-six offset");
                            int[] untouched = Enumerable.Range(0, pose.Count).Where(i => i != left && i != down).ToArray();
                            int[] actual = Enumerable.Range(0, pose.Count).Select(pose.DrawSlot).Where(i => i != left && i != down).ToArray();
                            Require(untouched.SequenceEqual(actual), "untouched draw slots preserve relative source order");
                        }
                    }
                }
                Synthetic();
                Debug.Log($"Profession native idle slot state passed: {slots} source slots, {tracks} attachment tracks, {keys} keys, {drawKeys} draw-order keys, {samples} samples and {checks} attachment/order checks. Exact key boundaries, null clears/blinks, backward sampling, signed weapon offsets, stable untouched draw order, setup color/dark/blend, inactive bones and input ownership/rejection pass. No SDK pose/visual reference, animation mixing, skin/geometry rendering or complete profession UI is validated.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Synthetic()
        {
            var bones = new ProfessionNativeBonePose(new[] { new ProfessionNativeBonePose.Bone { name = "base" }, new ProfessionNativeBonePose.Bone { name = "skin", parent = 0, skinRequired = true } });
            var setup = new[] { new ProfessionNativeSlotPose.Slot { name = "s", attachment = "setup", bone = 1 } };
            var pose = new ProfessionNativeSlotPose(setup, bones);
            var key = new ProfessionNativeSlotPose.AttachmentKey { time = 1, name = null };
            var track = new ProfessionNativeSlotPose.AttachmentTrack { slot = 0, keys = new[] { key } };
            var clip = new ProfessionNativeSlotPose.Clip(1, new[] { track }, Array.Empty<ProfessionNativeSlotPose.DrawOrderKey>());
            key.name = "mutated"; setup[0].attachment = "mutated";
            pose.Apply(clip, 1); Require(pose.Attachment(0) == "setup", "inactive skin bone skips attachment keys and owns setup");
            bones.SetSkinBones(new[] { 1 }); pose.Apply(clip, .99f); Require(pose.Attachment(0) == "setup", "before first key");
            pose.Apply(clip, 1); Require(pose.Attachment(0) == null, "null clear at exact key and input-key ownership");
            pose.Apply(clip, 0); Require(pose.Attachment(0) == "setup", "backward time resets attachment");
            Throws<ArgumentException>(() => pose.Apply(clip, float.NaN));
            Throws<ArgumentException>(() => new ProfessionNativeSlotPose.Clip(1, Array.Empty<ProfessionNativeSlotPose.AttachmentTrack>(), new[] { new ProfessionNativeSlotPose.DrawOrderKey { slots = new[] { 2 } } }));
            Throws<ArgumentException>(() => new ProfessionNativeSlotPose.Clip(1, new[] { track, track }, Array.Empty<ProfessionNativeSlotPose.DrawOrderKey>()));
        }
        private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
