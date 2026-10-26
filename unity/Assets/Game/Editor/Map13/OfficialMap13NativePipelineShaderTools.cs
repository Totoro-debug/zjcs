using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13NativePipelineShaderTools
    {
        [Serializable] private sealed class Case
        {
            public int subshader_index, pass_index;
            public string[] keywords;
            public string vertex_sha256, fragment_sha256;
        }
        [Serializable] private sealed class Input { public int path_id; public Case[] cases; }
        [Serializable] private sealed class Cases { public Input[] shaders; }
        [Serializable] private sealed class Binding { public int path_id, original_subshader_index; public string path, sha256; }
        [Serializable] private sealed class Bindings { public Binding[] engine_shaders; }

        public static void Validate()
        {
            string root = Path.GetFullPath(Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/") ? "../../.." : "..");
            var cases = JsonUtility.FromJson<Cases>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-pipeline-baseline-cases.json"));
            var bindings = JsonUtility.FromJson<Bindings>(File.ReadAllText(Path.Combine(root, "assets/reference/official-map/map13-native-pipeline-source-manifest.json")));
            var method = typeof(ShaderUtil).GetMethod("GetShaderPlatformKeywordsForBuildTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(ShaderCompilerPlatform), typeof(BuildTarget), typeof(GraphicsTier) }, null);
            BuiltinShaderDefine[] Defines(ShaderCompilerPlatform platform, BuildTarget target) =>
                ((BuiltinShaderDefine[])method.Invoke(null, new object[] { platform, target, GraphicsTier.Tier2 }))
                .Where(value => value.ToString() != "UNITY_COLORSPACE_GAMMA").ToArray();
            int windows = 0, adapted = 0;
            bool previous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                foreach (var binding in bindings.engine_shaders)
                {
                    string path = binding.path.Substring(6);
                    if (Hash(File.ReadAllBytes(path)) != binding.sha256) throw new InvalidDataException("Native pipeline shader changed: " + path);
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    if (!shader) throw new InvalidDataException("Native pipeline shader did not import: " + path);
                    var data = ShaderUtil.GetShaderData(shader);
                    var input = cases.shaders.Single(s => s.path_id == binding.path_id);
                    var variants = input.cases.Length != 0 ? input.cases : Enumerable.Range(0, data.SubshaderCount)
                        .SelectMany(s => Enumerable.Range(0, data.GetSubshader(s).PassCount).Select(p => new Case { subshader_index = s, pass_index = p, keywords = Array.Empty<string>() })).ToArray();
                    foreach (var variant in variants)
                    {
                        var pass = data.GetSubshader(variant.subshader_index - binding.original_subshader_index).GetPass(variant.pass_index);
                        foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                        {
                            var compiled = pass.CompileVariant(stage, variant.keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64,
                                Defines(ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64), GraphicsTier.Tier2, true);
                            if (!compiled.Success) throw new InvalidDataException("Native Windows pipeline shader failed: " + path + ":" + variant.pass_index + ":" + stage + ":" +
                                string.Join("\n", compiled.Messages.Select(m => m.message)));
                            windows++;
                            if (binding.original_subshader_index == 0) continue;
                            var gles = pass.CompileVariant(stage, variant.keywords, ShaderCompilerPlatform.GLES3x, BuildTarget.Android,
                                Defines(ShaderCompilerPlatform.GLES3x, BuildTarget.Android), GraphicsTier.Tier2, true);
                            string expected = stage == ShaderType.Vertex ? variant.vertex_sha256 : variant.fragment_sha256;
                            if (!gles.Success || Hash(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(gles.ShaderData).Trim())) != expected)
                                throw new InvalidDataException("Original Android shader branch changed after native selection: " + path);
                            adapted++;
                        }
                    }
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = previous; }
            Debug.Log("Map13 native pipeline shader validation passed: " + windows + " Windows D3D stages; " + adapted + " selected Android-branch GLES stages still exactly match APK executables.");
        }
        private static string Hash(byte[] value)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(value)).Replace("-", "").ToLowerInvariant();
        }
    }
}
