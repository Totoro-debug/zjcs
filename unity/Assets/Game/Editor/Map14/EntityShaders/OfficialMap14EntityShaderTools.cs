using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Zjcs.Editor.Map14EntityShaders
{
    public static class OfficialMap14EntityShaderTools
    {
        [Serializable] private sealed class Program { public int pass_index; public string[] keywords; public string path, sha256; }
        [Serializable] private sealed class Input { public Program[] programs; public int[] stripped_passes; }
        [Serializable] private sealed class Output { public string path, sha256, original_shader; }
        [Serializable] private sealed class Counts { public int native_shader_objects, engine_shader_objects, original_pass_variants; }
        [Serializable] private sealed class Engine { public string name; }
        [Serializable] private sealed class Receipt { public int mapClassId; public string manifest; public Input[] inputs; public Output[] outputs; public Counts counts; public Engine[] engine_shaders; }
        [Serializable] private sealed class NativeBinding { public string original_shader, path, sha256, meta_sha256, guid; public long file_id; public bool engine; }
        [Serializable] private sealed class BindingReceipt
        {
            public int mapClassId = 14, original_pass_variants, compiled_stages;
            public string manifest = "871_156598", source_sha256, validator_sha256;
            public NativeBinding[] bindings;
        }

        public static void ValidateAndRender()
        {
            Validate();
            OfficialMap14EntityShaderGpuAcceptance.Run();
        }

        public static void Validate()
        {
            var directory = new DirectoryInfo(Application.dataPath);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "official-source.json")))
                directory = directory.Parent;
            if (directory == null) throw new InvalidDataException("Map14 shader source repository was not found.");
            string root = directory.FullName;
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(root,
                "assets/reference/official-map/map14-entity-shaders-source-manifest.json")));
            if (receipt.manifest != "871_156598" || receipt.mapClassId != 14 || receipt.inputs.Length != 34 || receipt.outputs.Length != 34)
                throw new InvalidDataException("Original Map14 entity shader closure changed.");
            if (receipt.counts.native_shader_objects != 34 || receipt.counts.engine_shader_objects != 2 ||
                receipt.counts.original_pass_variants != 2208 || receipt.inputs.Sum(input => input.programs.Length) != 2208)
                throw new InvalidDataException("Original Map14 shader program inventory changed.");
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
                            if (compiled % 64 == 0) Debug.Log("Map14 entity shader progress: " + output.original_shader + " " + compiled);
                        }
                        if (ShaderUtil.ShaderHasError(shader)) throw new InvalidDataException(
                            string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(message => message.message)));
                        count += compiled;
                        families++;
                        Debug.Log("Map14 entity shader compiled: " + output.original_shader + ", " + compiled + " original pass variants.");
                    }
                    catch (Exception error)
                    {
                        errors.Add(output.original_shader + ": " + error.Message);
                        Debug.LogError("Map14 entity shader failed: " + errors[errors.Count - 1]);
                    }
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = asynchronous; }
            if (errors.Count != 0) throw new InvalidDataException(string.Join("\n", errors));
            if (families != receipt.counts.native_shader_objects || count != receipt.counts.original_pass_variants)
                throw new InvalidDataException("Original executable entity variant count changed.");
            NativeBinding Record(Shader shader, string name, bool engine, Output output = null)
            {
                if (!shader || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out string guid, out long fileId))
                    throw new InvalidDataException("Original shader has no explicit native identity: " + name);
                return new NativeBinding { original_shader = name, engine = engine, guid = guid, file_id = fileId,
                    path = output?.path ?? AssetDatabase.GetAssetPath(shader), sha256 = output?.sha256,
                    meta_sha256 = output == null ? null : Hash(File.ReadAllBytes(Path.Combine(root, output.path + ".meta"))) };
            }
            var bindings = receipt.outputs.Select(output => Record(
                AssetDatabase.LoadAssetAtPath<Shader>(output.path.Substring("unity/".Length)), output.original_shader, false, output))
                .Concat(receipt.engine_shaders.Select(engine => Record(Shader.Find(engine.name), engine.name, true))).ToArray();
            var binding = new BindingReceipt { original_pass_variants = count, compiled_stages = count * 2, bindings = bindings,
                source_sha256 = Hash(File.ReadAllBytes(Path.Combine(root, "assets/reference/official-map/map14-entity-shaders-source-manifest.json"))),
                validator_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map14/EntityShaders/OfficialMap14EntityShaderTools.cs")) };
            bool isolated = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/");
            File.WriteAllText(Path.Combine(root, isolated ? ".tools/map14-entity-native-cache-871_156598/entity-shaders-binding-source-manifest.json" :
                "assets/reference/official-map/map14-entity-shaders-binding-source-manifest.json"), JsonUtility.ToJson(binding, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map14 entity shaders passed: " + families + " identical original shader objects, " + count +
                " original pass variants, " + count * 2 + " Windows D3D stages and two exact-name engine shader bindings. " +
                "This verifies compilation; formal entity rendering, input and saves remain unverified.");
        }

        private static void Verify(string root, string path, string expected)
        {
                if (Hash(File.ReadAllBytes(Path.Combine(root, path))) != expected)
                    throw new InvalidDataException("Original entity shader/source hash changed: " + path);
        }
        private static string Hash(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
