using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeAtlasGeometryValidation
    {
        [Serializable] private sealed class Payload { public Body[] cases; }
        [Serializable] private sealed class Body { public string sex; public Region[] regions; public Attachment[] attachments; }
        [Serializable] private sealed class Region { public string name; public int[] size, bounds, offsets; public int rotate; public Vector2[] cornerUvs; }
        [Serializable] private sealed class Attachment
        {
            public string name; public int region; public bool mesh; public float[] uvs; public Vector2[] expectedUvs, expectedCorners;
            public float width, height, x, y, scaleX, scaleY, rotation;
        }
        private const string Active = "Zjcs.ProfessionAtlasGeometry.Active";
        private static bool ready;
        private static float maxUv, maxPosition;
        static ProfessionNativeAtlasGeometryValidation()
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
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Atlas fixture did not enter Play Mode.");
                if (!ready || !EditorApplication.isPlaying) return;
                var source = JsonUtility.FromJson<Payload>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("atlas-geometry-validation.json")));
                Require(source.cases.Length == 2, "both source atlases");
                int regions = 0, meshes = 0, rectangles = 0, uvCount = 0, cornerCount = 0;
                foreach (Body body in source.cases)
                {
                    var atlas = new ProfessionNativeAtlasRegion[body.regions.Length];
                    for (int i = 0; i < atlas.Length; i++)
                    {
                        Region r = body.regions[i];
                        atlas[i] = new ProfessionNativeAtlasRegion(r.size[0], r.size[1], r.bounds[0], r.bounds[1], r.bounds[2], r.bounds[3], r.offsets[0], r.offsets[1], r.offsets[2], r.offsets[3], r.rotate);
                        Compare(atlas[i].CornerUvs(), r.cornerUvs, true, body.sex + "/" + r.name); regions++;
                    }
                    foreach (Attachment attachment in body.attachments)
                    {
                        var r = atlas[attachment.region];
                        if (attachment.mesh)
                        {
                            var actual = r.MapMeshUvs(attachment.uvs);
                            Compare(actual, attachment.expectedUvs, true, attachment.name); uvCount += actual.Length; meshes++;
                        }
                        else
                        {
                            var actual = r.LocalCorners(attachment.width, attachment.height, attachment.x, attachment.y, attachment.scaleX, attachment.scaleY, attachment.rotation);
                            Compare(actual, attachment.expectedCorners, false, attachment.name); cornerCount += actual.Length; rectangles++;
                        }
                    }
                }
                Synthetic();
                Debug.Log($"Profession original atlas geometry passed: {regions} regions, {meshes} source meshes / {uvCount} UVs, {rectangles} rectangle attachments / {cornerCount} corners. Maximum UV difference {maxUv:R}, source-coordinate difference {maxPosition:R}. All quarter turns, trim, attachment transforms and invalid inputs pass. Production texture sampling, real character poses/rendering and full profession UI are not covered.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Compare(Vector2[] actual, Vector2[] expected, bool uv, string label)
        {
            Require(actual.Length == expected.Length, "complete affine reference");
            for (int i = 0; i < actual.Length; i++) for (int axis = 0; axis < 2; axis++)
            {
                float error = Mathf.Abs(actual[i][axis] - expected[i][axis]);
                if (uv) maxUv = Mathf.Max(maxUv, error); else maxPosition = Mathf.Max(maxPosition, error);
                Require(error <= (uv ? .000002f : .0004f + .000007f * Mathf.Abs(expected[i][axis])), label + "/" + i + "/" + axis + " error " + error.ToString("R"));
            }
        }
        private static void Synthetic()
        {
            var r = new ProfessionNativeAtlasRegion(100, 100, 10, 20, 20, 10, 5, 2, 40, 20, 90);
            Compare(r.CornerUvs(), new[] { new Vector2(.2f,.6f), new Vector2(.1f,.6f), new Vector2(.1f,.8f), new Vector2(.2f,.8f) }, true, "quarter-turn pixel corners");
            Compare(r.LocalCorners(80, 40, 3, -2, -1, 2, 90), new[] { new Vector2(35,28), new Vector2(-5,28), new Vector2(-5,-12), new Vector2(35,-12) }, false, "trimmed rectangle reflected then rotated");
            Throws<ArgumentException>(() => new ProfessionNativeAtlasRegion(100,100,99,0,20,10,0,0,20,10,90));
            Throws<ArgumentException>(() => new ProfessionNativeAtlasRegion(100,100,0,0,20,10,0,0,20,10,45));
            Throws<ArgumentException>(() => r.Map(new Vector2(float.NaN,0)));
            Throws<ArgumentException>(() => r.MapMeshUvs(new float[] { 1 }));
            Throws<ArgumentException>(() => r.LocalCorners(float.PositiveInfinity,1,0,0,1,1,0));
        }
        private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
