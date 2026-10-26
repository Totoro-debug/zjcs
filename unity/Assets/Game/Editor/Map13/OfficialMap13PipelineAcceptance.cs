using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13
{
    // Checks native layer resolution and procedural GPU transfer on fixtures.
    public static class OfficialMap13PipelineAcceptance
    {
        private const string Key = "Zjcs.Map13.PipelineExecution";
        private static bool started;
        [Serializable] private sealed class Mip { public int level; public string rgba_sha256; }
        [Serializable] private sealed class Texture { public string path; public Mip[] mips; }
        [Serializable] private sealed class SceneSource { public Texture[] textures; }

        public static void Run()
        {
            OfficialMap13PipelineShaderTools.Validate();
            OfficialMap13LayerTools.Validate();
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }

        private static void Hook()
        {
            started = false;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Map13 pipeline execution timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13 pipeline test runner").AddComponent<Runner>().StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            IEnumerator steps = VerifySteps();
            while (true)
            {
                object value;
                try { if (!steps.MoveNext()) break; value = steps.Current; }
                catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                yield return value;
            }
            SessionState.SetInt(Key + ".result", 0);
            Debug.Log("Map13 pipeline Play Mode passed: original layer/sorting IDs and native procedural Blit mip/color/row order. This GPU fixture does not verify full map rendering, mouse input or saves.");
            EditorApplication.isPlaying = false;
        }


        private static IEnumerator VerifySteps()
        {
            var source = new Texture2D(8, 8, TextureFormat.RGBA32, 4, true);
            source.filterMode = FilterMode.Point;
            source.wrapMode = TextureWrapMode.Clamp;
            var reference = new Color32[4][];
            for (int level = 0; level < 4; level++)
            {
                int size = 8 >> level;
                reference[level] = new Color32[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    reference[level][y * size + x] = new Color32((byte)(13 + x * 17 + level * 3), (byte)(21 + y * 19 + level * 5), (byte)(41 + (x + y) * 7), 255);
                source.SetPixelData(reference[level], level);
            }
            source.Apply(false, false);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeWater/Shaders/CoreBlit.shader");
            var material = new Material(shader);
            var target = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create();
            for (int level = 0; level < 4; level++)
            {
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(target);
                    commands.SetViewport(new Rect(0, 0, 8, 8));
                    commands.ClearRenderTarget(false, true, Color.magenta);
                    commands.SetGlobalTexture("_BlitTexture", source);
                    commands.SetGlobalVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                    commands.SetGlobalFloat("_BlitMipLevel", level);
                    commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                yield return null;
                var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                while (!request.done) yield return null;
                if (request.hasError) throw new InvalidDataException("Native Blit readback failed");
                var actual = request.GetData<Color32>(); int size = 8 >> level;
                for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                {
                    var expected = reference[level][(y * size / 8) * size + x * size / 8];
                    var got = actual[y * 8 + x];
                    if (!got.Equals(expected)) throw new InvalidDataException("Native Blit differs at mip " + level + " pixel " + x + "," + y + ": " + got + " expected " + expected);
                }
                Debug.Log("Map13 procedural native Blit mip " + level + " matches original uploaded RGBA bytes and row order.");
            }
            var depthValues = new float[64];
            for (int i = 0; i < depthValues.Length; i++) depthValues[i] = (i % 8 + 1) / 16f;
            var depthSource = new Texture2D(8, 8, TextureFormat.RFloat, false, true);
            depthSource.filterMode = FilterMode.Point;
            depthSource.SetPixelData(depthValues, 0); depthSource.Apply(false, false);
            var depthTarget = new RenderTexture(8, 8, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var sampledDepth = new RenderTexture(8, 8, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            depthTarget.Create(); sampledDepth.Create();
            var depthShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeWater/Shaders/CoreBlitColorDepth.shader");
            var depthMaterial = new Material(depthShader);
            using (var commands = new CommandBuffer())
            {
                commands.SetRenderTarget(depthTarget);
                commands.SetViewport(new Rect(0, 0, 8, 8));
                commands.ClearRenderTarget(true, true, Color.magenta, 0.9375f);
                commands.SetGlobalTexture("_BlitTexture", source);
                commands.SetGlobalTexture("_InputDepthTexture", depthSource);
                commands.SetGlobalFloat("_BlitMipLevel", 0);
                commands.DrawProcedural(Matrix4x4.identity, depthMaterial, 1, MeshTopology.Triangles, 3);
                commands.SetRenderTarget(sampledDepth);
                commands.SetGlobalTexture("_BlitTexture", new RenderTargetIdentifier(depthTarget), RenderTextureSubElement.Depth);
                commands.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(commands);
            }
            yield return null;
            var depthRequest = AsyncGPUReadback.Request(sampledDepth, 0);
            while (!depthRequest.done) yield return null;
            if (depthRequest.hasError) throw new InvalidDataException("Native depth-transfer readback failed");
            var actualDepth = depthRequest.GetData<float>();
            for (int i = 0; i < depthValues.Length; i++)
                if (Math.Abs(actualDepth[i] - depthValues[i]) > 0.0001f)
                    throw new InvalidDataException("Native original depth pass differs: " + i + ": " + actualDepth[i] + " expected " + depthValues[i]);
            Debug.Log("Map13 original fragment-depth pass writes and transfers all 64 depth values through a native DX12 depth buffer.");
            depthTarget.Release(); sampledDepth.Release(); target.Release();
            UnityEngine.Object.Destroy(depthTarget); UnityEngine.Object.Destroy(sampledDepth); UnityEngine.Object.Destroy(depthMaterial); UnityEngine.Object.Destroy(depthSource);
            UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(material); UnityEngine.Object.Destroy(source);
        }
        private sealed class Runner : MonoBehaviour { }
    }
}
