using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14ButterflyTools
    {
        public const string Folder = "Assets/Official/World/Map/14/NativeButterflies/";
        public const string SourcePath = Folder + "models.json";
        public const string ProofPath = "../assets/reference/official-map/map14-native-butterflies-source-manifest.json";

        public static void ImportAndValidate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var proof = JsonUtility.FromJson<SourceReceipt>(File.ReadAllText(ProofPath));
            if (proof.mapClassId != 14 || proof.manifest != "871_156598") throw new InvalidDataException("Original butterfly proof identity differs.");
            foreach (var record in proof.outputs.Concat(proof.shaders.Select(s => new Record { path = s.path, sha256 = s.sha256 })))
            {
                string path = "../" + record.path;
                if (Hash(path) != record.sha256 || (!string.IsNullOrEmpty(record.meta_sha256) && Hash(path + ".meta") != record.meta_sha256))
                    throw new InvalidDataException("Original butterfly source changed: " + record.path);
            }
            foreach (var record in proof.shaders)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(record.path.Substring("unity/".Length));
                var data = ShaderUtil.GetShaderData(shader);
                foreach (var variant in record.programs)
                    foreach (var stage in new[] { UnityEditor.Rendering.ShaderType.Vertex, UnityEditor.Rendering.ShaderType.Fragment })
                    {
                        var result = data.GetSubshader(0).GetPass(variant.pass_index).CompileVariant(stage, variant.keywords,
                            UnityEditor.Rendering.ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64, UnityEngine.Rendering.GraphicsTier.Tier2, true);
                        if (!result.Success) throw new InvalidDataException("Original butterfly shader variant failed: " + record.path + ":" + variant.pass_index + ":" +
                            stage + ":" + string.Join(",", variant.keywords) + ":" + string.Join("\n", result.Messages.Select(m => m.message)));
                    }
            }
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(SourcePath);
            var payload = JsonUtility.FromJson<Payload>(source.text);
            var outputs = new List<Record>();
            foreach (var model in payload.models)
            {
                string path = Folder + model.name + "-data.asset";
                var asset = AssetDatabase.LoadAssetAtPath<NativeMap14ButterflyData>(path);
                if (!asset)
                {
                    asset = ScriptableObject.CreateInstance<NativeMap14ButterflyData>();
                    AssetDatabase.CreateAsset(asset, path);
                }
                asset.Source = source; asset.SourceSha256 = Hash(SourcePath); asset.ModelName = model.name;
                asset.Material = AssetDatabase.LoadAssetAtPath<Material>(model.materialPath.Substring("unity/".Length));
                EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
                var data = asset.Load();
                if (data.bones.Length != (model.name == "home_butterfly_SkeletonData" ? 9 : 7))
                    throw new InvalidDataException("Original butterfly bone count differs.");
                for (int pass = 0; pass < asset.Material.passCount; pass++)
                    if (!asset.Material.SetPass(pass)) throw new InvalidDataException("Original butterfly shader pass failed.");
                if (ShaderUtil.ShaderHasError(asset.Material.shader)) throw new InvalidDataException("Original butterfly shader did not compile.");
                outputs.Add(FileRecord(path));
                outputs.Add(FileRecord(AssetDatabase.GetAssetPath(asset.Material)));
                outputs.Add(FileRecord(AssetDatabase.GetAssetPath(asset.Material.shader)));
                Debug.Log("Original Map14 butterfly native carrier: " + model.name + ", " + data.bones.Length + " bones, " + data.skins.Length + " skins, " + data.animations.Length + " animations.");
            }
            outputs.Add(FileRecord(SourcePath));
            foreach (string path in new[]
            {
                "Assets/Game/Runtime/Offline/World/NativeMap14/NativeMap14ButterflyData.cs",
                "Assets/Game/Runtime/Offline/World/NativeMap14/NativeMap14ButterflyMesh.cs",
                "Assets/Game/Editor/Map14/OfficialMap14ButterflyTools.cs"
            }) outputs.Add(FileRecord(path));
            var binding = new BindingReceipt { mapClassId = 14, manifest = "871_156598", source = FileRecord(ProofPath), outputs = outputs.ToArray() };
            File.WriteAllText("../assets/reference/official-map/map14-native-butterflies-binding-source-manifest.json", JsonUtility.ToJson(binding, true) + "\n");
            ExportEnvironmentBindings();
            var tools = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Zjcs.Editor.ProjectTools")).Single(t => t != null);
            tools.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }

        private static void ExportEnvironmentBindings()
        {
            const string path = "../.tools/map14-native-cache-871_156598/scene-script-bindings.json";
            var bindings = JsonUtility.FromJson<Bindings>(File.ReadAllText(path)).scripts.ToDictionary(b => b.original_type);
            var names = new HashSet<string> { "CullingGroupWrapper", "MapEffect", "MapLight", "MapRegionLight", "PartialSubmersion" };
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                Type type = script ? script.GetClass() : null;
                if (type == null || !(type.Namespace?.StartsWith("Zjcs.Official.World.Native", StringComparison.Ordinal) == true) || !names.Contains(type.Name) ||
                    !typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string actualGuid, out long fileId) || fileId != 11500000)
                    throw new InvalidDataException("Original environment component identity is missing.");
                bindings[type.Name] = new Binding { original_type = type.Name, native_type = type.FullName,
                    script_guid = actualGuid, script_file_id = fileId, script_asset = assetPath };
                names.Remove(type.Name);
            }
            if (names.Count != 0) throw new InvalidDataException("Original Map14 environment component is unbound: " + string.Join(",", names));
            File.WriteAllText(path, JsonUtility.ToJson(new Bindings { scripts = bindings.Values.OrderBy(b => b.original_type).ToArray() }, true) + "\n");
        }

        private static string Hash(string path)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        private static Record FileRecord(string path) => new Record
        {
            path = Path.GetFullPath(path).Substring(Path.GetFullPath("..").Length + 1).Replace('\\', '/'),
            sha256 = Hash(path), meta_sha256 = File.Exists(path + ".meta") ? Hash(path + ".meta") : ""
        };
        [Serializable] private sealed class Payload { public NativeMap14ButterflyData.Model[] models; }
        [Serializable] private sealed class Record { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Program { public int pass_index; public string[] keywords; }
        [Serializable] private sealed class ShaderRecord { public string path, sha256; public Program[] programs; }
        [Serializable] private sealed class SourceReceipt { public int mapClassId; public string manifest; public Record[] outputs; public ShaderRecord[] shaders; }
        [Serializable] private sealed class BindingReceipt { public int mapClassId; public string manifest; public Record source; public Record[] outputs; }
        [Serializable] private sealed class Binding { public string original_type, native_type, script_guid, script_asset; public long script_file_id; }
        [Serializable] private sealed class Bindings { public Binding[] scripts; }
    }
}
