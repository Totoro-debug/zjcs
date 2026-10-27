using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Zjcs.Offline.World.NativeMap13;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14NativeSceneTools
    {
        public static void ImportAndValidate()
        {
            Type.GetType("Zjcs.Editor.OfficialMap14TerrainTools, Zjcs.Editor", true)
                .GetMethod("RestoreAndValidateTerrain").Invoke(null, null);
            RestoreAndValidateScene();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Game/Runtime/Official/World/NativeMap14/Shaders/MovableSprite.shader");
            if (!shader) throw new InvalidDataException("Original movable-sprite shader is not bound.");
            var material = new Material(shader);
            try
            {
                for (int pass = 0; pass < material.passCount; pass++)
                    if (!material.SetPass(pass)) throw new InvalidDataException("Movable-sprite pass failed: " + pass);
                var errors = ShaderUtil.GetShaderMessages(shader).Where(value => value.severity.ToString() == "Error").ToArray();
                if (errors.Length != 0) throw new InvalidDataException(string.Join("\n", errors.Select(value => value.message)));
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
            Debug.Log("Map14 terrain, bare scene and movable-sprite shader imported and validated. Gameplay remains incomplete.");
        }

        [Serializable] private sealed class Binding
        {
            public string original_type, native_type, script_guid, script_asset;
            public long script_file_id;
        }
        [Serializable] private sealed class Bindings { public Binding[] scripts; }

        [Serializable] private sealed class AssetOutput { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class TextureRecord
        {
            public string path, rgba_sha256;
            public int width, height, mip_count, filter, wrapU, wrapV, wrapW, aniso;
            public bool sRGB, readable;
            public float mipBias;
        }
        [Serializable] private sealed class SceneReceipt { public AssetOutput[] outputs; public TextureRecord[] textures; }
        [Serializable] private sealed class SceneBindingReceipt
        {
            public int mapClassId = 14;
            public string manifest = "871_156598";
            public AssetOutput[] imported;
            public AssetOutput catalog;
            public string converter_receipt_sha256, validator_sha256;
        }

        public static void RestoreAndValidateScene()
        {
            string project = Path.GetFullPath(".").Replace('\\', '/');
            bool isolated = project.Contains("/.tools/");
            string root = Path.GetFullPath(isolated ? "../../.." : "..");
            string receiptPath = Path.Combine(root, "assets/reference/official-map/map14-native-scene-source-manifest.json");
            var receipt = JsonUtility.FromJson<SceneReceipt>(File.ReadAllText(receiptPath));
            if (receipt?.outputs?.Length != 270 || receipt.textures.Length != 10)
                throw new InvalidDataException("Original bare prefab dependency closure changed.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var output in receipt.outputs)
            {
                string path = output.path.Substring("unity/".Length);
                Verify(".", path, output.sha256);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!asset) throw new InvalidDataException("Native scene asset failed to import: " + path);
                var property = new SerializedObject(asset).GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null
                        && property.objectReferenceInstanceIDValue != 0)
                        throw new InvalidDataException("Native scene reference is missing: " + path + ":" + property.propertyPath);
            }
            foreach (var source in receipt.textures)
            {
                string path = source.path.Substring("unity/".Length);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (!texture || texture.width != source.width || texture.height != source.height || texture.mipmapCount != source.mip_count
                    || texture.format != TextureFormat.RGBA32 || texture.isReadable != source.readable
                    || texture.isDataSRGB != source.sRGB || (int)texture.filterMode != source.filter
                    || (int)texture.wrapModeU != source.wrapU || (int)texture.wrapModeV != source.wrapV
                    || (int)texture.wrapModeW != source.wrapW || texture.anisoLevel != source.aniso
                    || Math.Abs(texture.mipMapBias - source.mipBias) > 0.00001f)
                    throw new InvalidDataException("Native scene texture mip/color/sampling differs: " + path);
                string hex = Regex.Match(File.ReadAllText(path), @"(?m)^  _typelessdata: ([a-f0-9]+)$").Groups[1].Value;
                if (hex.Length == 0) throw new InvalidDataException("Native scene texture mip bytes are missing: " + path);
                byte[] bytes = new byte[hex.Length / 2];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                if (Hash(bytes) != source.rgba_sha256) throw new InvalidDataException("Native scene decoded mip bytes differ: " + path);
            }
            const string prefabPath = "Assets/Official/World/Map/14/NativeScene/Partition/MapGround_14/MapGround_14_bare.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab) throw new InvalidDataException("Original bare scene prefab is missing.");
            foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    throw new InvalidDataException("Original bare scene has an unbound component: " + transform.name);
            var terrain = Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-terrain-catalog");
            if (!terrain || terrain.Bindings.Length != 241) throw new InvalidDataException("Native terrain catalog is not bound.");
            const string catalogPath = "Assets/Game/Resources/Map14/Native/native-scene-catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(catalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.Bindings = terrain.Bindings.Concat(new[] { new NativeMap13AssetCatalog.Binding {
                OfficialPath = "Assets/Partition/MapGround_14/MapGround_14_bare", Asset = prefab } }).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            if (Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-scene-catalog") != catalog
                || catalog.Load<GameObject>("Assets/Partition/MapGround_14/MapGround_14_bare") != prefab)
                throw new InvalidDataException("Original bare prefab resource path is not bound.");
            AssetOutput Record(string path) => new AssetOutput { path = Path.GetRelativePath(root, Path.GetFullPath(path)).Replace('\\', '/'),
                sha256 = Hash(File.ReadAllBytes(path)), meta_sha256 = Hash(File.ReadAllBytes(path + ".meta")) };
            var bindingReceipt = new SceneBindingReceipt { imported = receipt.outputs.Select(o => Record(o.path.Substring("unity/".Length))).ToArray(),
                catalog = Record(catalogPath), converter_receipt_sha256 = Hash(File.ReadAllBytes(receiptPath)),
                validator_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map14/OfficialMap14NativeSceneTools.cs")) };
            File.WriteAllText(Path.Combine(root, isolated ? ".tools/map14-native-cache-871_156598/native-scene-binding-source-manifest.json"
                : "assets/reference/official-map/map14-native-scene-binding-source-manifest.json"), JsonUtility.ToJson(bindingReceipt, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map14 original bare prefab imported and bound: 270 new native assets, 102 terrain assets reused, 10 textures with complete original mips, no missing scripts, 242 local resource paths. Original pipeline, partition scenes, rendering and gameplay remain unverified.");
        }

        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void Verify(string root, string path, string expected)
        {
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(Path.Combine(root, path))))
                    .Replace("-", "").ToLowerInvariant() != expected)
                    throw new InvalidDataException("Original terrain shader source hash changed: " + path);
        }

        public static void ExportBindings()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string project = Path.GetFullPath(".").Replace('\\', '/');
            string root = Path.GetFullPath(project.Contains("/.tools/") ? "../../.." : "..");
            string cache = Path.Combine(root, ".tools/map14-native-cache-871_156598");
            var known = JsonUtility.FromJson<Bindings>(File.ReadAllText(Path.Combine(cache, "script-bindings.json")))
                .scripts.ToDictionary(item => item.original_type);
            var lightHost = new GameObject("Native component identity reference");
            lightHost.SetActive(false);
            string lightScript;
            try
            {
                var light = lightHost.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
                lightScript = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(light));
                if (string.IsNullOrEmpty(lightScript)) throw new InvalidDataException("Actual Light2D MonoScript is missing.");
            }
            finally { UnityEngine.Object.DestroyImmediate(lightHost); }
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Type type = script?.GetClass();
                if (type == null || !(type.Namespace?.StartsWith("Zjcs.Official.World.Native", StringComparison.Ordinal) == true
                    || type.FullName == "UnityEngine.Rendering.Universal.Light2D")) continue;
                if (type.FullName == "UnityEngine.Rendering.Universal.Light2D" && path != lightScript) continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string actualGuid, out long fileId))
                    throw new InvalidDataException("Original component MonoScript could not be bound: " + type.FullName);
                var binding = new Binding { original_type = type.Name, native_type = type.FullName,
                    script_guid = actualGuid, script_file_id = fileId, script_asset = path };
                if (known.TryGetValue(type.Name, out Binding existing) && existing.script_guid != actualGuid)
                    throw new InvalidDataException("Existing native component script identity changed: " + type.Name);
                known[type.Name] = binding;
            }
            if (!known.ContainsKey("Light2D") || !known.ContainsKey("MapGroundRoot") || !known.ContainsKey("WaterMaskRenderer"))
                throw new InvalidDataException("Native scene component script closure is incomplete.");
            File.WriteAllText(Path.Combine(cache, "scene-script-bindings.json"),
                JsonUtility.ToJson(new Bindings { scripts = known.Values.OrderBy(item => item.original_type).ToArray() }, true) + "\n", new UTF8Encoding(false));
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);
            texture.name = "Native texture serialization reference";
            texture.Apply(false, false);
            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { texture },
                Path.Combine(cache, "native-texture-template.asset"), true);
            UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("Exported actual native scene MonoScript bindings: " + known.Count);
        }
    }
}
