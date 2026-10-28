using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeWeaponResourceValidation
    {
        [Serializable] private sealed class Counts { public int bones, slots, skins, animations, ik, transform, physics; }
        [Serializable] private sealed class Matrix { public int bone; public float a, b, c, d, x, y; }
        [Serializable] private sealed class Frame { public string animation; public float time; public Matrix[] expected; }
        [Serializable] private sealed class Mip { public int level, width, height, rgba_size; public string rgba_sha256; }
        [Serializable] private sealed class Texture { public string path; public Mip[] mips; }
        [Serializable] private sealed class Fixture { public Frame[] frames; public Texture[] textures; public Counts counts; public string modelPath, modelGuid; }
        [Serializable] private sealed class Program { public string[] keywords; }
        [Serializable] private sealed class Proof { public Program[] programs; }
        private const string Active = "Zjcs.ProfessionNativeWeaponResources.Active";
        private static bool ready;
        static ProfessionNativeWeaponResourceValidation()
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
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 180));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Weapon resources validation timed out.");
                if (!ready || !EditorApplication.isPlaying) return;
                Check(); Finish(0);
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Check()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("native-weapon-resources-validation.json")));
            var catalog = Resources.Load<ProfessionNativeWeaponCatalog>("ProfessionUI/weapon-resources");
            Require(catalog && catalog.Model.SourcePath == fixture.modelPath && catalog.Model.SourceGuid == fixture.modelGuid, "actual original model path/GUID binding");
            var data = catalog.Model.Data;
            Require(ReferenceEquals(data, catalog.Model.Data) && data.Sex == "primary-weapon" && data.BoneCount == fixture.counts.bones && data.SlotCount == fixture.counts.slots && data.SkinCount == fixture.counts.skins && data.AnimationCount == fixture.counts.animations, "lossless weapon payload counts and shared identity");
            Require(data.Constraints.Count(c => c.Kind == "ik") == fixture.counts.ik && data.Constraints.Count(c => c.Kind == "transform") == fixture.counts.transform && data.Constraints.Count(c => c.Kind == "physics") == fixture.counts.physics, "all original constraints retained");
            int mipCount = 0, stages = 0, matrices = 0, frames = 0, images = 0;
            long bytesCount = 0; float maximum = 0;
            foreach (var expected in fixture.textures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(expected.path);
                Require(texture, "actual native texture asset " + expected.path);
                foreach (var mip in expected.mips)
                {
                    Require(Math.Max(1, texture.width >> mip.level) == mip.width && Math.Max(1, texture.height >> mip.level) == mip.height, "source texture mip dimensions");
                    var readback = AsyncGPUReadback.Request(texture, mip.level); readback.WaitForCompletion();
                    Require(!readback.hasError, "actual weapon GPU texture readback");
                    byte[] bytes = readback.GetData<byte>().ToArray();
                    using (var sha = SHA256.Create()) Require(bytes.Length == mip.rgba_size && BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() == mip.rgba_sha256, "exact source weapon mip bytes on GPU");
                    bytesCount += bytes.Length; mipCount++;
                }
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/ProfessionUI/SpineSkeletonGraphicHeightClip.shader");
            var proof = JsonUtility.FromJson<Proof>(File.ReadAllText(Path.Combine(ProfessionValidationWorkspace.Repository, "assets/reference/profession-ui/weapon-resources-source-manifest.json")));
            Require(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "translated original height-clip shader");
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var program in proof.programs)
                foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                {
                    var compiled = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(0).CompileVariant(stage, program.keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                    Require(compiled.Success, "original weapon shader variant " + stage + " " + string.Join(",", program.keywords)); stages++;
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = async; }
            foreach (string skin in new[] { "pWeapon_100_G_007", "pWeapon_200_G_007" })
            {
                foreach (string suffix in new[] { "_UI", "_UI_Integrated" })
                {
                    string path = "Assets/Prefabs/Appearance/PlayerPart/" + skin + "/" + skin + suffix + ".prefab";
                    var source = catalog.FindPrefabSource(path);
                    Require(source.Hierarchy && source.Hierarchy.text.Contains("\"path\":\"" + path + "\"") && source.Guid.Length == 32 && source.Hierarchy.text.Contains("SkeletonGraphicFollower"), "complete original UI source hierarchy metadata");
                }
                var state = new ProfessionNativeAppearanceSkeleton(data);
                state.SetSkin(data.SkinCatalog.FindSkin(skin)); state.SetToSetupPose();
                Require(state.SkinConstraints.Count == 0, "default weapon skin has no active constraints");
                using (var batches = new ProfessionNativeMeshBatches(data.SlotCount))
                {
                    foreach (var frame in fixture.frames.Reverse())
                    {
                        state.EvaluateUnmixedWeaponAnimation(frame.animation, frame.time);
                        foreach (var expected in frame.expected)
                        {
                            var actual = state.Pose.World(expected.bone);
                            var values = new[] { actual.m00, actual.m01, actual.m10, actual.m11, actual.m03, actual.m13 };
                            var targets = new[] { expected.a, expected.b, expected.c, expected.d, expected.x, expected.y };
                            for (int i = 0; i < values.Length; i++)
                            {
                                float delta = Mathf.Abs(values[i] - targets[i]); maximum = Mathf.Max(maximum, delta);
                                Require(delta <= .001f + .000007f * Mathf.Abs(targets[i]), "independent original weapon world matrix: " + skin + " " + frame.animation + " t=" + frame.time + " bone=" + expected.bone + " field=" + i + " actual=" + values[i] + " expected=" + targets[i]);
                            }
                            matrices++;
                        }
                        batches.Build(state.Pose, state.CreateDrawSlots());
                        Require(batches.Batches.Count == 1 && batches.Batches[0].Mesh.vertexCount == 4, "actual default weapon region geometry");
                        var batch = batches.Batches[0]; var page = catalog.FindPage(batch.Page);
                        Require(page.Main && page.Placeholder && page.Material && page.MainGuid.Length == 32, "original high texture/page asset bindings");
                        Require(page.Material.shader == shader, "original weapon material shader binding: " + page.Material.shader.name);
                        Require(page.Material.mainTexture == page.Main, "original weapon material texture binding: " + (page.Material.mainTexture ? page.Material.mainTexture.name : "null"));
                        Require(page.Material.IsKeywordEnabled("_STRAIGHT_ALPHA_INPUT") && page.Material.GetFloat("_WorldClipHeight") == -10000, "original weapon material keyword and height");
                        if (frame.time == .5f && frame.animation == (skin.Contains("100") ? "100/100_loop" : "200/200_loop"))
                        { Raster(batch, page.Material, skin); images++; }
                        frames++;
                    }
                }
                var constrained = data.SkinNames.Select(n => data.SkinCatalog.FindSkin(n)).OfType<ProfessionNativeSkin>().First(s => s.Constraints.Count > 0);
                state.SetSkin(constrained);
                bool rejected = false; try { state.EvaluateUnmixedWeaponAnimation("100/100_loop", 0); } catch (NotSupportedException) { rejected = true; }
                Require(rejected, "active weapon constraints are explicitly refused");
            }
            Require(frames == 96 && images == 2 && mipCount == 4 && stages == 32, "full source weapon validation coverage");
            Debug.Log("Original weapon resource Play Mode passed: 4 source UI hierarchies, 364 bones/17 slots/399 skins/6 inline actions and all 224 constraints retained; " + frames + " unconstrained default-weapon frames, " + matrices + " independent world matrices, maximum difference " + maximum + "; " + mipCount + " exact GPU mip hashes (" + bytesCount + " bytes), " + stages + " Windows D3D shader stages, 2 real weapon GPU captures. Production part prefabs/Graphic/AppearanceUILite/pool and full-page input/save/Windows release remain incomplete.");
        }
        private static void Raster(ProfessionNativeMeshBatches.Batch batch, Material material, string name)
        {
            var bounds = batch.Mesh.bounds; float extent = Mathf.Max(bounds.size.x, bounds.size.y) * .56f;
            var fit = Matrix4x4.Scale(new Vector3(1 / extent, 1 / extent, 1)) * Matrix4x4.Translate(-bounds.center);
            var target = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active; Texture2D capture = null;
            try
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    commands.DrawMesh(batch.Mesh, fit, material); Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = target; capture = new Texture2D(640, 640, TextureFormat.RGBA32, false, true);
                capture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); capture.Apply();
                Require(capture.GetPixels32().Count(p => p.a > 200) > 2000, "visible actual original weapon raster");
                File.WriteAllBytes(ProfessionValidationWorkspace.Output("native-weapon-" + name + ".png"), capture.EncodeToPNG());
            }
            finally { if (capture) UnityEngine.Object.DestroyImmediate(capture); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(int code) { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
