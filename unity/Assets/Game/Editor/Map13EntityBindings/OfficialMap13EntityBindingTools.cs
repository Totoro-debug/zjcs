using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Offline.World.NativeMap13EntityAssets;

namespace Zjcs.Editor.Map13EntityBindings
{
    public static class OfficialMap13EntityBindingTools
    {
        public const string SourcePath = "Assets/Official/World/Map/13/Source/native-entity-shader-bindings.json";
        public const string CatalogPath = "Assets/Game/Resources/Map13/Native/entity-shader-catalog.asset";
        [Serializable] public sealed class Program
        {
            public int pass_index;
            public string[] keywords;
        }
        [Serializable] public sealed class Binding
        {
            public string cab, path_id, original_name, original_fallback, native_name, path, fallback_native_name;
            public bool engine;
            public Program[] programs;
        }
        [Serializable] public sealed class Source
        {
            public string manifest;
            public Binding[] bindings;
        }

        public static void BuildAndValidate()
        {
            var asynchronous = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var source = JsonUtility.FromJson<Source>(File.ReadAllText(SourcePath));
                if (source.manifest != "871_156598" || source.bindings.Length != 21)
                    throw new InvalidOperationException("Unexpected original entity shader selection.");
                var entries = source.bindings.Select(binding =>
                {
                    var shader = binding.engine ? Shader.Find(binding.native_name) :
                        AssetDatabase.LoadAssetAtPath<Shader>(binding.path);
                    if (shader == null || shader.name != binding.native_name || (!binding.engine && !shader.isSupported))
                        throw new InvalidOperationException("Native shader failed to load: " + binding.original_name +
                            " actual=" + (shader == null ? "null" : shader.name + " supported=" + shader.isSupported +
                            " path=" + AssetDatabase.GetAssetPath(shader)));
                    if (!binding.engine)
                    {
                        var expected = string.IsNullOrEmpty(binding.fallback_native_name) ? "Fallback Off" :
                            "Fallback \"" + binding.fallback_native_name + "\"";
                        if (!File.ReadAllText(binding.path).Contains(expected + "\n"))
                            throw new InvalidOperationException("Original fallback is not bound: " + binding.original_name);
                    }
                    return new NativeMap13EntityShaderCatalog.Entry
                    {
                        sourceCab = binding.cab, sourcePathId = binding.path_id,
                        originalName = binding.original_name, originalFallback = binding.original_fallback,
                        shader = shader
                    };
                }).ToArray();
                var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13EntityShaderCatalog>(CatalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<NativeMap13EntityShaderCatalog>();
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                }
                catalog.manifest = source.manifest;
                catalog.entries = entries;
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
                catalog = AssetDatabase.LoadAssetAtPath<NativeMap13EntityShaderCatalog>(CatalogPath);
                var stages = 0;
                foreach (var binding in source.bindings)
                {
                    var shader = catalog.Require(binding.cab.ToUpperInvariant(), binding.path_id);
                    if (shader.name != binding.native_name)
                        throw new InvalidOperationException("Native catalog changed shader identity.");
                    if (!string.IsNullOrEmpty(binding.fallback_native_name) &&
                        Shader.Find(binding.fallback_native_name) == null)
                        throw new InvalidOperationException("Native fallback dependency is not available.");
                    if (!binding.engine && binding.programs != null)
                    {
                        var data = ShaderUtil.GetShaderData(shader);
                        foreach (var program in binding.programs)
                        {
                            var pass = data.GetSubshader(0).GetPass(program.pass_index);
                            foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                            {
                                var compiled = pass.CompileVariant(stage, program.keywords,
                                    ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                                if (!compiled.Success)
                                    throw new InvalidOperationException("Native shader did not compile: " +
                                        shader.name + " pass " + program.pass_index + " " + stage + " " +
                                        string.Join(",", program.keywords));
                                stages++;
                            }
                        }
                    }
                    if (ShaderUtil.ShaderHasError(shader))
                        throw new InvalidOperationException("Native shader import has errors: " + shader.name);
                    Debug.Log("Map13 bound shader: " + shader.name + " supported=" + shader.isSupported);
                }
                Debug.Log("Map13 native entity shader bindings passed: 21 serialized shader references, " +
                    "all original fallback chains, " + stages + " newly converted executable stages. Precompiled engine shader is checked by GPU execution.");
            }
            finally { ShaderUtil.allowAsyncCompilation = asynchronous; }
        }
    }
}
