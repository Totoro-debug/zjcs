using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13NativeSceneTools
    {
        [Serializable] private sealed class Binding
        {
            public string original_type, native_type, script_guid, script_asset;
            public long script_file_id;
        }
        [Serializable] private sealed class Bindings { public Binding[] scripts; }
        [Serializable] private sealed class Program { public int pass_index; public string[] keywords; public string path, sha256; }
        [Serializable] private sealed class Input { public Program[] programs; }
        [Serializable] private sealed class Output { public string path, sha256, original_shader; }
        [Serializable] private sealed class ShaderReceipt { public Input[] inputs; public Output[] outputs; }
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
            public int mapClassId = 13;
            public string manifest = "871_156598";
            public AssetOutput[] imported;
            public AssetOutput catalog;
            public string converter_receipt_sha256, validator_sha256;
        }

        public static void ValidateProject()
        {
            OfficialMap13EnvironmentTools.RestoreAndValidate();
            RestoreAndValidateScene();
            ValidateShaders();
            OfficialMap13LightShaderTools.ValidateProject();
        }

        public static void RestoreAndValidateScene()
        {
            string project = Path.GetFullPath(".").Replace('\\', '/');
            bool isolated = project.Contains("/.tools/");
            string root = Path.GetFullPath(isolated ? "../../.." : "..");
            string receiptPath = Path.Combine(root, "assets/reference/official-map/map13-native-scene-source-manifest.json");
            var receipt = JsonUtility.FromJson<SceneReceipt>(File.ReadAllText(receiptPath));
            if (receipt?.outputs?.Length != 228 || receipt.textures.Length != 7)
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
            const string prefabPath = "Assets/Official/World/Map/13/NativeScene/Partition/MapGround_13/MapGround_13_bare.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab) throw new InvalidDataException("Original bare scene prefab is missing.");
            foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    throw new InvalidDataException("Original bare scene has an unbound component: " + transform.name);
            var terrain = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-terrain-catalog");
            if (!terrain || terrain.Bindings.Length != 201) throw new InvalidDataException("Native terrain catalog is not bound.");
            const string catalogPath = "Assets/Game/Resources/Map13/Native/native-scene-catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(catalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.Bindings = terrain.Bindings.Concat(new[] { new NativeMap13AssetCatalog.Binding {
                OfficialPath = "Assets/Partition/MapGround_13/MapGround_13_bare", Asset = prefab } }).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            if (Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-scene-catalog") != catalog
                || catalog.Load<GameObject>("Assets/Partition/MapGround_13/MapGround_13_bare") != prefab)
                throw new InvalidDataException("Original bare prefab resource path is not bound.");
            AssetOutput Record(string path) => new AssetOutput { path = Path.GetRelativePath(root, Path.GetFullPath(path)).Replace('\\', '/'),
                sha256 = Hash(File.ReadAllBytes(path)), meta_sha256 = Hash(File.ReadAllBytes(path + ".meta")) };
            var bindingReceipt = new SceneBindingReceipt { imported = receipt.outputs.Select(o => Record(o.path.Substring("unity/".Length))).ToArray(),
                catalog = Record(catalogPath), converter_receipt_sha256 = Hash(File.ReadAllBytes(receiptPath)),
                validator_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map13/OfficialMap13NativeSceneTools.cs")) };
            File.WriteAllText(Path.Combine(root, isolated ? ".tools/map13-native-cache-871_156598/native-scene-binding-source-manifest.json"
                : "assets/reference/official-map/map13-native-scene-binding-source-manifest.json"), JsonUtility.ToJson(bindingReceipt, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map13 original bare prefab imported and bound: 228 new native assets, 93 terrain assets reused, 7 textures with complete original mips, no missing scripts, 202 local resource paths. Original pipeline, partition scenes, rendering and gameplay remain unverified.");
        }

        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        public static void ValidateShaders()
        {
            string root = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/")
                ? Path.GetFullPath("../../..") : Path.GetFullPath("..");
            var receipt = JsonUtility.FromJson<ShaderReceipt>(File.ReadAllText(Path.Combine(root,
                "assets/reference/official-map/map13-scene-shaders-source-manifest.json")));
            if (receipt?.outputs?.Length != 2 || receipt.inputs.Length != 2)
                throw new InvalidDataException("Original terrain/lava shader closure changed.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            bool previous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            int count = 0;
            try
            {
                for (int index = 0; index < receipt.outputs.Length; index++)
                {
                    var output = receipt.outputs[index];
                    Verify(root, output.path, output.sha256);
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(output.path.Substring("unity/".Length));
                    if (!shader) throw new InvalidDataException("Original terrain shader is missing: " + output.path);
                    foreach (var program in receipt.inputs[index].programs)
                    {
                        Verify(root, program.path, program.sha256);
                        var pass = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(program.pass_index);
                        foreach (ShaderType stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                        {
                            var result = pass.CompileVariant(stage, program.keywords, ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64);
                            if (!result.Success) throw new InvalidDataException(output.original_shader + " pass " + program.pass_index
                                + " [" + string.Join(",", program.keywords) + "] " + stage + ": "
                                + string.Join("\n", result.Messages.Select(item => item.message)));
                        }
                        count++;
                    }
                    if (ShaderUtil.ShaderHasError(shader)) throw new InvalidDataException(string.Join("\n", ShaderUtil.GetShaderMessages(shader).Select(item => item.message)));
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = previous; }
            if (count != 20) throw new InvalidDataException("Original terrain/lava shader variant count changed.");
            Debug.Log("Map13 original terrain/lava shaders compiled: 2 families, 4 passes, 20 variants, both GPU stages for Windows D3D. Native scene rendering remains unverified.");
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
            string cache = Path.Combine(root, ".tools/map13-native-cache-871_156598");
            var known = JsonUtility.FromJson<Bindings>(File.ReadAllText(Path.Combine(cache, "script-bindings.json")))
                .scripts.ToDictionary(item => item.original_type);
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Type type = script?.GetClass();
                if (type == null || !(type.Namespace?.StartsWith("Zjcs.Official.World.Native", StringComparison.Ordinal) == true
                    || type.FullName == "UnityEngine.Rendering.Universal.Light2D")) continue;
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
