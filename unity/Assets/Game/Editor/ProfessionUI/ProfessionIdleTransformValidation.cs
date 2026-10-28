using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionIdleTransformValidation
    {
        [Serializable] private sealed class Fixture { public Frame[] cases; }
        [Serializable] private sealed class Frame
        {
            public string sex, animation; public string[] skins; public float time;
            public State[] states; public ProfessionNativeIdleIk.Constraint[] ik; public World[] expected;
        }
        [Serializable] private sealed class State { public string name; public int order, target; public int[] bones; public float[] mixes; }
        [Serializable] private sealed class World { public int bone; public float a, b, c, d, x, y; }
        private const string Active = "Zjcs.ProfessionIdleTransform.Active";
        private static bool ready;
        private static int checks;
        private static float maximumError;
        static ProfessionIdleTransformValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) ready = true; };
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
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Combined idle transform validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check();
                Debug.Log("Original idle world transforms passed actual Play Mode: 96 combined source frames, " + checks + " matrix/state scalar comparisons, maximum absolute difference " + maximumError.ToString("R") + ". Actual packaged timelines run before original-order idle IK and active world transforms. Original setup/timeline omission defaults, complete weapon target copying, descendant caches and synthetic reflected/sheared geometry pass. Prior 144 world frames and idle IK regression checks also pass. No physics, mixed animation, original SDK poses, actor raster, full page or Windows Player claim.");
                Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            typeof(ProfessionWorldInheritanceValidation).GetMethod("Check", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            typeof(ProfessionNativeIdleIkValidation).GetMethod("CheckSynthetic", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("idle-transform-validation.json")));
            Require(fixture.cases.Length == 96, "both source skeletons, two original outfits, three idles, eight times");
            checks = 0; maximumError = 0;
            foreach (var group in fixture.cases.GroupBy(f => f.sex))
            {
                var data = ProfessionNativeSkeletonData.LoadResource(group.Key);
                var tracks = new Dictionary<string, ProfessionNativeBonePose.Track[]>();
                foreach (var frame in group)
                {
                    if (!tracks.TryGetValue(frame.animation, out var clip)) { clip = data.CreateBoneTracks(frame.animation); tracks.Add(frame.animation, clip); }
                    var pose = data.CreateBonePose(); var members = new HashSet<int>(); var constraints = new HashSet<int>();
                    foreach (string name in frame.skins)
                    {
                        var skin = (ProfessionNativeSkin)data.SkinCatalog.FindSkin(name);
                        foreach (int bone in skin.Bones) members.Add(bone);
                        foreach (int constraint in skin.Constraints) constraints.Add(constraint);
                    }
                    pose.SetSkinBones(members.ToArray()); pose.Apply(clip, frame.time);
                    ProfessionNativeIdleIk.Apply(pose, frame.ik);
                    var bound = ProfessionNativeIdleTransform.Bind(data, frame.animation, frame.time, constraints.ToArray());
                    Require(bound.Length == frame.states.Length, "exact active original transform set");
                    for (int i = 0; i < bound.Length; i++)
                    {
                        var actual = bound[i]; var expected = frame.states[i];
                        Require(actual.Name == expected.name && actual.Order == expected.order && actual.Target == expected.target && actual.Bones.SequenceEqual(expected.bones), "source transform identity and order");
                        float[] mixes = { actual.Rotation, actual.X, actual.Y, actual.ScaleX, actual.ScaleY, actual.Shear };
                        for (int j = 0; j < 6; j++) Near(expected.mixes[j], mixes[j]);
                    }
                    // Populate every unconstrained cache to verify that each
                    // world override invalidates the actual descendants.
                    pose.UpdateWorld();
                    ProfessionNativeIdleTransform.Apply(pose, bound); pose.UpdateWorld();
                    Require(frame.expected.Length == Enumerable.Range(0, pose.Count).Count(pose.IsActive), "all active original bones and ancestor closure");
                    foreach (var expected in frame.expected)
                    {
                        var actual = pose.World(expected.bone);
                        Near(expected.a, actual.m00); Near(expected.b, actual.m01); Near(expected.c, actual.m10);
                        Near(expected.d, actual.m11); Near(expected.x, actual.m03); Near(expected.y, actual.m13);
                    }
                    if (frame.animation != "home/idle_01" && frame.time >= 0)
                        Matrix(pose.World(data.FindBone("pWeapon_R2")), pose.World(data.FindBone("pWeapon_zhu")));
                    if (frame.animation != "home/idle_01")
                    {
                        var weapon = bound.Single(c => c.Name == "pWeapon_R2");
                        Require(frame.time < 0 ? weapon.Rotation == 0 && weapon.X == 0 && weapon.ScaleX == 0 && weapon.Shear == 0 :
                            weapon.Rotation == 1 && weapon.X == 1 && weapon.Y == 1 && weapon.ScaleX == 1 && weapon.ScaleY == 1 && weapon.Shear == 1, "source setup zeros versus constant key first-five ones and explicit shear");
                    }
                }
            }
            CheckGeometry();
        }
        private static void CheckGeometry()
        {
            foreach (float reflection in new[] { -1f, 1f })
            {
                var source = new[] {
                    new ProfessionNativeBonePose.Bone { name = "root", x = 5, y = -7, rotation = 33, scaleX = -2, scaleY = 1.3f },
                    new ProfessionNativeBonePose.Bone { name = "target", parent = 0, x = 13, y = -17, rotation = -41, scaleX = 2, scaleY = reflection * 3, shearX = 7, shearY = -19 },
                    new ProfessionNativeBonePose.Bone { name = "constrained", parent = 0, x = 3, y = 4, rotation = 127, scaleX = 1.7f, scaleY = 4, shearX = -13, shearY = 23 },
                    new ProfessionNativeBonePose.Bone { name = "child", parent = 2, x = 11, y = -3, rotation = 17 },
                    new ProfessionNativeBonePose.Bone { name = "unrelated", parent = 0, x = 9 } };
                var pose = new ProfessionNativeBonePose(source); pose.UpdateWorld();
                var unrelated = Matrix4x4.Translate(new Vector3(111, 222, 0)); pose.OverrideWorld(4, unrelated);
                var complete = new ProfessionNativeIdleTransform.Constraint { Name = "copy", Order = 0, Target = 1, Bones = new[] { 2 }, Rotation = 1, X = 1, Y = 1, ScaleX = 1, ScaleY = 1, Shear = 1 };
                ProfessionNativeIdleTransform.Apply(pose, new[] { complete });
                Matrix(pose.World(1), pose.World(2)); Matrix(unrelated, pose.World(4));
                Matrix(pose.World(1) * Matrix4x4.TRS(new Vector3(11, -3, 0), Quaternion.Euler(0, 0, 17), Vector3.one), pose.World(3));
                var position = new ProfessionNativeIdleTransform.Constraint { Name = "position", Order = 1, Target = 1, Bones = new[] { 2 }, X = .25f, Y = .75f, OffsetPosition = new Vector2(3, -9) };
                pose.Reset(); var before = pose.World(2); var target = pose.World(1); var destination = target.MultiplyPoint3x4(new Vector3(3, -9, 0));
                ProfessionNativeIdleTransform.Apply(pose, new[] { position }); var after = pose.World(2);
                Near(before.m03 * .75f + destination.x * .25f, after.m03); Near(before.m13 * .25f + destination.y * .75f, after.m13);
                Near(before.m00, after.m00); Near(before.m01, after.m01); Near(before.m10, after.m10); Near(before.m11, after.m11);
                Reject<ArgumentException>(() => ProfessionNativeIdleTransform.Apply(pose, new[] { position, complete }));
                complete.Local = true; Reject<NotSupportedException>(() => ProfessionNativeIdleTransform.Apply(pose, new[] { complete })); complete.Local = false;
                complete.Relative = true; Reject<NotSupportedException>(() => ProfessionNativeIdleTransform.Apply(pose, new[] { complete })); complete.Relative = false;
                complete.OffsetScale = Vector2.one; Reject<NotSupportedException>(() => ProfessionNativeIdleTransform.Apply(pose, new[] { complete })); complete.OffsetScale = Vector2.zero;
                complete.Shear = float.NaN; Reject<ArgumentException>(() => ProfessionNativeIdleTransform.Apply(pose, new[] { complete }));
                var invalid = Matrix4x4.identity; invalid.m03 = float.PositiveInfinity; Reject<ArgumentException>(() => pose.OverrideWorld(2, invalid));
                invalid = Matrix4x4.identity; invalid.m20 = 1; Reject<ArgumentException>(() => pose.OverrideWorld(2, invalid));
                pose.Reset(); Require(pose.World(4).m03 != 111, "new source frame clears world constraints");
            }
        }
        private static void Matrix(Matrix4x4 expected, Matrix4x4 actual) { for (int i = 0; i < 16; i++) Near(expected[i], actual[i]); }
        private static void Near(float expected, float actual)
        {
            float error = Mathf.Abs(expected - actual); checks++; maximumError = Mathf.Max(maximumError, error);
            Require(!float.IsNaN(actual) && error <= .001f + .000007f * Mathf.Abs(expected), "source scalar differs: " + expected.ToString("R") + "/" + actual.ToString("R"));
        }
        private static void Reject<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Original idle transforms: " + message); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
