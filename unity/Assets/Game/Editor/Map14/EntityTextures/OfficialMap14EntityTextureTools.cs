using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map14EntityTextures
{
    public static class OfficialMap14EntityTextureTools
    {
        internal const string CatalogPath = "Assets/Game/Resources/Map14/Native/native-entity-textures-catalog.asset";
        internal const string SourcePath = "assets/reference/official-map/map14-native-entity-textures-source-manifest.json";
        [Serializable] internal sealed class Mip { public int level; public string rgba_sha256; }
        [Serializable] internal sealed class Sampler { public int m_FilterMode, m_Aniso, m_WrapU, m_WrapV, m_WrapW; public float m_MipBias; }
        [Serializable] internal sealed class Fields { public string m_Name; public int m_Width, m_Height, m_MipCount, m_ColorSpace; public bool m_IsReadable; public Sampler m_TextureSettings; }
        [Serializable] internal sealed class Texture { public string cab, path_id, path; public int native_format; public Fields fields; public Mip[] mips; }
        [Serializable] internal sealed class Output { public string path, sha256, meta_sha256; }
        [Serializable] internal sealed class Counts { public int original_textures, native_textures, empty_dynamic_atlases, mips; }
        [Serializable] internal sealed class Source { public string manifest; public int mapClassId; public Texture[] textures; public Output[] outputs; public Counts counts; }
        [Serializable] private sealed class Binding
        {
            public int mapClassId = 14;
            public string manifest = "871_156598", source_sha256, importer_sha256;
            public Output catalog;
            public Output[] imported;
            public int deferred_empty_dynamic_atlases;
        }
        internal static string Repository
        {
            get
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "official-source.json"))) directory = directory.Parent;
                return directory?.FullName ?? throw new InvalidDataException("Original Map14 source repository was not found.");
            }
        }
        internal static Source ReadSource()
        {
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(Repository, SourcePath)));
            if (source.manifest != "871_156598" || source.mapClassId != 14 || source.textures.Length != 1644 ||
                source.outputs.Length != 1644 || source.counts.original_textures != 1650 || source.counts.empty_dynamic_atlases != 6)
                throw new InvalidDataException("Original Map14 material texture inventory differs.");
            return source;
        }
        internal static string Identity(Texture texture) => texture.cab + ":Texture2D:" + texture.path_id;
        public static void ImportAndRun()
        {
            RestoreAndValidate();
            OfficialMap14EntityTextureAcceptance.Run();
        }
        public static void RestoreAndValidate()
        {
            Source source = ReadSource();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var output in source.outputs)
            {
                if (Hash(File.ReadAllBytes(Path.Combine(Repository, output.path))) != output.sha256 ||
                    Hash(File.ReadAllBytes(Path.Combine(Repository, output.path + ".meta"))) != output.meta_sha256)
                    throw new InvalidDataException("Original native texture bytes changed: " + output.path);
            }
            var bindings = source.textures.Select(record =>
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(record.path.Substring("unity/".Length));
                ValidateTexture(record, texture);
                return new NativeMap13AssetCatalog.Binding { OfficialPath = Identity(record), Asset = texture };
            }).ToArray();
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(CatalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.Bindings = bindings;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            if (Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-entity-textures-catalog") != catalog ||
                bindings.Any(b => catalog.Load<Texture2D>(b.OfficialPath) != b.Asset))
                throw new InvalidDataException("Original material textures are not explicitly bound by CAB/PathID.");
            var binding = new Binding { source_sha256 = Hash(File.ReadAllBytes(Path.Combine(Repository, SourcePath))),
                importer_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map14/EntityTextures/OfficialMap14EntityTextureTools.cs")),
                catalog = Record(CatalogPath), imported = source.outputs, deferred_empty_dynamic_atlases = 6 };
            bool isolated = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/");
            File.WriteAllText(Path.Combine(Repository, isolated ? ".tools/map14-entity-native-cache-871_156598/entity-textures-binding-source-manifest.json" :
                "assets/reference/official-map/map14-native-entity-textures-binding-source-manifest.json"), JsonUtility.ToJson(binding, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map14 original material textures imported: 1644 explicit native bindings; six empty original dynamic atlas descriptors await original font initialization.");
        }
        internal static void ValidateTexture(Texture record, Texture2D texture)
        {
            Fields fields = record.fields; Sampler sampler = fields.m_TextureSettings;
            if (!texture || texture.name != fields.m_Name || texture.width != fields.m_Width || texture.height != fields.m_Height ||
                texture.mipmapCount != fields.m_MipCount || texture.format != (TextureFormat)record.native_format || texture.isReadable != fields.m_IsReadable ||
                texture.filterMode != (FilterMode)sampler.m_FilterMode || texture.anisoLevel != sampler.m_Aniso || texture.mipMapBias != sampler.m_MipBias ||
                texture.wrapModeU != (TextureWrapMode)sampler.m_WrapU || texture.wrapModeV != (TextureWrapMode)sampler.m_WrapV || texture.wrapModeW != (TextureWrapMode)sampler.m_WrapW ||
                (record.native_format == 4 && UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat) != (fields.m_ColorSpace == 1)))
                throw new InvalidDataException("Original native texture settings differ: " + Identity(record));
        }
        private static Output Record(string path) => new Output { path = "unity/" + path, sha256 = Hash(File.ReadAllBytes(path)), meta_sha256 = Hash(File.ReadAllBytes(path + ".meta")) };
        internal static string Hash(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
