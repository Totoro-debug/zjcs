using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13PipelineShaderTools
    {
        [Serializable] private sealed class Program { public int pass_index; public string[] keywords; public string path, sha256; }
        [Serializable] private sealed class Input { public Program[] programs; }
        [Serializable] private sealed class Output { public string path, sha256, original_shader; }
        [Serializable] private sealed class ShaderReceipt { public Input[] inputs; public Output[] outputs; }
        public static void ValidateProject()
        {
            Validate();
            OfficialMap13LayerTools.Validate();
            OfficialMap13NativeSceneTools.ValidateProject();
        }

        public static void Validate()
        {
            string root = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/")
                ? Path.GetFullPath("../../..") : Path.GetFullPath("..");
            var receipt = JsonUtility.FromJson<ShaderReceipt>(File.ReadAllText(Path.Combine(root,
                "assets/reference/official-map/map13-pipeline-shaders-source-manifest.json")));
            if (receipt?.outputs?.Length != 12 || receipt.inputs.Length != 12)
                throw new InvalidDataException("Original Renderer2D shader closure changed.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            bool previous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            int count = 0;
            try
            {
                for (int index = 0; index < receipt.outputs.Length; index++)
                {
                    var output = receipt.outputs[index];
                    Verify(root, output.path, output.sha256);
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(output.path.Substring("unity/".Length));
                    if (!shader) throw new InvalidDataException("Original pipeline shader is missing: " + output.path);
                    if (ShaderUtil.ShaderHasError(shader)) throw new InvalidDataException(output.original_shader + ": " + string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(item => item.file + ":" + item.line + ": " + item.message)));
                    foreach (var program in receipt.inputs[index].programs)
                    {
                        Verify(root, program.path, program.sha256);
                        var pass = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(program.pass_index);
                        foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                        {
                            var result = pass.CompileVariant(stage, program.keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                            if (!result.Success) throw new InvalidDataException(output.original_shader + " pass " + program.pass_index
                                + " [" + string.Join(",", program.keywords) + "] " + stage + ": "
                                + string.Join("\n", result.Messages.Select(item => item.message)));
                        }
                        count++;
                    }
                    if (ShaderUtil.ShaderHasError(shader)) throw new InvalidDataException(string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(item => item.message)));
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = previous; }
            if (count != 91) throw new InvalidDataException("Original Renderer2D pass/variant closure changed.");
            Debug.Log("Map13 original Renderer2D shaders compiled: 12 families, 39 passes, 91 pass variants, both GPU stages for Windows D3D. Native scene rendering remains unverified.");
        }

        private static void Verify(string root, string path, string expected)
        {
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(root, path))))
                    .Replace("-", "").ToLowerInvariant() != expected)
                    throw new InvalidDataException("Original pipeline shader source hash changed: " + path);
        }

    }
}
