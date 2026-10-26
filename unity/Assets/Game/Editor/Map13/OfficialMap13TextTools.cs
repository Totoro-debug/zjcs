using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13TextTools
    {
        internal const string FontPath = "Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic.asset";
        internal const string AtlasPath = "Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic-Atlas.texture.asset";
        [Serializable] private sealed class Output { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Program { public string path, sha256; public string[] keywords; }
        [Serializable] private sealed class Input { public Program[] programs; }
        [Serializable] private sealed class Source { public Output[] outputs, configs, dependencies, converters; public Input[] inputs; }
        [Serializable] private sealed class Binding
        {
            public int mapClassId = 13, shaderVariants = 128;
            public string manifest = "871_156598", font_source_sha256, shader_source_sha256, importer_sha256;
            public Output[] imported;
            public Output catalog;
        }

        public static void ValidateProject()
        {
            RestoreAndCompile();
            var tools = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Zjcs.Editor.ProjectTools", false)).FirstOrDefault(t => t != null);
            if (tools == null) throw new InvalidDataException("Global project validator is unavailable.");
            tools.GetMethod("Validate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(null, null);
        }

        public static void RestoreAndCompile()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string root = OfficialMap13FlagTools.Repository;
            string shaderReceipt = Path.Combine(root, "assets/reference/official-map/map13-text-shader-source-manifest.json");
            string fontReceipt = Path.Combine(root, "assets/reference/official-map/map13-native-text-font-source-manifest.json");
            var shaderSource = JsonUtility.FromJson<Source>(File.ReadAllText(shaderReceipt));
            var fontSource = JsonUtility.FromJson<Source>(File.ReadAllText(fontReceipt));
            foreach (Output record in shaderSource.outputs.Concat(fontSource.outputs).Concat(fontSource.configs).Concat(shaderSource.converters).Concat(fontSource.converters))
            {
                if (Hash(File.ReadAllBytes(Path.Combine(root, record.path))) != record.sha256)
                    throw new InvalidDataException("Original text asset/converter bytes differ: " + record.path);
                if (record.path.StartsWith("unity/", StringComparison.Ordinal) && Hash(File.ReadAllBytes(record.path.Substring(6))) != record.sha256)
                    throw new InvalidDataException("Validation project text bytes differ from the main project: " + record.path);
                if (!string.IsNullOrEmpty(record.meta_sha256) && Hash(File.ReadAllBytes(Path.Combine(root, record.path + ".meta"))) != record.meta_sha256)
                    throw new InvalidDataException("Original text import metadata differs: " + record.path);
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderSource.outputs.Single().path.Substring(6));
            if (shader == null) throw new InvalidDataException("Original text shader did not import.");
            var programs = shaderSource.inputs.Single().programs;
            if (programs.Length != 128) throw new InvalidDataException("Original text program count differs.");
            var data = ShaderUtil.GetShaderData(shader);
            foreach (var program in programs)
            {
                if (Hash(File.ReadAllBytes(Path.Combine(root, program.path))) != program.sha256)
                    throw new InvalidDataException("Original text executable source differs.");
                foreach (var stage in new[] { UnityEditor.Rendering.ShaderType.Vertex, UnityEditor.Rendering.ShaderType.Fragment })
                {
                    var compiled = data.GetSubshader(0).GetPass(0).CompileVariant(stage, program.keywords,
                        UnityEditor.Rendering.ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64, UnityEngine.Rendering.GraphicsTier.Tier2, true);
                    if (!compiled.Success) throw new InvalidDataException("Original text Windows compile failed: " + stage + ":" +
                        string.Join(",", program.keywords) + "\n" + string.Join("\n", compiled.Messages.Select(m => m.file + ":" + m.line + ":" + m.message)));
                }
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Official/World/Map/13/NativeText/FZY4K-Dynamic.mat");
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Official/World/Map/13/NativeText/FZY4K.ttf");
            if (font == null || atlas == null || material == null || sourceFont == null || font.sourceFontFile != sourceFont ||
                font.material != material || material.shader != shader || material.mainTexture != atlas || font.atlasTextures.Length != 1 || font.atlasTexture != atlas)
                throw new InvalidDataException("Original text font/atlas/material/source font binding differs.");
            if (font.faceInfo.familyName != "FZCuYuan-M03" || font.faceInfo.pointSize != 40 || font.atlasWidth != 2048 || font.atlasHeight != 2048 ||
                font.atlasPadding != 4 || (int)font.atlasRenderMode != 4165 || font.atlasPopulationMode != AtlasPopulationMode.Dynamic ||
                !font.isMultiAtlasTexturesEnabled || font.glyphTable.Count != 2 || font.characterTable.Count != 2 || font.italicStyle != 15 ||
                atlas.format != TextureFormat.Alpha8 || atlas.mipmapCount != 1)
                throw new InvalidDataException("Original native font serialized settings differ.");
            const string catalogPath = "Assets/Game/Resources/Map13/Native/native-text-catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(catalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.Bindings = new[]
            {
                new NativeMap13AssetCatalog.Binding { OfficialPath = "Assets/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic.asset", Asset = font },
                new NativeMap13AssetCatalog.Binding { OfficialPath = "Assets/TextMesh Pro/Fonts/FZY4K.TTF", Asset = sourceFont }
            };
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            Output Record(string path) => new Output { path = "unity/" + path, sha256 = Hash(File.ReadAllBytes(path)), meta_sha256 = Hash(File.ReadAllBytes(path + ".meta")) };
            var binding = new Binding { imported = fontSource.outputs.Select(p => Record(p.path.Substring(6))).ToArray(), catalog = Record(catalogPath),
                font_source_sha256 = Hash(File.ReadAllBytes(fontReceipt)), shader_source_sha256 = Hash(File.ReadAllBytes(shaderReceipt)),
                importer_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map13/OfficialMap13TextTools.cs")) };
            bool isolated = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/");
            File.WriteAllText(Path.Combine(root, isolated ? ".tools/map13-native-cache-871_156598/native-text-binding-source-manifest.json" :
                "assets/reference/official-map/map13-native-text-binding-source-manifest.json"), JsonUtility.ToJson(binding, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map13 native text font/atlas/material/source font explicit bindings passed; all 128 original text variants compiled for Windows in both GPU stages. World label/effect runtime integration remains incomplete.");
        }

        internal static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
