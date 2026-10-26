using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13RenderPipelineTools
    {
        [Serializable] private sealed class Template
        {
            public string type, path, sha256, script_guid;
            public long script_file_id;
        }
        [Serializable] private sealed class Templates { public Template[] templates; }
        [Serializable] private sealed class Field
        {
            public string path, kind, value, target_path, guid;
            public long file_id;
            public int length;
        }
        [Serializable] private sealed class Asset
        {
            public string path, type, name, script_guid;
            public long script_file_id;
            public Field[] fields;
        }
        [Serializable] private sealed class Reference { public int path_id; public string path, guid; public long file_id; }
        [Serializable] private sealed class Bindings { public Asset[] assets; public Reference[] catalog; }
        [Serializable] private sealed class Output { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Receipt { public Output[] outputs; }
        [Serializable] private sealed class TextureRecord
        {
            public string path, sha256, meta_sha256, pixel_sha256;
            public int width, height, mip_count, format, filter, wrapU, wrapV, wrapW, aniso;
            public bool sRGB, readable;
            public float mipBias;
        }
        [Serializable] private sealed class TextureSource { public TextureRecord[] outputs; }
        [Serializable] private sealed class BindingReceipt
        {
            public int mapClassId = 13, original_fields, dependency_count;
            public string manifest = "871_156598", converter_receipt_sha256, texture_receipt_sha256, validator_sha256;
            public Output[] imported;
            public Output catalog;
        }

        public static void ValidateProject()
        {
            RestoreAndValidate();
            OfficialMap13NativePipelineShaderTools.Validate();
            OfficialMap13PipelineShaderTools.ValidateProject();
        }

        public static void RestoreAndValidate()
        {
            bool isolated = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/");
            string root = Path.GetFullPath(isolated ? "../../.." : "..");
            string receiptPath = Path.Combine(root, "assets/reference/official-map/map13-native-pipeline-source-manifest.json");
            string texturePath = Path.Combine(root, "assets/reference/official-map/map13-pipeline-textures-source-manifest.json");
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(receiptPath));
            var textures = JsonUtility.FromJson<TextureSource>(File.ReadAllText(texturePath));
            var source = JsonUtility.FromJson<Bindings>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-pipeline-bindings.json"));
            if (receipt.outputs.Length != 4 || source.assets.Length != 4 || source.catalog.Length != 81 || textures.outputs.Length != 47)
                throw new InvalidDataException("Original pipeline dependency closure changed.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var output in receipt.outputs.Concat(textures.outputs.Select(t => new Output { path = t.path, sha256 = t.sha256, meta_sha256 = t.meta_sha256 })))
            {
                string path = Local(output.path);
                if (output.sha256 != null && Hash(File.ReadAllBytes(path)) != output.sha256)
                    throw new InvalidDataException("Original native pipeline bytes changed: " + path);
                if (output.meta_sha256 != null && Hash(File.ReadAllBytes(path + ".meta")) != output.meta_sha256)
                    throw new InvalidDataException("Original native pipeline identity changed: " + path);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!asset) throw new InvalidDataException("Native pipeline asset did not import: " + path);
                var field = new SerializedObject(asset).GetIterator();
                while (field.Next(true))
                    if (field.propertyType == SerializedPropertyType.ObjectReference && !field.objectReferenceValue
                        && field.objectReferenceInstanceIDValue != 0)
                        throw new InvalidDataException("Native pipeline reference is missing: " + path + ":" + field.propertyPath);
            }
            int count = 0;
            foreach (var expected in source.assets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Local(expected.path));
                if (!asset || asset.GetType().Name != expected.type || asset.name != expected.name)
                    throw new InvalidDataException("Original pipeline object type/name differs: " + expected.path);
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(MonoScript.FromScriptableObject(asset), out string guid, out long id)
                    || guid != expected.script_guid || id != expected.script_file_id)
                    throw new InvalidDataException("Original pipeline script is not explicitly bound: " + expected.path);
                var data = new SerializedObject(asset);
                foreach (var value in expected.fields)
                {
                    var field = data.FindProperty(value.path);
                    if (field == null) throw new InvalidDataException("Original pipeline field is absent: " + expected.path + ":" + value.path);
                    bool match = false;
                    if (value.kind == "ObjectReference")
                        match = field.propertyType == SerializedPropertyType.ObjectReference &&
                            (value.file_id == 0 ? field.objectReferenceValue == null :
                                field.objectReferenceValue == AssetDatabase.LoadMainAssetAtPath(Local(value.target_path)));
                    else if (value.kind == "Array") match = field.isArray && field.arraySize == value.length;
                    else if (value.kind == "Scalar")
                    {
                        switch (field.propertyType)
                        {
                            case SerializedPropertyType.Boolean: match = field.boolValue == (value.value == "1" || value.value == "True"); break;
                            case SerializedPropertyType.Integer:
                            case SerializedPropertyType.Enum: match = field.longValue == long.Parse(value.value, CultureInfo.InvariantCulture); break;
                            case SerializedPropertyType.Float: match = Math.Abs(field.doubleValue - double.Parse(value.value, CultureInfo.InvariantCulture)) < 0.000001; break;
                            case SerializedPropertyType.String: match = field.stringValue == value.value; break;
                        }
                    }
                    if (!match) throw new InvalidDataException("Original pipeline field differs: " + expected.path + ":" + value.path + " (" + field.propertyType + ")");
                    count++;
                }
            }
            foreach (var expected in textures.outputs)
            {
                string path = Local(expected.path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (!texture || texture.width != expected.width || texture.height != expected.height || texture.mipmapCount != expected.mip_count
                    || (int)texture.format != expected.format || texture.isReadable != expected.readable || texture.isDataSRGB != expected.sRGB
                    || (int)texture.filterMode != expected.filter || (int)texture.wrapModeU != expected.wrapU || (int)texture.wrapModeV != expected.wrapV
                    || (int)texture.wrapModeW != expected.wrapW || texture.anisoLevel != expected.aniso || Math.Abs(texture.mipMapBias - expected.mipBias) > 0.000001)
                    throw new InvalidDataException("Original pipeline texture sampling/format differs: " + path);
                string hex = Regex.Match(File.ReadAllText(path), @"(?m)^  _typelessdata: ([a-f0-9]+)$").Groups[1].Value;
                byte[] raw = new byte[hex.Length / 2];
                for (int i = 0; i < raw.Length; i++) raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                if (Hash(raw) != expected.pixel_sha256) throw new InvalidDataException("Original pipeline texture raw pixels differ: " + path);
            }
            const string catalogPath = "Assets/Game/Resources/Map13/Native/native-render-pipeline-catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(catalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.Bindings = source.catalog.Select(reference =>
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(Local(reference.path));
                if (!asset || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id)
                    || guid != reference.guid || id != reference.file_id)
                    throw new InvalidDataException("Original native pipeline dependency identity differs: " + reference.path);
                return new NativeMap13AssetCatalog.Binding { OfficialPath = "globalgamemanagers.assets:" + reference.path_id, Asset = asset };
            }).ToArray();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            if (Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-render-pipeline-catalog") != catalog ||
                !catalog.Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209"))
                throw new InvalidDataException("Original native pipeline local resource catalog is unavailable.");
            Output Record(string path) => new Output { path = "unity/" + path, sha256 = Hash(File.ReadAllBytes(path)), meta_sha256 = Hash(File.ReadAllBytes(path + ".meta")) };
            var binding = new BindingReceipt { original_fields = count, dependency_count = source.catalog.Length,
                imported = receipt.outputs.Concat(textures.outputs.Select(t => new Output { path = t.path })).Select(o => Record(Local(o.path))).ToArray(),
                catalog = Record(catalogPath), converter_receipt_sha256 = Hash(File.ReadAllBytes(receiptPath)), texture_receipt_sha256 = Hash(File.ReadAllBytes(texturePath)),
                validator_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map13/OfficialMap13RenderPipelineTools.cs")) };
            File.WriteAllText(Path.Combine(root, isolated ? ".tools/map13-native-cache-871_156598/native-pipeline-binding-source-manifest.json" :
                "assets/reference/official-map/map13-native-pipeline-binding-source-manifest.json"), JsonUtility.ToJson(binding, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map13 native render pipeline imported: " + count + " original fields, 4 pipeline objects, 47 unchanged textures, 81 explicit local dependencies. Graphics activation and full scene rendering remain unverified.");
        }

        private static string Local(string path) => path.StartsWith("unity/", StringComparison.Ordinal) ? path.Substring(6) : throw new InvalidDataException("Non-project pipeline path: " + path);
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        public static void ExportTemplates()
        {
            string root = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/") ? Path.GetFullPath("../../..") : Path.GetFullPath("..");
            string cache = Path.Combine(root, ".tools/map13-native-cache-871_156598/native-pipeline-templates");
            Directory.CreateDirectory(cache);
            var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            var post = ScriptableObject.CreateInstance<PostProcessData>();
            var records = new System.Collections.Generic.List<Template>();
            foreach (var asset in new ScriptableObject[] { renderer, pipeline, post })
            {
                string path = Path.Combine(cache, asset.GetType().Name + ".asset");
                UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { asset }, path, true);
                var script = MonoScript.FromScriptableObject(asset);
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long fileID))
                    throw new InvalidDataException("Native pipeline MonoScript binding is unavailable.");
                using (var hash = SHA256.Create())
                    records.Add(new Template { type = asset.GetType().Name, path = Path.GetRelativePath(root, path).Replace('\\', '/'),
                        sha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(),
                        script_guid = guid, script_file_id = fileID });
            }
            File.WriteAllText(Path.Combine(cache, "templates.json"), JsonUtility.ToJson(new Templates { templates = records.ToArray() }, true) + "\n", new UTF8Encoding(false));
            UnityEngine.Object.DestroyImmediate(pipeline); UnityEngine.Object.DestroyImmediate(renderer); UnityEngine.Object.DestroyImmediate(post);
            Debug.Log("Exported native Unity pipeline/renderer/post-process serialization templates and actual MonoScript references.");
        }
    }
}
