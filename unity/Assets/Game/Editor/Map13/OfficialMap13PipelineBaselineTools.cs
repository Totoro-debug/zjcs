using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13PipelineBaselineTools
    {
        [Serializable] private sealed class Case
        {
            public int subshader_index, pass_index, index;
            public string[] keywords;
            public string path, sha256, vertex_sha256, fragment_sha256;
        }
        [Serializable] private sealed class Input
        {
            public int path_id;
            public string name, baseline_path, baseline_sha256;
            public Case[] cases;
        }
        [Serializable] private sealed class Cases { public string manifest; public int color_space; public Input[] shaders; }
        [Serializable] private sealed class Result
        {
            public int path_id, subshader_index, pass_index, index;
            public string stage, expected_sha256, actual_sha256;
            public string[] keywords;
            public bool success;
        }
        [Serializable] private sealed class Results
        {
            public string manifest, cases_sha256, validator_sha256;
            public string[] platform_defines;
            public Result[] stages;
        }

        public static void Validate()
        {
            string root = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/") ? Path.GetFullPath("../../..") : Path.GetFullPath("..");
            string path = Path.Combine(root, "unity/Assets/Official/World/Map/13/Source/native-pipeline-baseline-cases.json");
            string cache = Path.Combine(root, ".tools/map13-native-cache-871_156598/baseline-shader-comparison");
            Directory.CreateDirectory(cache);
            var cases = JsonUtility.FromJson<Cases>(File.ReadAllText(path));
            if (cases.color_space != 1) throw new InvalidDataException("Original APK linear shader convention differs.");
            var platformMethod = typeof(ShaderUtil).GetMethod("GetShaderPlatformKeywordsForBuildTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(ShaderCompilerPlatform), typeof(BuildTarget), typeof(GraphicsTier) }, null);
            var platform = ((BuiltinShaderDefine[])platformMethod.Invoke(null, new object[] { ShaderCompilerPlatform.GLES3x, BuildTarget.Android, GraphicsTier.Tier2 }))
                .Where(value => value.ToString() != "UNITY_COLORSPACE_GAMMA").ToArray();
            var results = new List<Result>();
            bool previous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                foreach (Input source in cases.shaders)
                {
                    Verify(root, source.baseline_path, source.baseline_sha256);
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(source.baseline_path.Substring("unity/".Length));
                    if (!shader || shader.name != source.name) throw new InvalidDataException("Engine shader source binding differs: " + source.name);
                    foreach (Case variant in source.cases)
                    {
                        Verify(root, variant.path, variant.sha256);
                        var pass = ShaderUtil.GetShaderData(shader).GetSubshader(variant.subshader_index).GetPass(variant.pass_index);
                        foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                        {
                            var compiled = pass.CompileVariant(stage, variant.keywords, ShaderCompilerPlatform.GLES3x, BuildTarget.Android, platform, GraphicsTier.Tier2, true);
                            string text = compiled.Success ? Encoding.UTF8.GetString(compiled.ShaderData).Trim() : "";
                            string expected = stage == ShaderType.Vertex ? variant.vertex_sha256 : variant.fragment_sha256;
                            string actual = Hash(Encoding.UTF8.GetBytes(text));
                            bool success = compiled.Success && actual == expected;
                            results.Add(new Result { path_id = source.path_id, subshader_index = variant.subshader_index,
                                pass_index = variant.pass_index, index = variant.index, stage = stage.ToString(),
                                keywords = variant.keywords, expected_sha256 = expected, actual_sha256 = actual, success = success });
                            if (!success)
                            {
                                string name = source.path_id + "-s" + variant.subshader_index + "-p" + variant.pass_index + "-" + variant.index + "-" + stage;
                                File.WriteAllText(Path.Combine(cache, name + ".glsl"), text, new UTF8Encoding(false));
                                Debug.LogWarning("APK/URP executable comparison differs: " + name + " [" + string.Join(",", variant.keywords) + "] "
                                    + string.Join("\n", compiled.Messages.Select(m => m.message)));
                            }
                        }
                    }
                    Debug.Log("APK/URP shader comparison checked: " + source.name + ", " + source.cases.Length + " pass/keyword cases.");
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = previous; }
            File.WriteAllText(Path.Combine(cache, "results.json"), JsonUtility.ToJson(new Results { manifest = cases.manifest,
                cases_sha256 = Hash(File.ReadAllBytes(path)), platform_defines = platform.Select(value => value.ToString()).ToArray(), validator_sha256 = Hash(File.ReadAllBytes(Path.Combine(root,
                    "unity/Assets/Game/Editor/Map13/OfficialMap13PipelineBaselineTools.cs"))), stages = results.ToArray() }, true) + "\n", new UTF8Encoding(false));
            int failures = results.Count(item => !item.success);
            if (failures != 0) throw new InvalidDataException("APK/URP executable stages require investigation: " + failures + "/" + results.Count);
            Debug.Log("APK/URP executable equivalence proved: " + results.Count + " compiled GLES stages match normalized original APK bytes. Platform-stripped shaders have no comparison result.");
        }

        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static void Verify(string root, string path, string expected)
        {
            if (Hash(File.ReadAllBytes(Path.Combine(root, path))) != expected) throw new InvalidDataException("Shader comparison input differs: " + path);
        }
    }
}
