using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor
{
    public static class OfficialMap14TerrainTools
    {
        [Serializable] private sealed class Binding
        {
            public string original_type;
            public string native_type;
            public string script_guid;
            public long script_file_id;
            public string script_asset;
        }
        [Serializable] private sealed class Bindings { public Binding[] scripts; }
        [Serializable] private sealed class TileReferences
        {
            public string path;
            public string[] guids;
            public long[] file_ids;
        }
        [Serializable] private sealed class Output { public string path; public string sha256; public string meta_sha256; }
        [Serializable] private sealed class Texture
        {
            public string path;
            public int width, height, mip_count, filter, wrapU, wrapV, aniso;
            public bool sRGB;
            public float mipBias;
        }
        [Serializable] private sealed class Receipt
        {
            public Output[] outputs;
            public Texture[] textures;
            public TileReferences[] tile_references;
        }
        [Serializable] private sealed class ImportReceipt
        {
            public int mapClassId = 14;
            public string manifest = "871_156598";
            public string converter_receipt_sha256;
            public string validator_sha256;
            public Output[] imported;
            public Output catalog;
        }

        public static void RestoreAndValidateTerrain()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText("../assets/reference/official-map/map14-native-terrain-source-manifest.json"));
            Check(receipt.tile_references != null && receipt.tile_references.Length == 240,
                "Original Tile pointer proof is missing.");
            var tileReferences = receipt.tile_references.ToDictionary(value => value.path);
            Type catalogType = Type.GetType("Zjcs.Offline.World.NativeMap13.NativeMap13AssetCatalog, Zjcs.Offline.World.NativeMap13", true);
            const string catalogPath = "Assets/Game/Resources/Map14/Native/native-terrain-catalog.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(catalogPath));
            AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<ScriptableObject>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance(catalogType);
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            var serialized = new SerializedObject(catalog);
            var bindings = serialized.FindProperty("Bindings");
            var resourcePaths = receipt.outputs.Where(output => output.path.Contains("/Partition/MapGround_14/Chunks/") ||
                output.path.EndsWith("/MonoBehaviour/partition_data.asset", StringComparison.Ordinal)).Select(output => output.path).ToHashSet();
            bindings.arraySize = resourcePaths.Count;
            int bindingIndex = 0;
            int chunks = 0, sprites = 0, tiles = 0;
            for (int index = 0; index < receipt.outputs.Length; index++)
            {
                Output output = receipt.outputs[index];
                string path = output.path.Substring("unity/".Length);
                Check(Hash(File.ReadAllBytes(path)) == output.sha256, "Native asset hash differs: " + path);
                // Unity canonicalizes exporter metadata (field order, importer platform entries,
                // generated sprite IDs). Verify identity and actual imported data below; record
                // the resulting metadata hash separately from the converter input hash.
                string exportRelative = path.Substring("Assets/Official/World/Map/14/NativeTerrain/".Length);
                using (var md5 = MD5.Create())
                    Check(AssetDatabase.AssetPathToGUID(path) == Hex(md5.ComputeHash(Encoding.UTF8.GetBytes("zjcs-map14-native-terrain:" + exportRelative))),
                        "Native asset identity changed: " + path);
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                Check(asset != null, "Native asset failed to import: " + path);
                if (asset.GetType().Name == "TileChunkData")
                {
                    chunks++;
                    var chunk = new SerializedObject(asset);
                    var values = chunk.FindProperty("Tiles");
                    TileReferences expected = tileReferences[output.path];
                    Check(values.arraySize == expected.guids.Length && values.arraySize == expected.file_ids.Length,
                        "Original chunk Tile array size changed: " + path);
                    for (int i = 0; i < values.arraySize; i++)
                    {
                        var value = values.GetArrayElementAtIndex(i);
                        if (expected.file_ids[i] == 0)
                            Check(value.objectReferenceValue == null && value.objectReferenceInstanceIDValue == 0,
                                "Original empty Tile slot was replaced: " + path + ":" + i);
                        else
                            Check(value.objectReferenceValue != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                                value.objectReferenceValue, out string guid, out long fileId)
                                && guid == expected.guids[i] && fileId == expected.file_ids[i],
                                "Original Tile pointer is unbound or changed: " + path + ":" + i);
                    }
                }
                if (asset is Sprite sprite)
                {
                    sprites++;
                    Check(sprite.texture != null && sprite.vertices.Length > 0 && sprite.triangles.Length > 0,
                        "Original Sprite mesh/texture failed to import: " + path);
                }
                if (asset is UnityEngine.Tilemaps.Tile tile)
                {
                    tiles++;
                    Check(tile.sprite != null, "Original Tile Sprite is not bound: " + path);
                }
                var properties = new SerializedObject(asset).GetIterator();
                while (properties.Next(true))
                    if (properties.propertyType == SerializedPropertyType.ObjectReference)
                        Check(properties.objectReferenceValue != null || properties.objectReferenceInstanceIDValue == 0,
                            "Broken native reference " + path + ":" + properties.propertyPath);
                string relative = path.Substring("Assets/Official/World/Map/14/NativeTerrain/".Length);
                if (resourcePaths.Contains(output.path))
                {
                    var binding = bindings.GetArrayElementAtIndex(bindingIndex++);
                    binding.FindPropertyRelative("OfficialPath").stringValue = "Assets/" + relative.Substring(0, relative.Length - Path.GetExtension(relative).Length);
                    binding.FindPropertyRelative("Asset").objectReferenceValue = asset;
                }
            }
            Check(chunks == 240, "Original terrain chunk count differs");
            foreach (Texture source in receipt.textures)
            {
                string path = source.path.Substring("unity/".Length);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Check(texture.width == source.width && texture.height == source.height && texture.mipmapCount == source.mip_count,
                    "Original texture dimensions or mip count changed: " + path);
                Check(importer.sRGBTexture == source.sRGB && !importer.alphaIsTransparency &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    (int)texture.filterMode == source.filter && (int)texture.wrapModeU == source.wrapU &&
                    (int)texture.wrapModeV == source.wrapV && texture.anisoLevel == source.aniso &&
                    Math.Abs(texture.mipMapBias - source.mipBias) < 0.00001f,
                    "Original texture sampling/color import settings changed: " + path);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            // Exercise each official resource string against the actual packaged local catalog.
            var load = catalogType.GetMethod("Load").MakeGenericMethod(typeof(UnityEngine.Object));
            foreach (Output output in receipt.outputs)
            {
                if (!resourcePaths.Contains(output.path)) continue;
                string relative = output.path.Substring("unity/Assets/Official/World/Map/14/NativeTerrain/".Length);
                load.Invoke(catalog, new object[] { "Assets/" + relative.Substring(0, relative.Length - Path.GetExtension(relative).Length) });
            }
            var imported = receipt.outputs.Select(output => new Output { path = output.path,
                sha256 = Hash(File.ReadAllBytes(output.path.Substring(6))),
                meta_sha256 = Hash(File.ReadAllBytes(output.path.Substring(6) + ".meta")) }).ToArray();
            File.WriteAllText("../assets/reference/official-map/map14-native-terrain-binding-source-manifest.json",
                JsonUtility.ToJson(new ImportReceipt {
                    converter_receipt_sha256 = Hash(File.ReadAllBytes("../assets/reference/official-map/map14-native-terrain-source-manifest.json")),
                    validator_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/OfficialMap14TerrainTools.cs")),
                    imported = imported,
                    catalog = new Output { path = "unity/" + catalogPath, sha256 = Hash(File.ReadAllBytes(catalogPath)),
                        meta_sha256 = Hash(File.ReadAllBytes(catalogPath + ".meta")) }
                }, true) + "\n");
            Debug.Log($"Map14 native terrain imported and bound: {receipt.outputs.Length} assets, {chunks} chunks, {tiles} Tiles, {sprites} Sprites, {receipt.textures.Length} original textures.");
            ProjectTools.Validate();
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
        }

        private static string Hex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2")));

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }

        public static void ExportScriptBindings()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var bindings = new List<Binding>();
            foreach (string name in new[] { "PartitionData", "TileChunkData", "MyTile", "MapGroundTile",
                "PartitionElement", "PartitionGroup", "PartitionLayerAssigner", "PartitionLoader", "PartitionController" })
            {
                Type type = Type.GetType("Zjcs.Official.World.NativeTerrain." + name + ", Zjcs.Official.World.NativeTerrain", true);
                bindings.Add(Bind(name, type));
            }
            Type tile = Type.GetType("UnityEngine.Tilemaps.Tile, UnityEngine.TilemapModule", true);
            bindings.Add(Bind("UnityEngine.Tilemaps.Tile", tile));
            string output = Path.GetFullPath("../.tools/map14-native-cache-871_156598/script-bindings.json");
            File.WriteAllText(output, JsonUtility.ToJson(new Bindings { scripts = bindings.ToArray() }, true) + "\n");
            Debug.Log("Map14 terrain script bindings resolved against actual Unity MonoScripts: " + bindings.Count);
            ProjectTools.Validate();
        }

        private static Binding Bind(string name, Type type)
        {
            UnityEngine.Object instance = null;
            GameObject host = null;
            try
            {
                MonoScript script;
                if (typeof(ScriptableObject).IsAssignableFrom(type))
                {
                    instance = ScriptableObject.CreateInstance(type);
                    script = MonoScript.FromScriptableObject((ScriptableObject)instance);
                }
                else
                {
                    host = new GameObject("Map14 script binding lookup");
                    host.SetActive(false);
                    script = MonoScript.FromMonoBehaviour((MonoBehaviour)host.AddComponent(type));
                }
                if (script == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long fileId))
                    throw new InvalidDataException("Cannot resolve the actual native MonoScript: " + type.FullName);
                return new Binding { original_type = name, native_type = type.AssemblyQualifiedName,
                    script_guid = guid, script_file_id = fileId, script_asset = AssetDatabase.GetAssetPath(script) };
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
