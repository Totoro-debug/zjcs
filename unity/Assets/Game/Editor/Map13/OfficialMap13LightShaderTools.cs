using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13LightShaderTools
    {
        [Serializable] private sealed class Receipt { public Program[] programs; public Output output; }
        [Serializable] private sealed class Program { public string[] keywords; public string path; public string sha256; }
        [Serializable] private sealed class Output { public string path; public string sha256; }

        public static void ValidateProject()
        {
            Validate();
            Type waterValidation = Type.GetType("Zjcs.Editor.OfficialMap13WaterShaderTools, Zjcs.Editor", true);
            // The existing water validator also runs ProjectTools.Validate.
            waterValidation.GetMethod("Validate").Invoke(null, null);
        }

        public static void Validate()
        {
            string project = Path.GetFullPath(".");
            bool isolated = project.Replace('\\', '/').Contains("/.tools/");
            string root = isolated ? Path.GetFullPath("../../..") : Path.GetFullPath("..");
            string receiptPath = Path.Combine(root, isolated
                ? ".tools/map13-native-cache-871_156598/light-shader-source-manifest.json"
                : "assets/reference/official-map/map13-light-shader-source-manifest.json");
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(receiptPath));
            if (receipt?.programs?.Length != 32) throw new InvalidDataException("Original shape-light variant closure changed.");
            Verify(root, receipt.output.path, receipt.output.sha256);
            foreach (Program program in receipt.programs) Verify(root, program.path, program.sha256);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeWater/Shaders/LightShape.shader");
            if (!shader) throw new InvalidDataException("Original shape-light translation is missing.");
            bool previous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var pass = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(0);
                foreach (Program program in receipt.programs)
                    foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                    {
                        var compiled = pass.CompileVariant(stage, program.keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                        if (!compiled.Success) throw new InvalidDataException("Shape-light [" + string.Join(",", program.keywords)
                            + "] " + stage + ": " + string.Join("\n", compiled.Messages.Select(m => m.message)));
                    }
                if (ShaderUtil.ShaderHasError(shader))
                    throw new InvalidDataException(string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));
            }
            finally { ShaderUtil.allowAsyncCompilation = previous; }
            Debug.Log("Map13 original shape-light shader compiled: 32 variants, both GPU stages for Windows D3D. Pipeline binding and rendered equivalence remain required.");
        }

        private static void Verify(string root, string path, string expected)
        {
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(root, path))))
                    .Replace("-", "").ToLowerInvariant() != expected)
                    throw new InvalidDataException("Original shape-light source hash changed: " + path);
        }
    }
}
