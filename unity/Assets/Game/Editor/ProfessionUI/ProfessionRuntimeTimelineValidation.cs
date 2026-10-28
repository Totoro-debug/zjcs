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
    public static class ProfessionRuntimeTimelineValidation
    {
        [Serializable] private sealed class SlotFixture { public Body[] cases; }
        [Serializable] private sealed class Body { public string sex; public Clip[] clips; }
        [Serializable] private sealed class Clip { public string name; public SlotSample[] samples; }
        [Serializable] private sealed class SlotSample { public float time; public string[] attachments; public int[] order; }
        [Serializable] private sealed class IkFixture { public IkFrame[] cases; }
        [Serializable] private sealed class IkFrame
        {
            public string sex, animation; public float time; public ProfessionNativeIdleIk.Constraint[] constraints;
            public Rotation[] rotations; public World[] world;
        }
        [Serializable] private sealed class Rotation { public int bone; public float value; }
        [Serializable] private sealed class World { public int bone; public float[] values; }
        private const string Active = "Zjcs.ProfessionRuntimeTimelines.Active";
        private static bool ready;
        private static int slotChecks, stateChecks, frames;
        private static float maxAngle, maxWorld;
        static ProfessionRuntimeTimelineValidation()
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
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 300));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Actual packaged timeline validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check();
                Debug.Log("Actual packaged idle attachment/draw-order/IK bindings passed Play Mode: " + slotChecks + " exact slot/order checks over 109 source boundary/backward samples; " + stateChecks + " exact IK state comparisons, " + frames + " post-IK source frames; maximum circular angle difference " + maxAngle.ToString("R") + ", maximum world difference " + maxWorld.ToString("R") + ". Original native data supplies all runtime inputs; independent verified references supply expected values only. Signed weapon offsets, null/blink attachment keys and constant source IK defaults pass. Physics, mixed animation, actor/page and Windows Player remain unvalidated.");
                Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            var slots = JsonUtility.FromJson<SlotFixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("slot-pose-validation.json")));
            var ik = JsonUtility.FromJson<IkFixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("idle-ik-validation.json")));
            slotChecks = 0; stateChecks = 0; frames = 0; maxAngle = 0; maxWorld = 0; int samples = 0;
            foreach (var body in slots.cases)
            {
                var data = ProfessionNativeSkeletonData.LoadResource(body.sex);
                var allBones = data.CreateBonePose(); allBones.SetSkinBones(Enumerable.Range(0, allBones.Count).ToArray());
                var state = data.CreateSlotPose(allBones);
                foreach (var clip in body.clips)
                {
                    var bound = data.CreateSlotClip(clip.name);
                    foreach (var sample in clip.samples.Reverse())
                    {
                        state.Apply(bound, sample.time); samples++;
                        for (int i = 0; i < data.SlotCount; i++)
                        {
                            Require(state.Attachment(i) == (string.IsNullOrEmpty(sample.attachments[i]) ? null : sample.attachments[i]), "original attachment boundary/null identity");
                            Require(state.DrawSlot(i) == sample.order[i], "complete original draw permutation and signed offset"); slotChecks += 2;
                        }
                    }
                }
                foreach (var sample in ik.cases.Where(c => c.sex == body.sex))
                {
                    var pose = data.CreateBonePose(); pose.Apply(data.CreateBoneTracks(sample.animation), sample.time);
                    var actual = data.CreateIdleIk(sample.animation, sample.time);
                    Require(actual.Length == sample.constraints.Length, "all original IK definitions");
                    for (int i = 0; i < actual.Length; i++)
                    {
                        var a = actual[i]; var e = sample.constraints[i];
                        Require(a.name == e.name && a.order == e.order && a.target == e.target && a.bones.SequenceEqual(e.bones) &&
                            a.childLength == e.childLength && a.mix == e.mix && a.bendPositive == e.bendPositive, "actual packaged IK fields/defaults/order differ"); stateChecks += 7;
                    }
                    ProfessionNativeIdleIk.Apply(pose, actual);
                    foreach (var expected in sample.rotations)
                    {
                        float error = Mathf.Abs(Mathf.DeltaAngle(expected.value, pose.LocalRotation(expected.bone))); maxAngle = Mathf.Max(maxAngle, error);
                        Require(error <= .0004f, "actual original idle IK angles");
                    }
                    foreach (var expected in sample.world)
                    {
                        var m = pose.World(expected.bone); float[] values = { m.m00, m.m01, m.m10, m.m11, m.m03, m.m13 };
                        for (int i = 0; i < values.Length; i++)
                        {
                            float error = Mathf.Abs(expected.values[i] - values[i]); maxWorld = Mathf.Max(maxWorld, error);
                            Require(!float.IsNaN(values[i]) && error <= .001f + .000007f * Mathf.Abs(expected.values[i]), "actual source post-IK world reference");
                        }
                    }
                    frames++;
                }
                Reject<NotSupportedException>(() => data.CreateIdleIk("home/wakeUp_01", 0));
                Reject<ArgumentException>(() => data.CreateIdleIk("home/idle_01", float.NaN));
            }
            Require(samples == 109 && slotChecks == 27468 && frames == 48, "complete verified source reference coverage");
        }
        private static void Reject<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Original runtime timelines: " + message); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
