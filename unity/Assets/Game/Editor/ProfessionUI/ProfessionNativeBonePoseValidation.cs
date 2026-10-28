using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeBonePoseValidation
    {
        [Serializable] private sealed class Payload { public Case[] cases; }
        [Serializable] private sealed class Case { public string sex; public ProfessionNativeBonePose.Bone[] bones; public Clip[] clips; }
        [Serializable] private sealed class Clip { public string name; public Track[] tracks; public Sample[] samples; }
        [Serializable] private sealed class Track { public int bone, channel; public ProfessionNativeCurve.Key[] keys; }
        [Serializable] private sealed class Sample { public float time; public Local[] local; }
        [Serializable] private sealed class Local { public float x, y, rotation, scaleX, scaleY; }
        private const string Active = "Zjcs.ProfessionBonePose.Active";
        private static bool ready;
        private static float maximumError;
        private static int checks;

        static ProfessionNativeBonePoseValidation()
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
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Bone pose fixture did not enter Play Mode.");
                if (!ready || !EditorApplication.isPlaying) return;
                ValidateWorld();
                var source = JsonUtility.FromJson<Payload>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("bone-pose-validation.json")));
                Require(source.cases.Length == 2, "both original character skeletons");
                int samples = 0, bones = 0, curves = 0;
                foreach (Case body in source.cases)
                {
                    bones += body.bones.Length;
                    var pose = new ProfessionNativeBonePose(body.bones);
                    pose.SetSkinBones(Enumerable.Range(0, pose.Count).ToArray());
                    Require(body.clips.Length == 3, "all three original idle clips");
                    foreach (Clip clip in body.clips)
                    {
                        var tracks = clip.tracks.Select(t => new ProfessionNativeBonePose.Track(t.bone, t.channel, new ProfessionNativeCurve(t.keys))).ToArray();
                        curves += tracks.Length;
                        foreach (Sample sample in clip.samples)
                        {
                            samples++;
                            pose.Apply(tracks, sample.time);
                            Require(sample.local.Length == pose.Count, "original local transform inventory");
                            for (int i = 0; i < pose.Count; i++)
                            {
                                Local expected = sample.local[i];
                                Near(expected.x, pose.LocalPosition(i).x, body.sex + "/" + clip.name + "/x");
                                Near(expected.y, pose.LocalPosition(i).y, "y");
                                Near(expected.rotation, pose.LocalRotation(i), "rotation");
                                Near(expected.scaleX, pose.LocalScale(i).x, "scaleX");
                                Near(expected.scaleY, pose.LocalScale(i).y, "scaleY");
                            }
                        }
                    }
                }
                Debug.Log($"Profession native bone pose passed: {bones} original bones, {curves} source curves, {samples} local poses, {checks} scalar checks, maximum absolute local difference {maximumError:R}. Synthetic normal affine chains match Unity transforms; translation-only, shear, skin activation/ancestors, source ownership and explicit unsupported inheritance rejection passed. Full source world poses, constraints, character geometry and profession UI are not validated.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }

        private static void ValidateWorld()
        {
            var bones = new[] {
                new ProfessionNativeBonePose.Bone { name = "root", x = 3, y = -4, rotation = 27, scaleX = -2, scaleY = 3 },
                new ProfessionNativeBonePose.Bone { name = "child", parent = 0, x = 5, y = 7, rotation = -43, scaleX = .8f, scaleY = 1.3f },
                new ProfessionNativeBonePose.Bone { name = "tip", parent = 1, x = -1, y = 2, rotation = 19, scaleX = 2, scaleY = -.6f }
            };
            var pose = new ProfessionNativeBonePose(bones);
            var objects = new GameObject[bones.Length];
            try
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    objects[i] = new GameObject("AffineFixture" + i);
                    if (i > 0) objects[i].transform.SetParent(objects[bones[i].parent].transform, false);
                    var b = bones[i]; objects[i].transform.localPosition = new Vector3(b.x, b.y, 0);
                    objects[i].transform.localRotation = Quaternion.Euler(0, 0, b.rotation);
                    objects[i].transform.localScale = new Vector3(b.scaleX, b.scaleY, 1);
                }
                pose.UpdateWorld();
                for (int i = 0; i < bones.Length; i++) MatrixNear(objects[i].transform.localToWorldMatrix, pose.World(i));
            }
            finally { foreach (var go in objects.Reverse()) if (go) Object.DestroyImmediate(go); }
            bones[1].inherit = 1;
            pose = new ProfessionNativeBonePose(bones); pose.UpdateWorld();
            Matrix4x4 translated = Matrix4x4.TRS(pose.World(0).MultiplyPoint3x4(new Vector3(5, 7, 0)), Quaternion.Euler(0, 0, -43), new Vector3(.8f, 1.3f, 1));
            MatrixNear(translated, pose.World(1));
            var shear = new ProfessionNativeBonePose(new[] { new ProfessionNativeBonePose.Bone { name = "shear", shearX = 30, shearY = -15, scaleX = 2, scaleY = 3 } });
            shear.UpdateWorld(); Near(Mathf.Sqrt(3), shear.World(0).m00, "shear X"); Near(1, shear.World(0).m10, "shear X sine");
            Near(.7764571353f, shear.World(0).m01, "shear Y cosine"); Near(2.897777479f, shear.World(0).m11, "shear Y sine");
            var required = new[] { new ProfessionNativeBonePose.Bone { name = "base" },
                new ProfessionNativeBonePose.Bone { name = "requiredParent", parent = 0, skinRequired = true },
                new ProfessionNativeBonePose.Bone { name = "requiredChild", parent = 1, skinRequired = true },
                new ProfessionNativeBonePose.Bone { name = "unselected", parent = 0, skinRequired = true, inherit = 4 } };
            pose = new ProfessionNativeBonePose(required); pose.UpdateWorld(); Require(!pose.IsActive(1) && !pose.IsActive(2), "unselected skin bones");
            pose.SetSkinBones(new[] { 2 }); Require(pose.IsActive(1) && pose.IsActive(2) && !pose.IsActive(3), "selected ancestors only"); pose.UpdateWorld();
            required[0].x = 100; pose.Reset(); Require(pose.LocalPosition(0).x == 0, "source setup ownership");
            pose.SetSkinBones(new[] { 3 }); Throws<NotSupportedException>(() => pose.UpdateWorld());
            Throws<ArgumentOutOfRangeException>(() => pose.SetSkinBones(new[] { 9 }));
            Throws<ArgumentException>(() => pose.Apply(Array.Empty<ProfessionNativeBonePose.Track>(), float.NaN));
            Throws<ArgumentException>(() => new ProfessionNativeBonePose(new[] { new ProfessionNativeBonePose.Bone { name = "invalid", parent = 0 } }));
        }

        private static void MatrixNear(Matrix4x4 expected, Matrix4x4 actual)
        { for (int i = 0; i < 16; i++) Near(expected[i], actual[i], "Unity affine matrix"); }
        private static void Near(float expected, float actual, string label)
        {
            float error = Mathf.Abs(expected - actual); maximumError = Mathf.Max(maximumError, error); checks++;
            Require(!float.IsNaN(actual) && error <= .0004f + .000007f * Mathf.Abs(expected), label + " differs: " + expected.ToString("R") + " vs " + actual.ToString("R"));
        }
        private static void Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
