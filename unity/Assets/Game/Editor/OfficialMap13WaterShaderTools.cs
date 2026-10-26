using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Zjcs.Editor
{
    public static class OfficialMap13WaterShaderTools
    {
        [Serializable] private sealed class Receipt { public string manifest; public Input[] inputs; public Output[] outputs; }
        [Serializable] private sealed class Input { public Program[] programs; }
        [Serializable] private sealed class Program { public string[] keywords; public string path; public string sha256; }
        [Serializable] private sealed class Output { public string path; public string sha256; public string original_shader; }

        [MenuItem("Zjcs/World/Map13/Validate Original Water Shader Variants")]
        public static void Validate()
        {
            string root = Path.GetFullPath("..");
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(root,
                "assets/reference/official-map/map13-water-shaders-source-manifest.json")));
            if (receipt == null || receipt.inputs.Length != 5 || receipt.outputs.Length != 5)
                throw new InvalidDataException("Map13 original water shader source closure is incomplete.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            int variants = 0;
            try
            {
                for (int i = 0; i < receipt.outputs.Length; i++)
                {
                    Output output = receipt.outputs[i];
                    Verify(root, output.path, output.sha256);
                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(output.path.Substring("unity/".Length));
                    if (shader == null) throw new InvalidDataException("Map13 water Shader asset is missing: " + output.path);
                    var pass = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(0);
                    foreach (Program program in receipt.inputs[i].programs)
                    {
                        Verify(root, program.path, program.sha256);
                        foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                        {
                            var compiled = pass.CompileVariant(stage, program.keywords, ShaderCompilerPlatform.D3D,
                                BuildTarget.StandaloneWindows64);
                            if (!compiled.Success)
                                throw new InvalidDataException(output.original_shader + " [" + string.Join(",", program.keywords)
                                    + "] " + stage + ": " + string.Join("\n", compiled.Messages.Select(m => m.message)));
                        }
                        variants++;
                    }
                    if (ShaderUtil.ShaderHasError(shader))
                        throw new InvalidDataException(string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = previousAsync; }
            if (variants != 16) throw new InvalidDataException("Original Map13 water shader variant count changed.");
            Debug.Log("Map13 original water shaders compiled: 5 families, 16 variants, both GPU stages for Windows D3D. "
                + "This verifies compilation only; native scene binding and rendered water gameplay remain required.");
            ProjectTools.Validate();
        }

        private static void Verify(string root, string relative, string expected)
        {
            using (var algorithm = SHA256.Create())
            {
                string actual = BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(Path.Combine(root, relative))))
                    .Replace("-", "").ToLowerInvariant();
                if (actual != expected) throw new InvalidDataException("Map13 water shader source hash changed: " + relative);
            }
        }
    }
}
