using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Zjcs.Editor.Map13EntityShaders
{
    public static class OfficialMap13EntityShaderTools
    {
        [Serializable] private sealed class Program { public int pass_index; public string[] keywords; public string path, sha256; }
        [Serializable] private sealed class Input { public Program[] programs; public int[] stripped_passes; }
        [Serializable] private sealed class Output { public string path, sha256, original_shader; }
        [Serializable] private sealed class Receipt { public string manifest; public Input[] inputs; public Output[] outputs; }

        public static void Validate() => ValidateCore(false);
        public static void ValidateRemaining() => ValidateCore(true);

        private static void ValidateCore(bool remaining)
        {
            var directory = new DirectoryInfo(Application.dataPath);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "official-source.json")))
                directory = directory.Parent;
            if (directory == null) throw new InvalidDataException("Map13 shader source repository was not found.");
            string root = directory.FullName;
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(root,
                "assets/reference/official-map/map13-entity-shaders-source-manifest.json")));
            if (receipt.manifest != "871_156598" || receipt.inputs.Length != 28 || receipt.outputs.Length != 28)
                throw new InvalidDataException("Original Map13 entity shader closure changed.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            bool asynchronous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            int count = 0, families = 0;
            var errors = new System.Collections.Generic.List<string>();
            try
            {
                for (int i = 0; i < receipt.outputs.Length; i++)
                {
                    Output output = receipt.outputs[i];
                    if (remaining && output.original_shader != "Universal Render Pipeline/Lit" && output.original_shader != "Skybox/Procedural") continue;
                    try
                    {
                        Verify(root, output.path, output.sha256);
                        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(output.path.Substring("unity/".Length));
                        if (!shader) throw new InvalidDataException("Native shader is missing.");
                        int compiled = 0;
                        foreach (Program program in receipt.inputs[i].programs.OrderByDescending(item => item.keywords.Length))
                        {
                            Verify(root, program.path, program.sha256);
                            var pass = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(program.pass_index);
                            foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                            {
                                var result = pass.CompileVariant(stage, program.keywords,
                                    ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                                if (!result.Success) throw new InvalidDataException("pass " + program.pass_index
                                    + " [" + string.Join(",", program.keywords) + "] " + stage + ": "
                                    + string.Join("\n", result.Messages.Select(message => message.message)));
                            }
                            compiled++;
                            if (compiled % 64 == 0) Debug.Log("Map13 entity shader progress: " + output.original_shader + " " + compiled);
                        }
                        if (ShaderUtil.ShaderHasError(shader)) throw new InvalidDataException(
                            string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(message => message.message)));
                        count += compiled;
                        families++;
                        Debug.Log("Map13 entity shader compiled: " + output.original_shader + ", " + compiled + " original pass variants.");
                    }
                    catch (Exception error)
                    {
                        errors.Add(output.original_shader + ": " + error.Message);
                        Debug.LogError("Map13 entity shader failed: " + errors[errors.Count - 1]);
                    }
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = asynchronous; }
            if (errors.Count != 0) throw new InvalidDataException(string.Join("\n", errors));
            if (families != (remaining ? 2 : 28) || count != (remaining ? 12 : 1543)) throw new InvalidDataException("Original executable entity variant count changed.");
            Debug.Log("Map13 entity shaders passed: " + families + " families, " + count + " original pass variants, " + (count*2) + " Windows D3D stages. "
                + "Eight previously migrated shaders are separately hash-verified; two APK engine shaders and original stripped GBuffer remain separately recorded. "
                + "This verifies compilation. Entity rendering, input and saves remain unverified.");
        }

        private static void Verify(string root, string path, string expected)
        {
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(root, path))))
                    .Replace("-", "").ToLowerInvariant() != expected)
                    throw new InvalidDataException("Original entity shader/source hash changed: " + path);
        }
    }
}
