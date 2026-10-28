using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Editor.ProfessionUI
{
    public static class ProfessionShaderValidation
    {
        [Serializable] private sealed class Native { public string path, sha256, guid, name; }
        [Serializable] private sealed class Program { public int pass_index; public string[] keywords; }
        [Serializable] private sealed class Binding
        {
            public string original_name;
            public Native native;
            public Program[] programs;
        }
        [Serializable] private sealed class Source { public string manifest; public Binding[] bindings; }

        public static void Run()
        {
            string path = null;
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-professionShaderBindings") path = args[i + 1];
            if (path == null) throw new InvalidOperationException("Original shader binding receipt was not specified.");
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(path));
            if (source.manifest != "871_156598" || source.bindings.Length != 7)
                throw new InvalidOperationException("Original profession shader selection changed.");
            var async = ShaderUtil.allowAsyncCompilation;
            var stages = 0;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var binding in source.bindings)
                {
                    if (AssetDatabase.GUIDToAssetPath(binding.native.guid) != binding.native.path)
                        throw new InvalidOperationException("Native shader GUID binding changed: " + binding.original_name);
                    using (var hash = SHA256.Create())
                    {
                        var actual = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(binding.native.path))).Replace("-", "").ToLowerInvariant();
                        if (actual != binding.native.sha256)
                            throw new InvalidOperationException("Native shader source changed: " + binding.original_name);
                    }
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(binding.native.path);
                    if (shader == null || shader.name != binding.native.name || !shader.isSupported)
                        throw new InvalidOperationException("Native shader failed to load: " + binding.original_name);
                    var data = ShaderUtil.GetShaderData(shader);
                    foreach (var program in binding.programs)
                    {
                        var pass = data.GetSubshader(0).GetPass(program.pass_index);
                        foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                        {
                            var compiled = pass.CompileVariant(stage, program.keywords,
                                ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                            if (!compiled.Success)
                                throw new InvalidOperationException("Profession shader failed Windows compilation: " +
                                    shader.name + " pass " + program.pass_index + " " + stage + " " + string.Join(",", program.keywords));
                            stages++;
                        }
                    }
                    if (ShaderUtil.ShaderHasError(shader))
                        throw new InvalidOperationException("Profession shader has import errors: " + shader.name);
                    Debug.Log("Profession shader verified: " + binding.original_name + " => " + shader.name +
                        "; original variants=" + binding.programs.Length);
                }
                Debug.Log("Profession shader validation passed: 7 native GUID bindings, " + stages +
                    " Windows D3D executable stages. Profession prefab rendering remains a separate validation.");
            }
            finally { ShaderUtil.allowAsyncCompilation = async; }
        }
    }
}
