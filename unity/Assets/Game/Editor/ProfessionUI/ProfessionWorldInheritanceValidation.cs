using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionWorldInheritanceValidation
    {
        [Serializable] private sealed class Fixture { public Case[] cases; public Synthetic[] synthetic; }
        [Serializable] private sealed class Case { public string sex; public string[] skins; public Frame[] frames; }
        [Serializable] private sealed class Frame { public string animation; public float time; public World[] expected; }
        [Serializable] private sealed class World { public int bone; public float a, b, c, d, x, y; }
        [Serializable] private sealed class Synthetic { public ProfessionNativeBonePose.Bone[] bones; public World[] expected; }
        [Serializable] private sealed class LocalFixture { public Body[] cases; }
        [Serializable] private sealed class Body { public string sex; public Clip[] clips; }
        [Serializable] private sealed class Clip { public string name; public Track[] tracks; public Sample[] samples; }
        [Serializable] private sealed class Track { public int bone, channel; public ProfessionNativeCurve.Key[] keys; }
        [Serializable] private sealed class Sample { public float time; public Local[] local; }
        [Serializable] private sealed class Local { public float x, y, rotation, scaleX, scaleY; }
        private const string Active = "Zjcs.ProfessionWorldInheritance.Active";
        private static bool ready;
        private static int checks;
        private static float maximumError;
        static ProfessionWorldInheritanceValidation()
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
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("World inheritance validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check();
                Debug.Log("Native world inheritance passed actual Play Mode: " + checks + " scalar checks, maximum absolute difference " + maximumError.ToString("R") + ". Actual packaged idle tracks preserve all 48 original local-pose references; 144 pre-constraint world frames cover both player skeletons, warrior/mage outfits, source map leaves and conformal female sleeves. Independent complex-plane references cover no-scale parent shear/reflection/local scale, and conformal no-rotation/reflection; unsupported nonuniform/sheared no-rotation parents and mode 4 reject explicitly. No transform/physics constraints, mixed animation, actor raster, native page or Windows Player claim.");
                Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            checks = 0; maximumError = 0;
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("world-inheritance-validation.json")));
            var locals = JsonUtility.FromJson<LocalFixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("bone-pose-validation.json")));
            int frames = 0, curves = 0;
            foreach (string sex in new[] { "male", "female" })
            {
                var data = ProfessionNativeSkeletonData.LoadResource(sex);
                var source = locals.cases.Single(c => c.sex == sex);
                var tracks = new Dictionary<string, ProfessionNativeBonePose.Track[]>();
                foreach (var clip in source.clips)
                {
                    var bound = data.CreateBoneTracks(clip.name); tracks.Add(clip.name, bound); curves += bound.Length;
                    Require(bound.Length == clip.tracks.Length, "all original source curve bindings");
                    for (int i = 0; i < bound.Length; i++) Require(bound[i].Bone == clip.tracks[i].bone && bound[i].Channel == clip.tracks[i].channel && bound[i].Curve.KeyCount == clip.tracks[i].keys.Length, "actual packaged channel order, bone and key count");
                    var all = data.CreateBonePose(); all.SetSkinBones(Enumerable.Range(0, data.BoneCount).ToArray());
                    foreach (var sample in clip.samples)
                    {
                        all.Apply(bound, sample.time);
                        for (int i = 0; i < all.Count; i++)
                        {
                            var expected = sample.local[i]; var position = all.LocalPosition(i); var scale = all.LocalScale(i);
                            Near(expected.x, position.x); Near(expected.y, position.y); Near(expected.rotation, all.LocalRotation(i)); Near(expected.scaleX, scale.x); Near(expected.scaleY, scale.y);
                        }
                    }
                }
                foreach (var row in fixture.cases.Where(c => c.sex == sex))
                {
                    var pose = data.CreateBonePose(); var members = new HashSet<int>();
                    foreach (string name in row.skins) foreach (int bone in ((ProfessionNativeSkin)data.SkinCatalog.FindSkin(name)).Bones) members.Add(bone);
                    pose.SetSkinBones(members.ToArray());
                    foreach (var frame in row.frames)
                    {
                        pose.Apply(tracks[frame.animation], frame.time); pose.UpdateWorld();
                        Require(frame.expected.Length == Enumerable.Range(0, pose.Count).Count(pose.IsActive), "exact selected-skin activation and ancestor closure");
                        foreach (var expected in frame.expected) Compare(expected, pose.World(expected.bone));
                        frames++;
                    }
                }
            }
            Require(curves == 1171 && frames == 144 && fixture.cases.Length == 6, "all current original idle and selected-skin references");
            foreach (var row in fixture.synthetic)
            {
                var pose = new ProfessionNativeBonePose(row.bones); pose.UpdateWorld();
                foreach (var expected in row.expected) Compare(expected, pose.World(expected.bone));
                if (row.bones[1].inherit == 2)
                {
                    var before = pose.World(1); pose.SetRotation(0, row.bones[0].rotation + 90); var after = pose.World(1);
                    Near(before.m00, after.m00); Near(before.m01, after.m01); Near(before.m10, after.m10); Near(before.m11, after.m11);
                    var origin = pose.World(0).MultiplyPoint3x4(new Vector3(row.bones[1].x, row.bones[1].y, 0)); Near(origin.x, after.m03); Near(origin.y, after.m13);
                }
            }
            foreach (var parent in new[] { new ProfessionNativeBonePose.Bone { name = "parent", scaleX = 2, scaleY = 3 }, new ProfessionNativeBonePose.Bone { name = "parent", shearX = 20 } })
                Reject(new[] { parent, new ProfessionNativeBonePose.Bone { name = "child", parent = 0, inherit = 2 } });
            Reject(new[] { new ProfessionNativeBonePose.Bone { name = "parent" }, new ProfessionNativeBonePose.Bone { name = "child", parent = 0, inherit = 4 } });
            // Native Unity TRS independently checks unchanged normal inheritance.
            var normal = new[] { new ProfessionNativeBonePose.Bone { name = "p", x = 3, y = -4, rotation = 27, scaleX = -2, scaleY = 3 }, new ProfessionNativeBonePose.Bone { name = "c", parent = 0, x = 5, y = 7, rotation = -43, scaleX = .8f, scaleY = 1.3f } };
            var actual = new ProfessionNativeBonePose(normal); actual.UpdateWorld();
            var expectedMatrix = Matrix4x4.TRS(new Vector3(3, -4, 0), Quaternion.Euler(0, 0, 27), new Vector3(-2, 3, 1)) * Matrix4x4.TRS(new Vector3(5, 7, 0), Quaternion.Euler(0, 0, -43), new Vector3(.8f, 1.3f, 1));
            for (int i = 0; i < 16; i++) Near(expectedMatrix[i], actual.World(1)[i]);
        }
        private static void Compare(World expected, Matrix4x4 actual)
        { Near(expected.a, actual.m00); Near(expected.b, actual.m01); Near(expected.c, actual.m10); Near(expected.d, actual.m11); Near(expected.x, actual.m03); Near(expected.y, actual.m13); }
        private static void Near(float expected, float actual)
        {
            float error = Mathf.Abs(expected - actual); checks++; maximumError = Mathf.Max(maximumError, error);
            Require(!float.IsNaN(actual) && error <= .0004f + .000007f * Mathf.Abs(expected), "world/local scalar differs: " + expected.ToString("R") + "/" + actual.ToString("R"));
        }
        private static void Reject(ProfessionNativeBonePose.Bone[] bones)
        { bool rejected = false; try { new ProfessionNativeBonePose(bones).UpdateWorld(); } catch (NotSupportedException) { rejected = true; } Require(rejected, "unsupported original inheritance must not be approximated"); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Native world inheritance: " + message); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
