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
    public static class ProfessionNativeIdleIkValidation
    {
        [Serializable] private sealed class Poses { public Body[] cases; }
        [Serializable] private sealed class Body { public string sex; public ProfessionNativeBonePose.Bone[] bones; public Clip[] clips; }
        [Serializable] private sealed class Clip { public string name; public Track[] tracks; }
        [Serializable] private sealed class Track { public int bone, channel; public ProfessionNativeCurve.Key[] keys; }
        [Serializable] private sealed class Payload { public Case[] cases; }
        [Serializable] private sealed class Case
        {
            public string sex, animation; public float time;
            public ProfessionNativeIdleIk.Constraint[] constraints;
            public Rotation[] rotations; public World[] world;
        }
        [Serializable] private sealed class Rotation { public int bone; public float value; }
        [Serializable] private sealed class World { public int bone; public float[] values; }
        private const string Active = "Zjcs.ProfessionIdleIk.Active";
        private static bool ready;
        private static float maxAngleError, maxWorldError;
        private static int angles, matrices;

        static ProfessionNativeIdleIkValidation()
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
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Idle IK fixture did not enter Play Mode.");
                if (!ready || !EditorApplication.isPlaying) return;
                var poses = JsonUtility.FromJson<Poses>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("bone-pose-validation.json")));
                var payload = JsonUtility.FromJson<Payload>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("idle-ik-validation.json")));
                Require(payload.cases.Length == 48, "six source clips at eight times");
                foreach (Case sample in payload.cases)
                {
                    Body body = poses.cases.Single(b => b.sex == sample.sex);
                    Clip clip = body.clips.Single(c => c.name == sample.animation);
                    var pose = new ProfessionNativeBonePose(body.bones);
                    pose.SetSkinBones(Enumerable.Range(0, pose.Count).ToArray());
                    pose.Apply(clip.tracks.Select(t => new ProfessionNativeBonePose.Track(t.bone, t.channel, new ProfessionNativeCurve(t.keys))).ToArray(), sample.time);
                    ProfessionNativeIdleIk.Apply(pose, sample.constraints);
                    string label = sample.sex + "/" + sample.animation + "/" + sample.time.ToString("R");
                    foreach (Rotation expected in sample.rotations)
                    {
                        float error = Mathf.Abs(Mathf.DeltaAngle(expected.value, pose.LocalRotation(expected.bone)));
                        maxAngleError = Mathf.Max(error, maxAngleError); angles++;
                        Require(error <= .0004f, label + " rotation " + expected.bone + " differs by " + error.ToString("R"));
                    }
                    foreach (World expected in sample.world)
                    {
                        Matrix4x4 m = pose.World(expected.bone);
                        float[] actual = { m.m00, m.m01, m.m10, m.m11, m.m03, m.m13 };
                        matrices++;
                        for (int i = 0; i < 6; i++)
                        {
                            float error = Mathf.Abs(expected.values[i] - actual[i]);
                            maxWorldError = Mathf.Max(error, maxWorldError);
                            Require(!float.IsNaN(actual[i]) && error <= .001f + .000007f * Mathf.Abs(expected.values[i]), label + " affine " + expected.bone + "/" + i + " differs by " + error.ToString("R"));
                        }
                    }
                }
                CheckSynthetic();
                Debug.Log($"Profession native idle IK binding passed: {payload.cases.Length} poses, {angles} final angles and {matrices} world matrices; maximum circular angle difference {maxAngleError:R}, maximum absolute world difference {maxWorldError:R}. Original order, zero mix, lazy world ancestors, descendant invalidation and explicit unsupported chains pass. References are independent geometry, not original SDK poses; complete constraints, character rendering and profession UI remain unvalidated.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void CheckSynthetic()
        {
            var source = new[] {
                new ProfessionNativeBonePose.Bone { name = "root" },
                new ProfessionNativeBonePose.Bone { name = "arm", parent = 0 },
                new ProfessionNativeBonePose.Bone { name = "hand", parent = 1, x = 4 },
                new ProfessionNativeBonePose.Bone { name = "target", parent = 0, x = 3, y = 4 },
                new ProfessionNativeBonePose.Bone { name = "unsupported", parent = 0, inherit = 4 }
            };
            var pose = new ProfessionNativeBonePose(source);
            Require(pose.World(2).m03 == 4, "initial descendant cache");
            pose.SetRotation(1, 90);
            Require(Mathf.Abs(pose.World(2).m03) < .000001f && Mathf.Abs(pose.World(2).m13 - 4) < .000001f, "descendant invalidation");
            pose.Reset();
            var pair = new ProfessionNativeIdleIk.Constraint { order = 0, bones = new[] { 1, 2 }, target = 3, childLength = 3, mix = 1, bendPositive = true };
            var aim = new ProfessionNativeIdleIk.Constraint { order = 1, bones = new[] { 2 }, target = 3, mix = 1 };
            ProfessionNativeIdleIk.Apply(pose, new[] { pair, aim });
            Vector2 end = pose.World(2).MultiplyPoint3x4(new Vector3(3, 0, 0));
            Require(Vector2.Distance(end, new Vector2(3, 4)) < .00001f, "pair then aim uses updated parent transform");
            Throws<NotSupportedException>(() => pose.World(4));
            Throws<ArgumentException>(() => ProfessionNativeIdleIk.Apply(pose, new[] { aim, pair }));
            source[1].scaleX = 1.000001f;
            Throws<NotSupportedException>(() => ProfessionNativeIdleIk.Apply(new ProfessionNativeBonePose(source), new[] { pair }));
            pair.mix = 0;
            ProfessionNativeIdleIk.Apply(new ProfessionNativeBonePose(source), new[] { pair });
            Require(pose.Parent(2) == 1, "source parent identity");
        }
        private static void Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
