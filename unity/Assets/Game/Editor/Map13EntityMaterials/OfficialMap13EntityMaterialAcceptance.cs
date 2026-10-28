using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13EntityMaterials
{
    [InitializeOnLoad]
    public static class OfficialMap13EntityMaterialAcceptance
    {
        private const string Key = "Map13.NativeMaterialGpu";
        private static bool started, loggedError;
        static OfficialMap13EntityMaterialAcceptance()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
                Application.logMessageReceived -= Observe;
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
            };
        }
        public static void ImportAndRun()
        {
            try { OfficialMap13EntityMaterialTools.RestoreAndValidate(); Run(); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 900);
            SessionState.SetString(Key + ".source", OfficialMap13EntityMaterialTools.Hash(File.ReadAllBytes(Path.Combine(OfficialMap13EntityMaterialTools.Repository, OfficialMap13EntityMaterialTools.SourcePath))));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) loggedError = true; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map13 native material GPU checks timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13 original material GPU fixture").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            Application.logMessageReceived += Observe;
            var steps = VerifySteps();
            while (true)
            {
                object next;
                try { if (!steps.MoveNext()) break; next = steps.Current; }
                catch (Exception exception) { Debug.LogException(exception); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (loggedError) { EditorApplication.isPlaying = false; yield break; }
            SessionState.SetInt(Key + ".result", 0); EditorApplication.isPlaying = false;
        }
        private static IEnumerator VerifySteps()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            string source = Path.Combine(OfficialMap13EntityMaterialTools.Repository, OfficialMap13EntityMaterialTools.SourcePath);
            if (OfficialMap13EntityMaterialTools.Hash(File.ReadAllBytes(source)) != SessionState.GetString(Key + ".source", ""))
                throw new InvalidDataException("Original material source changed during Play Mode transition.");
            var entries = OfficialMap13EntityMaterialTools.ReadEntries();
            var catalog = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-entity-materials-catalog");
            if (!catalog || catalog.Bindings.Length != 1843) throw new InvalidDataException("Runtime material catalog differs.");
            int fields = 0;
            for (int index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                fields += OfficialMap13EntityMaterialTools.Validate(entry, catalog.Load<Material>(OfficialMap13EntityMaterialTools.Identity(entry)));
                if ((index + 1) % 128 == 0) yield return null;
            }
            bool asynchronous = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            try
            {
                int pixels = VerifyEngineShaders(entries, catalog);
                CaptureOriginalMaterials(entries, catalog);
                if (loggedError) throw new InvalidDataException("Runtime errors occurred during original material GPU checks.");
                OfficialMap13EntityMaterialTools.RecordPlayModeValidation(fields,pixels);
                Debug.Log($"Map13 native material Play Mode passed: 1843 CAB/PathID bindings, {fields} exact original properties; {pixels} engine shader pixels compared to original ordered equations, four original textured materials rendered. XR/MSAA execution, font initialization, entity geometry, input and saves are not covered.");
            }
            finally { ShaderUtil.allowAsyncCompilation = asynchronous; }
        }
        private static int VerifyEngineShaders(OfficialMap13EntityMaterialTools.Entry[] entries, NativeMap13AssetCatalog catalog)
        {
            var guiSource = entries.Single(entry => entry.cab == "unity default resources" && entry.path_id == "10100");
            var debugSource = entries.Single(entry => entry.cab == "unity default resources" && entry.path_id == "10756");
            var gui = new Material(catalog.Load<Material>(OfficialMap13EntityMaterialTools.Identity(guiSource)));
            var debug = new Material(catalog.Load<Material>(OfficialMap13EntityMaterialTools.Identity(debugSource)));
            var texture = new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var array = new Texture2DArray(2, 2, 2, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var cube = new Cubemap(2, TextureFormat.RGBAFloat, false) { filterMode = FilterMode.Point };
            var mesh = Quad(); var target = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create(); var readback = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);
            var colors = new[] { new Color(.2f,.4f,.7f,.31234567f), new Color(.73f,.6f,.1f,.73127f), new Color(.8f,.3f,.2f,.42351f), new Color(.1f,.2f,.5f,.93719f) };
            int compared = 0;
            try
            {
                texture.SetPixels(colors); texture.Apply();
                array.SetPixels(Enumerable.Repeat(Color.black, 4).ToArray(), 0); array.SetPixels(colors, 1); array.Apply();
                foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
                    cube.SetPixels(Enumerable.Repeat(face == CubemapFace.PositiveX ? colors[0] : Color.magenta, 4).ToArray(), face);
                cube.Apply();
                foreach (Color vertex in new[] { new Color(.71f,.43f,.29f,.61f), new Color(.23f,.79f,.37f,.83f) })
                for (int index = 0; index < 4; index++)
                {
                    mesh.colors = Enumerable.Repeat(vertex, 4).ToArray();
                    mesh.SetUVs(0, Enumerable.Repeat(new Vector3(index % 2 * .5f + .25f, index / 2 * .5f + .25f, 0), 4).ToList());
                    gui.mainTexture = texture; gui.SetColor("_Color", Color.white);
                    float alpha = colors[index].a * vertex.a;
                    Color expected = new Color(vertex.r * alpha, vertex.g * alpha, vertex.b * alpha, alpha * alpha);
                    compared += Compare(Draw(mesh, gui, Matrix4x4.identity, target, readback), expected, "GUI original alpha/vertex-color/blend");
                }
                mesh.colors = Enumerable.Repeat(Color.white, 4).ToArray();
                foreach (string mode in new[] { "2D", "array", "cube" })
                foreach (bool undo in new[] { false, true })
                foreach (bool single in new[] { false, true })
                foreach (bool flip in new[] { false, true })
                {
                    debug.shaderKeywords = Array.Empty<string>();
                    if (mode == "array") debug.EnableKeyword("_TEX2DARRAY");
                    if (mode == "cube") debug.EnableKeyword("_CUBEMAP");
                    debug.SetTexture("_MainTex", mode == "array" ? (Texture)array : mode == "cube" ? cube : texture);
                    debug.SetFloat("_MainTexWidth", 2); debug.SetFloat("_MainTexHeight", 2);
                    debug.SetInt("_ShouldYFlip", flip ? 1 : 0); debug.SetInt("_UndoOutputSRGB", undo ? 1 : 0);
                    debug.SetVector("_Levels", new Vector4(.1f,.9f,0,0));
                    Vector4 channels = single ? new Vector4(0,0,1,0) : new Vector4(1,1,1,1);
                    debug.SetVector("_Channels", channels);
                    Vector3 uv = mode == "cube" ? new Vector3(1,.25f,0) : new Vector3(.25f,.25f,mode == "array" ? 1 : 0);
                    mesh.SetUVs(0, Enumerable.Repeat(uv, 4).ToList());
                    Color sampled = colors[mode != "cube" && flip ? 2 : 0];
                    Color expected = (sampled - new Color(.1f,.1f,.1f,.1f)) / .8f;
                    for (int channel = 0; channel < 4; channel++) expected[channel] *= channels[channel];
                    if (single) expected = new Color(expected.b,expected.b,expected.b,expected.b);
                    if (undo) for (int channel = 0; channel < 3; channel++) expected[channel] = Polynomial(Mathf.Clamp01(expected[channel]));
                    compared += Compare(Draw(mesh, debug, Matrix4x4.identity, target, readback), expected, "FrameDebugger " + mode + "/" + undo + "/" + single + "/" + flip);
                }
                Save(readback, "native-engine-material-gpu.png");
            }
            finally
            {
                foreach (var obj in new UnityEngine.Object[] { gui, debug, texture, array, cube, mesh, readback }) UnityEngine.Object.Destroy(obj);
                target.Release(); UnityEngine.Object.Destroy(target);
            }
            return compared;
        }
        private static void CaptureOriginalMaterials(OfficialMap13EntityMaterialTools.Entry[] entries, NativeMap13AssetCatalog catalog)
        {
            string[] names = { "map_fangJianBei_A_1_Material", "map_fangJianBei_A_2_Material", "map_fangJianBei_C_2_Material", "map_huangQuanGe_C_Material" };
            var mesh = Quad(); mesh.colors = Enumerable.Repeat(Color.white, 4).ToArray();
            var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create(); var readback = new Texture2D(512, 512, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active; var commands = new CommandBuffer();
            try
            {
                commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1,1,-1,1,-10,10), true));
                commands.SetGlobalFloat("_UseSceneLighting", 0); commands.SetGlobalFloat("_HDREmulationScale", 1);
                for (int index = 0; index < names.Length; index++)
                {
                    var entry = entries.Single(item => item.name == names[index]);
                    var material = catalog.Load<Material>(OfficialMap13EntityMaterialTools.Identity(entry));
                    commands.SetViewport(new Rect(index % 2 * 256, index / 2 * 256, 256, 256));
                    commands.SetGlobalVector("_ScreenParams", new Vector4(256,256,1+1f/256,1+1f/256));
                    commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0);
                }
                Graphics.ExecuteCommandBuffer(commands); RenderTexture.active = target;
                readback.ReadPixels(new Rect(0,0,512,512),0,0); readback.Apply();
                var pixels = readback.GetPixels();
                for (int index = 0; index < 4; index++)
                {
                    int lit = 0;
                    for (int y = index / 2 * 256; y < index / 2 * 256 + 256; y++)
                    for (int x = index % 2 * 256; x < index % 2 * 256 + 256; x++)
                    { Color pixel = pixels[y * 512 + x]; if (pixel.a > .01f && Mathf.Max(pixel.r, pixel.g, pixel.b) > .01f) lit++; }
                    if (lit < 128) throw new InvalidDataException("Original textured material rendered no useful coverage: " + names[index]);
                }
                Save(readback, "original-native-materials-gpu.png");
            }
            finally { RenderTexture.active = previous; commands.Release(); target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(mesh); UnityEngine.Object.Destroy(readback); }
        }
        private static Mesh Quad() => new Mesh { vertices = new[] { new Vector3(-1,-1),new Vector3(1,-1),new Vector3(1,1),new Vector3(-1,1) }, uv = new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up }, triangles = new[] { 0,1,2,0,2,3 } };
        private static Color[] Draw(Mesh mesh, Material material, Matrix4x4 model, RenderTexture target, Texture2D readback)
        {
            var commands = new CommandBuffer(); var previous = RenderTexture.active;
            try
            {
                commands.SetRenderTarget(target); commands.SetViewport(new Rect(0,0,target.width,target.height)); commands.ClearRenderTarget(true,true,Color.clear);
                commands.SetViewProjectionMatrices(Matrix4x4.identity,GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1,1,-1,1,-10,10),true));
                commands.DrawMesh(mesh,model,material,0,0); Graphics.ExecuteCommandBuffer(commands); RenderTexture.active=target;
                readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0); readback.Apply(); return readback.GetPixels();
            }
            finally { RenderTexture.active=previous; commands.Release(); }
        }
        private static int Compare(Color[] pixels, Color expected, string label)
        {
            foreach (Color actual in pixels) for (int channel=0;channel<4;channel++)
                if (float.IsNaN(actual[channel]) || Mathf.Abs(actual[channel]-expected[channel]) > .00002f)
                    throw new InvalidDataException(label + ": " + actual + " != " + expected);
            return pixels.Length;
        }
        private static float Polynomial(float value) => value*(value*(value*.305306017f+.682171106f)+.0125228781f);
        private static void Save(Texture2D source,string name)
        {
            string folder=Path.GetFullPath(".tools/map13-entity-materials"); Directory.CreateDirectory(folder);
            var png=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
            try { png.SetPixels(source.GetPixels()); png.Apply(); File.WriteAllBytes(Path.Combine(folder,name),png.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(png); }
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
