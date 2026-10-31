using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

public static class CurrentResourceWindowsValidation
{
    [Serializable] public sealed class ProgramCase { public int subshader, pass_index; public bool geometry; public string[] keywords; }
    [Serializable] public sealed class ShaderCase { public string path, sha256, source_name; public ProgramCase[] programs; }
    [Serializable] public sealed class Catalog { public ShaderCase[] shaders; }
    [Serializable] public sealed class Result { public string path, sha256, error; public int compiled_stages; public bool success; }
    [Serializable] public sealed class Results { public Result[] shaders; public int compiled_stages; public string graphics_device; }

    public static void Run()
    {
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        if (String.IsNullOrWhiteSpace(work)) throw new InvalidOperationException("Validation session is required.");
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(work, "shader-catalog.json")));
        var results = new List<Result>();
        string resultPath = Path.Combine(work, "shader-results.json");
        Dictionary<string, Result> prior = File.Exists(resultPath)
            ? JsonUtility.FromJson<Results>(File.ReadAllText(resultPath)).shaders.ToDictionary(r => r.path)
            : new Dictionary<string, Result>();
        bool async = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try
        {
            foreach (ShaderCase item in catalog.shaders)
            {
                if (prior.TryGetValue(item.path, out Result saved) && saved.success && saved.sha256 == item.sha256)
                { results.Add(saved); continue; }
                var result = new Result { path = item.path, sha256 = item.sha256 };
                try
                {
                    using (var hasher = SHA256.Create())
                    {
                        string actual = BitConverter.ToString(hasher.ComputeHash(File.ReadAllBytes(item.path))).Replace("-", "").ToLowerInvariant();
                        if (actual != item.sha256) throw new InvalidDataException("Shader snapshot hash differs.");
                    }
                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(item.path);
                    if (!shader) throw new InvalidDataException("Shader import returned null.");
                    ShaderUtil.ClearShaderMessages(shader);
                    foreach (ProgramCase program in item.programs)
                    {
                        var pass = ShaderUtil.GetShaderData(shader).GetSubshader(program.subshader).GetPass(program.pass_index);
                        foreach (ShaderType stage in program.geometry ? new[] { ShaderType.Vertex, ShaderType.Geometry, ShaderType.Fragment } : new[] { ShaderType.Vertex, ShaderType.Fragment })
                        {
                            var compiled = pass.CompileVariant(stage, program.keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                            if (!compiled.Success)
                                throw new InvalidDataException("sub=" + program.subshader + " pass=" + program.pass_index + " [" + String.Join(",", program.keywords) + "] " + stage + ": " + String.Join("\n", compiled.Messages.Select(m => m.file + ":" + m.line + " " + m.message)));
                            result.compiled_stages++;
                        }
                        if (result.compiled_stages % 40 == 0)
                            Debug.Log("CURRENT_PROGRAM " + item.source_name + " " + result.compiled_stages / 2 + "/" + item.programs.Length);
                    }
                    result.success = true;
                }
                catch (Exception error) { result.error = error.ToString(); }
                results.Add(result);
                File.WriteAllText(resultPath, JsonUtility.ToJson(new Results { shaders = results.ToArray(), compiled_stages = results.Sum(r => r.compiled_stages), graphics_device = SystemInfo.graphicsDeviceVersion }, true));
                Debug.Log("CURRENT_SHADER " + results.Count + "/" + catalog.shaders.Length + " " + item.source_name + " " + result.success);
            }
        }
        finally { ShaderUtil.allowAsyncCompilation = async; }
        File.WriteAllText(resultPath, JsonUtility.ToJson(new Results { shaders = results.ToArray(), compiled_stages = results.Sum(r => r.compiled_stages), graphics_device = SystemInfo.graphicsDeviceVersion }, true));
        EditorApplication.Exit(results.All(r => r.success) ? 0 : 1);
    }
}
