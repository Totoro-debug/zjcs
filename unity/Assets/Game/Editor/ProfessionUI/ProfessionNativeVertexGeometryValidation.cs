using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeVertexGeometryValidation
    {
        [Serializable] private sealed class Payload { public Body[] cases; }
        [Serializable] private sealed class Body { public string sex; public ProfessionNativeBonePose.Bone[] bones; public Attachment[] attachments; }
        [Serializable] private sealed class Attachment
        {
            public string name; public int slotBone, count, influences; public bool weighted;
            public float[] vertices; public Vector2[] expected;
        }
        private const string Active = "Zjcs.ProfessionVertexGeometry.Active";
        private static bool ready;
        static ProfessionNativeVertexGeometryValidation()
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
                SessionState.SetBool(Active, true); SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 180));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Vertex fixture did not enter Play Mode.");
                if (!ready || !EditorApplication.isPlaying) return;
                var source = JsonUtility.FromJson<Payload>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("vertex-geometry-validation.json")));
                Require(source.cases.Length == 2, "both original skeletons");
                int attachments = 0, vertices = 0, influences = 0; float maximum = 0;
                foreach (Body body in source.cases)
                {
                    var pose = new ProfessionNativeBonePose(body.bones);
                    foreach (Attachment attachment in body.attachments)
                    {
                        var geometry = new ProfessionNativeVertexAttachment(attachment.vertices, attachment.count, attachment.slotBone, pose.Count);
                        Require(geometry.VertexCount == attachment.count && geometry.InfluenceCount == attachment.influences && geometry.Weighted == attachment.weighted, "source vertex layout");
                        var output = new Vector2[attachment.count]; geometry.Evaluate(pose, output);
                        Require(output.Length == attachment.expected.Length, "complete reference geometry");
                        for (int i = 0; i < output.Length; i++)
                        {
                            for (int axis = 0; axis < 2; axis++)
                            {
                                float expected = attachment.expected[i][axis], error = Mathf.Abs(output[i][axis] - expected);
                                maximum = Mathf.Max(maximum, error);
                                Require(error <= .0004f + .000007f * Mathf.Abs(expected), body.sex + "/" + attachment.name + "/" + i + "/" + axis + " error " + error.ToString("R"));
                            }
                        }
                        attachments++; vertices += geometry.VertexCount; influences += geometry.InfluenceCount;
                    }
                }
                Synthetic();
                Debug.Log($"Profession native source vertex geometry passed: {attachments} original attachments, {vertices} vertices, {influences} influences; maximum absolute coordinate difference {maximum:R}. Synthetic reflected/sheared/translated affine references, unchanged weight sums, slot-bone geometry, zero-weight inactive bones, ownership and invalid layouts pass. Original animation poses, deformation, atlas UVs, real character rendering and full profession UI are not validated here.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Synthetic()
        {
            var pose = new ProfessionNativeBonePose(new[] {
                new ProfessionNativeBonePose.Bone { name = "a", x = 10, y = -4 },
                new ProfessionNativeBonePose.Bone { name = "b", x = -2, y = 6, skinRequired = true } });
            float[] raw = { 2, 0, 2, 3, .7f, 1, -3, 4, .7f };
            var geometry = new ProfessionNativeVertexAttachment(raw, 1, 0, 2);
            var result = new Vector2[1];
            Throws<InvalidOperationException>(() => geometry.Evaluate(pose, result));
            pose.SetSkinBones(new[] { 1 }); raw[2] = 500;
            geometry.Evaluate(pose, result);
            Require(Vector2.Distance(result[0], new Vector2(4.9f, 6.3f)) < .00001f, "original non-normalized .7 + .7 weights and raw-data ownership");
            pose.SetSkinBones(Array.Empty<int>());
            new ProfessionNativeVertexAttachment(new float[] { 2, 0, 2, 3, 1, 1, 0, 0, 0 }, 1, 0, 2).Evaluate(pose, result);
            Require(Vector2.Distance(result[0], new Vector2(12, -1)) < .00001f, "zero-weight inactive influence");
            new ProfessionNativeVertexAttachment(new float[] { 2, 3 }, 1, 0, 2).Evaluate(pose, result);
            Require(Vector2.Distance(result[0], new Vector2(12, -1)) < .00001f, "unweighted uses slot bone");
            foreach (float[] invalid in new[] {
                new float[] { 1, 0, 2, 3 }, new float[] { 0 }, new float[] { 1.5f, 0, 0, 0, 1 },
                new float[] { 1, 2, 0, 0, 1 }, new float[] { 1, 0, 0, 0, -1 },
                new float[] { 1, 0, 0, 0, 1.01f }, new float[] { 1, 0, float.NaN, 0, 1 },
                new float[] { 1, 0, 0, 0, 1, 7 }, new float[] { float.PositiveInfinity, 0 } })
                Throws<ArgumentException>(() => new ProfessionNativeVertexAttachment(invalid, 1, 0, 2));
            Throws<ArgumentException>(() => geometry.Evaluate(pose, new Vector2[2]));
        }
        private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
