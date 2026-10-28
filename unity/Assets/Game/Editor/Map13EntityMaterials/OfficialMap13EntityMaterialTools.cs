using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13EntityMaterials
{
    public static class OfficialMap13EntityMaterialTools
    {
        internal const string SourcePath = "assets/reference/official-map/map13-native-entity-materials-native-source-manifest.json";
        private const string CatalogPath = "Assets/Game/Resources/Map13/Native/native-entity-materials-catalog.asset";
        [Serializable] internal sealed class Output { public string path, sha256, meta_sha256; }
        [Serializable] internal sealed class Reference { public string path, guid, native_name; public long file_id; }
        [Serializable] internal sealed class TextureProperty { public string name, path, state; public Vector2 scale, offset; }
        [Serializable] internal sealed class NumberProperty { public string name; public int value_bits, int_value; }
        [Serializable] internal sealed class ColorProperty { public string name; public int r_bits, g_bits, b_bits, a_bits; }
        [Serializable] internal sealed class Tag { public string name, value; }
        [Serializable] internal sealed class KeywordNormalization { public string[] native_valid, native_invalid; }
        [Serializable] internal sealed class Entry
        {
            public string cab, path_id, name;
            public Output output;
            public Reference shader;
            public string[] valid_keywords, invalid_keywords, disabled_passes;
            public KeywordNormalization keyword_normalization;
            public int lightmap_flags, render_queue;
            public bool instancing, double_sided;
            public Tag[] tags;
            public TextureProperty[] textures;
            public NumberProperty[] ints, floats;
            public ColorProperty[] colors;
        }
        [Serializable] private sealed class Bindings { public int mapClassId; public string manifest; public Entry[] entries; }
        [Serializable] private sealed class Source { public int mapClassId; public string manifest; public Output bindings; public Output[] outputs; }
        [Serializable] private sealed class BindingProof
        {
            public int mapClassId = 13, imported_materials, checked_properties, bound_textures, deferred_empty_atlas_slots;
            public string manifest = "871_156598", source_sha256, importer_sha256, unity_version;
            public Output catalog;
            public Output[] imported;
            public PlayModeProof play_mode;
        }
        [Serializable] internal sealed class PlayModeProof
        {
            public string unity_version, graphics_api, color_space, acceptance_sha256, coverage;
            public int materials, exact_properties, engine_render_cases, compared_engine_pixels, original_textured_materials, runtime_errors;
        }
        internal static string Repository
        {
            get
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "official-source.json"))) directory = directory.Parent;
                return directory?.FullName ?? throw new InvalidDataException("Map13 repository not found.");
            }
        }
        internal static Entry[] ReadEntries()
        {
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(Repository, SourcePath)));
            Check(source.bindings);
            var bindings = JsonUtility.FromJson<Bindings>(File.ReadAllText(Path.Combine(Repository, source.bindings.path)));
            if (source.mapClassId != 13 || source.manifest != "871_156598" || source.outputs.Length != 1843 ||
                bindings.mapClassId != 13 || bindings.manifest != "871_156598" || bindings.entries.Length != 1843)
                throw new InvalidDataException("Original Map13 material inventory differs.");
            return bindings.entries;
        }
        public static void ImportAndValidate()
        {
            try { RestoreAndValidate(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        [Serializable] private sealed class KeywordDifference
        {
            public string identity, name, shader;
            public string[] original_valid, original_invalid, native_valid, native_invalid, native_keyword_space;
        }
        [Serializable] private sealed class KeywordDifferences { public KeywordDifference[] entries; }
        public static void InspectKeywordNormalization()
        {
            var differences = new System.Collections.Generic.List<KeywordDifference>();
            foreach (Entry entry in ReadEntries())
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetPath(entry.output.path));
                var serialized = new SerializedObject(material);
                string[] Strings(string name)
                {
                    var property = serialized.FindProperty(name);
                    return Enumerable.Range(0,property.arraySize).Select(i => property.GetArrayElementAtIndex(i).stringValue).ToArray();
                }
                string[] valid = Strings("m_ValidKeywords"), invalid = Strings("m_InvalidKeywords");
                if (valid.SequenceEqual(entry.valid_keywords) && invalid.SequenceEqual(entry.invalid_keywords)) continue;
                differences.Add(new KeywordDifference { identity = Identity(entry), name = entry.name, shader = material.shader.name,
                    original_valid = entry.valid_keywords, original_invalid = entry.invalid_keywords, native_valid = valid, native_invalid = invalid,
                    native_keyword_space = material.shader.keywordSpace.keywords.Select(k => k.name).ToArray() });
            }
            File.WriteAllText(Path.Combine(Repository,".tools/map13-entity-native-cache-871_156598/material-keyword-differences.json"),
                JsonUtility.ToJson(new KeywordDifferences { entries = differences.ToArray() },true) + "\n",new UTF8Encoding(false));
            Debug.Log("Map13 material keyword normalization inspected: " + differences.Count + " differences.");
            EditorApplication.Exit(0);
        }
        internal static void RestoreAndValidate()
        {
            Entry[] entries = ReadEntries();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int properties = 0, textures = 0, deferred = 0;
            var bindings = entries.Select(entry =>
            {
                Check(entry.output);
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetPath(entry.output.path));
                properties += Validate(entry, material);
                textures += entry.textures.Count(t => t.state == "native");
                deferred += entry.textures.Count(t => t.state == "empty_dynamic_atlas");
                return new NativeMap13AssetCatalog.Binding { OfficialPath = Identity(entry), Asset = material };
            }).ToArray();
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(CatalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.Bindings = bindings;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            if (Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-entity-materials-catalog") != catalog ||
                bindings.Any(binding => catalog.Load<Material>(binding.OfficialPath) != binding.Asset))
                throw new InvalidDataException("Exact CAB/PathID material bindings failed.");
            var proof = new BindingProof { source_sha256 = Hash(File.ReadAllBytes(Path.Combine(Repository, SourcePath))),
                importer_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map13EntityMaterials/OfficialMap13EntityMaterialTools.cs")),
                imported_materials = entries.Length, checked_properties = properties, bound_textures = textures,
                deferred_empty_atlas_slots = deferred, unity_version = Application.unityVersion,
                catalog = Record(CatalogPath), imported = entries.Select(entry => entry.output).ToArray() };
            bool isolated = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/");
            File.WriteAllText(Path.Combine(Repository, isolated ? ".tools/map13-entity-native-cache-871_156598/entity-materials-binding-source-manifest.json" :
                "assets/reference/official-map/map13-native-entity-materials-binding-source-manifest.json"), JsonUtility.ToJson(proof, true) + "\n", new UTF8Encoding(false));
            Debug.Log($"Map13 native materials validated: {entries.Length} exact bindings, {properties} original properties, {textures} texture references, {deferred} explicitly deferred empty font atlas slots.");
        }
        internal static string Identity(Entry entry) => entry.cab + ":Material:" + entry.path_id;
        internal static void RecordPlayModeValidation(int properties, int pixels)
        {
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Actual GPU Play Mode is required for material validation.");
            bool isolated = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/");
            string path = Path.Combine(Repository,isolated ? ".tools/map13-entity-native-cache-871_156598/entity-materials-binding-source-manifest.json" :
                "assets/reference/official-map/map13-native-entity-materials-binding-source-manifest.json");
            var proof = JsonUtility.FromJson<BindingProof>(File.ReadAllText(path));
            if (proof.source_sha256 != Hash(File.ReadAllBytes(Path.Combine(Repository,SourcePath))) ||
                proof.importer_sha256 != Hash(File.ReadAllBytes("Assets/Game/Editor/Map13EntityMaterials/OfficialMap13EntityMaterialTools.cs")) ||
                proof.imported_materials != 1843 || proof.checked_properties != properties)
                throw new InvalidDataException("Native material import proof changed during GPU checks.");
            proof.play_mode = new PlayModeProof { unity_version=Application.unityVersion,graphics_api=SystemInfo.graphicsDeviceType.ToString(),
                color_space=QualitySettings.activeColorSpace.ToString(),acceptance_sha256=Hash(File.ReadAllBytes("Assets/Game/Editor/Map13EntityMaterials/OfficialMap13EntityMaterialAcceptance.cs")),
                materials=1843,exact_properties=properties,engine_render_cases=32,compared_engine_pixels=pixels,original_textured_materials=4,runtime_errors=0,
                coverage="Actual runtime catalog and all saved fields; GUI alpha/color/blend, FrameDebugger 2D/array/cube levels/channels/Y flip/sRGB equations, four original material/texture quad renders. Excludes XR/MSAA execution, original font initialization, entity geometry, input, saves and Windows Player." };
            File.WriteAllText(path,JsonUtility.ToJson(proof,true)+"\n",new UTF8Encoding(false));
        }
        internal static string AssetPath(string path) => path.StartsWith("unity/", StringComparison.Ordinal) ? path.Substring(6) : path;
        internal static int Validate(Entry entry, Material material)
        {
            if (!material || material.name != entry.name || !material.shader)
                throw new InvalidDataException("Native material is missing or renamed: " + Identity(entry));
            Shader shader = entry.shader.path.Length > 0 ? AssetDatabase.LoadAssetAtPath<Shader>(AssetPath(entry.shader.path)) : Shader.Find(entry.shader.native_name);
            if (!shader || material.shader != shader || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                throw new InvalidDataException("Original native shader binding/compilation failed: " + entry.name + ": " + shader);
            if (entry.shader.path.Length == 0 && (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out string guid, out long id) ||
                guid != entry.shader.guid || id != entry.shader.file_id)) throw new InvalidDataException("Windows builtin shader identity differs.");
            var serialized = new SerializedObject(material);
            int checkedCount = 0;
            SerializedProperty Property(string name)
            {
                checkedCount++;
                return serialized.FindProperty(name) ?? throw new InvalidDataException("Original serialized material property is absent: " + entry.name + "/" + name);
            }
            void Equal(bool condition, string name)
            { if (!condition) throw new InvalidDataException("Original material field differs: " + Identity(entry) + "/" + name); }
            void Strings(string name, string[] expected)
            {
                var array = Property(name); Equal(array.arraySize == expected.Length, name + " length actual=[" +
                    string.Join(",", Enumerable.Range(0,array.arraySize).Select(i => array.GetArrayElementAtIndex(i).stringValue)) + "] expected=[" + string.Join(",", expected) + "] shader=" + material.shader.name);
                for (int i = 0; i < expected.Length; i++) Equal(array.GetArrayElementAtIndex(i).stringValue == expected[i], name + "[" + i + "]");
            }
            Equal(Property("m_LightmapFlags").intValue == entry.lightmap_flags, "lightmap flags");
            Equal(Property("m_CustomRenderQueue").intValue == entry.render_queue, "render queue");
            Equal(Property("m_EnableInstancingVariants").boolValue == entry.instancing, "instancing");
            Equal(Property("m_DoubleSidedGI").boolValue == entry.double_sided, "double sided GI");
            if (entry.cab == "CAB-a4948dbbfef837f4a78843af204c8064" && entry.path_id == "1419505037369228759")
            {
                Equal(entry.keyword_normalization != null && entry.keyword_normalization.native_valid != null && entry.keyword_normalization.native_invalid != null &&
                    !shader.keywordSpace.FindKeyword("ETC1_EXTERNAL_ALPHA").isValid, "reviewed unused GLES keyword classification");
                Strings("m_ValidKeywords", entry.keyword_normalization.native_valid); Strings("m_InvalidKeywords", entry.keyword_normalization.native_invalid);
            }
            else { Strings("m_ValidKeywords", entry.valid_keywords); Strings("m_InvalidKeywords", entry.invalid_keywords); }
            Strings("disabledShaderPasses", entry.disabled_passes);
            var tags = Property("stringTagMap"); Equal(tags.arraySize == entry.tags.Length, "tag count");
            for (int i = 0; i < entry.tags.Length; i++)
            {
                var pair = tags.GetArrayElementAtIndex(i);
                Equal(pair.FindPropertyRelative("first").stringValue == entry.tags[i].name && pair.FindPropertyRelative("second").stringValue == entry.tags[i].value, "tag " + i);
            }
            SerializedProperty Pairs(string category, int length)
            {
                var array = Property("m_SavedProperties." + category); Equal(array.arraySize == length, category + " length"); return array;
            }
            var texturePairs = Pairs("m_TexEnvs", entry.textures.Length);
            for (int i = 0; i < entry.textures.Length; i++)
            {
                var expected = entry.textures[i]; var pair = texturePairs.GetArrayElementAtIndex(i);
                Equal(pair.FindPropertyRelative("first").stringValue == expected.name, "texture property name " + i);
                var value = pair.FindPropertyRelative("second");
                var target = expected.path.Length > 0 ? AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath(expected.path)) : null;
                Equal(value.FindPropertyRelative("m_Texture").objectReferenceValue == target && (expected.path.Length == 0 || target), expected.name + " texture pointer");
                Equal(value.FindPropertyRelative("m_Scale").vector2Value.Equals(expected.scale) && value.FindPropertyRelative("m_Offset").vector2Value.Equals(expected.offset), expected.name + " scale/offset");
                checkedCount += 4;
            }
            foreach (var category in new[] { "m_Ints", "m_Floats" })
            {
                var expected = category == "m_Ints" ? entry.ints : entry.floats; var pairs = Pairs(category, expected.Length);
                for (int i = 0; i < expected.Length; i++)
                {
                    var pair = pairs.GetArrayElementAtIndex(i); var number = pair.FindPropertyRelative("second");
                    Equal(pair.FindPropertyRelative("first").stringValue == expected[i].name, category + " name " + i);
                    Equal(category == "m_Ints" ? number.intValue == expected[i].int_value : Bits(number.floatValue) == expected[i].value_bits, expected[i].name + " number");
                    checkedCount += 2;
                }
            }
            var colorPairs = Pairs("m_Colors", entry.colors.Length);
            for (int i = 0; i < entry.colors.Length; i++)
            {
                var expected = entry.colors[i]; var pair = colorPairs.GetArrayElementAtIndex(i);
                Equal(pair.FindPropertyRelative("first").stringValue == expected.name, "color name " + i);
                var components = pair.FindPropertyRelative("second");
                Color actual = new Color(components.FindPropertyRelative("r").floatValue, components.FindPropertyRelative("g").floatValue,
                    components.FindPropertyRelative("b").floatValue, components.FindPropertyRelative("a").floatValue);
                Equal(Bits(actual.r) == expected.r_bits && Bits(actual.g) == expected.g_bits && Bits(actual.b) == expected.b_bits && Bits(actual.a) == expected.a_bits,
                    expected.name + " color bits actual=" + Bits(actual.r) + "," + Bits(actual.g) + "," + Bits(actual.b) + "," + Bits(actual.a) +
                    " expected=" + expected.r_bits + "," + expected.g_bits + "," + expected.b_bits + "," + expected.a_bits);
                checkedCount += 2;
            }
            Equal(Property("m_BuildTextureStacks").arraySize == 0, "texture stacks");
            return checkedCount;
        }
        private static void Check(Output output)
        {
            string path = Path.Combine(Repository, output.path);
            if (Hash(File.ReadAllBytes(path)) != output.sha256 || (!string.IsNullOrEmpty(output.meta_sha256) && Hash(File.ReadAllBytes(path + ".meta")) != output.meta_sha256))
                throw new InvalidDataException("Original material proof hash differs: " + output.path);
        }
        private static Output Record(string path) => new Output { path = "unity/" + path, sha256 = Hash(File.ReadAllBytes(path)), meta_sha256 = Hash(File.ReadAllBytes(path + ".meta")) };
        internal static string Hash(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
    }
}
