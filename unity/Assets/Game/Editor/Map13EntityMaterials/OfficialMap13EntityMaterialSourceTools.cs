using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor.Map13EntityMaterials
{
    public static class OfficialMap13EntityMaterialSourceTools
    {
        [Serializable] private sealed class EngineBinding
        {
            public string original_name, native_name, asset_path, guid;
            public long file_id;
        }
        [Serializable] private sealed class Template
        {
            public int mapClassId = 13;
            public string manifest = "871_156598", unity_version, template_path, template_sha256;
            public EngineBinding[] engine_shaders;
        }
        public static void ExportTemplateAndEngineBindings()
        {
            var root = new DirectoryInfo(Application.dataPath);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "official-source.json"))) root = root.Parent;
            if (root == null) throw new InvalidDataException("Official Map13 repository was not found.");
            string cache = Path.Combine(root.FullName, ".tools/map13-entity-native-cache-871_156598");
            Directory.CreateDirectory(cache);
            string path = Path.Combine(cache, "native-material-template.asset");
            var shader = Shader.Find("Sprites/Default");
            if (!shader) throw new InvalidDataException("Native material serialization reference shader is unavailable.");
            var material = new Material(shader) { name = "Native material serialization reference" };
            try
            {
                UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { material }, path, true);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
            var names = new[] { "GUI/Text Shader", "Hidden/FrameDebuggerRenderTargetDisplay" };
            var bindings = names.Select(name =>
            {
                var native = Shader.Find(name);
                if (!native || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(native, out string guid, out long fileId))
                    throw new InvalidDataException("Original engine shader has no explicit Windows object identity: " + name);
                return new EngineBinding { original_name = name, native_name = native.name,
                    asset_path = AssetDatabase.GetAssetPath(native), guid = guid, file_id = fileId };
            }).ToArray();
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
            var receipt = new Template { unity_version = Application.unityVersion,
                template_path = ".tools/map13-entity-native-cache-871_156598/native-material-template.asset", template_sha256 = hash, engine_shaders = bindings };
            File.WriteAllText(Path.Combine(cache, "native-material-template-source.json"), JsonUtility.ToJson(receipt, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map13 actual Unity material serialization template and two original engine shader identities exported.");
            EditorApplication.Exit(0);
        }
        public static void ExportNonFiniteTemplate()
        {
            var root = new DirectoryInfo(Application.dataPath);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "official-source.json"))) root = root.Parent;
            if (root == null) throw new InvalidDataException("Map13 source repository was not found.");
            string path = Path.Combine(root.FullName, ".tools/map13-entity-native-cache-871_156598/native-nonfinite-material-template.asset");
            var material = new Material(Shader.Find("Sprites/Default"));
            material.SetColor("_Color", new Color(-0.0f, float.PositiveInfinity, float.NegativeInfinity, 1));
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Official/World/Map/13/NativeEntityTextures/092ba36b6fe3c090e7c0b35cbea6732e/1801756418716178170.texture.asset");
            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { material }, path, true);
            path = Path.ChangeExtension(path, ".binary.asset");
            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { material }, path, false);
            var restored = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(path).OfType<Material>().Single();
            Color actual = restored.GetColor("_Color");
            var serialized = new SerializedObject(restored);
            var value = serialized.FindProperty("m_SavedProperties.m_Colors.Array.data[0].second");
            float scalar = value.FindPropertyRelative("g").floatValue;
            Debug.Log("Map13 nonfinite scalar probe: GetColor=" + actual + "; GetVector=" + restored.GetVector("_Color") +
                "; serialized.colorValue=" + value.colorValue + "; serialized scalar=" + scalar + "; bits=" + BitConverter.ToInt32(BitConverter.GetBytes(scalar), 0));
            if (!float.IsPositiveInfinity(scalar) || !float.IsNegativeInfinity(value.FindPropertyRelative("b").floatValue) ||
                BitConverter.ToInt32(BitConverter.GetBytes(value.FindPropertyRelative("r").floatValue),0) != int.MinValue)
            { Debug.LogError("Actual Unity nonfinite material scalar did not roundtrip."); EditorApplication.Exit(1); return; }
            UnityEngine.Object.DestroyImmediate(restored); UnityEngine.Object.DestroyImmediate(material);
            Debug.Log("Map13 actual Unity nonfinite material scalar serialization roundtrip passed.");
            EditorApplication.Exit(0);
        }
    }
}
