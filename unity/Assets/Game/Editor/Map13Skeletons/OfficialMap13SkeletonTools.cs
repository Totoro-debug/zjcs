using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13Skeletons;

namespace Zjcs.Editor.Map13Skeletons
{
    public static class OfficialMap13SkeletonTools
    {
        public const string SourcePath = "assets/reference/official-map/map13-native-skeleton-models-source-manifest.json";
        public const string CatalogPath = "Assets/Game/Resources/Map13/Native/Skeletons/catalog.asset";
        private const string SourceCatalog = "Assets/Official/World/Map/13/NativeSkeletonModels/catalog.json";
        private const string BindingProof = "assets/reference/official-map/map13-native-skeleton-model-bindings-source-manifest.json";
        [Serializable] public sealed class Output { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Receipt { public int models, renderer_profiles; public Output[] dependencies, outputs; }
        [Serializable] private sealed class Model { public string cab, path_id, name; public Output definition; }
        [Serializable] private sealed class Catalog { public Model[] models; public NativeMap13SkeletonCatalog.Profile[] renderers; }
        [Serializable] private sealed class Proof
        {
            public string manifest = "871_156598", source_sha256, importer_sha256, runtime_sha256, unity_version;
            public int models, renderer_profiles, bones, animations, curves;
            public Output[] outputs;
            public Execution play_mode;
        }
        [Serializable] public sealed class Execution
        {
            public string unity_version, graphics_api, acceptance_sha256, coverage;
            public int models, animations, poses, finite_matrices, original_profiles, rendered_pixels, runtime_errors;
        }
        public static string Repository
        {
            get
            {
                var directory = new DirectoryInfo(Application.dataPath);
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "official-source.json"))) directory = directory.Parent;
                return directory?.FullName ?? throw new InvalidDataException("Map13 repository not found.");
            }
        }
        public static string Hash(byte[] value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }
        private static void Check(Output output)
        {
            if (Hash(File.ReadAllBytes(Path.Combine(Repository, output.path))) != output.sha256)
                throw new InvalidDataException("Original skeleton dependency changed: " + output.path);
        }
        public static void ImportAndValidate()
        {
            try { RestoreAndValidate(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void RestoreAndValidate()
        {
            var receipt = JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(Repository, SourcePath)));
            foreach (var output in receipt.dependencies.Concat(receipt.outputs)) Check(output);
            if (receipt.models != 27 || receipt.renderer_profiles != 57) throw new InvalidDataException("Original prop inventory differs.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)); AssetDatabase.Refresh();
            var original = JsonUtility.FromJson<Catalog>(File.ReadAllText(SourceCatalog));
            var models = new List<NativeMap13SkeletonCatalog.Model>(); var outputs = new List<string>();
            int curves = 0, bones = 0, animations = 0;
            foreach (var entry in original.models)
            {
                var source = JsonUtility.FromJson<NativeMap13SkeletonSource>(File.ReadAllText(entry.definition.path.Substring(6)));
                if (source.constraints.Length != 0 || source.slots.Any(s => s.blend != "normal")) throw new InvalidDataException("Unrestored prop constraints/blend.");
                foreach (var animation in source.animations)
                foreach (var track in animation.tracks) { track.Curve = ConvertCurve(track.keys); curves++; }
                string key = Path.GetFileNameWithoutExtension(entry.definition.path);
                string path = Path.GetDirectoryName(CatalogPath).Replace('\\', '/') + "/" + key + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<NativeMap13SkeletonData>(path);
                if (!data) { data = ScriptableObject.CreateInstance<NativeMap13SkeletonData>(); AssetDatabase.CreateAsset(data, path); }
                data.name = source.name; data.Source = source; data.Material = AssetDatabase.LoadAssetAtPath<Material>(source.materialPath);
                if (!data.Material) throw new InvalidDataException("Original prop material is not bound: " + source.materialPath);
                EditorUtility.SetDirty(data); models.Add(new NativeMap13SkeletonCatalog.Model { key = key, data = data }); outputs.Add(path);
                bones += source.bones.Length; animations += source.animations.Length;
            }
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13SkeletonCatalog>(CatalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13SkeletonCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.models = models.ToArray(); catalog.renderers = original.renderers; EditorUtility.SetDirty(catalog); outputs.Add(CatalogPath);
            AssetDatabase.SaveAssets();
            WriteProof(new Proof { source_sha256 = Hash(File.ReadAllBytes(Path.Combine(Repository, SourcePath))),
                importer_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map13Skeletons/OfficialMap13SkeletonTools.cs")),
                runtime_sha256 = Hash(File.ReadAllBytes("Assets/Game/Runtime/Offline/World/NativeMap13Skeletons/NativeMap13SkeletonView.cs")),
                unity_version = Application.unityVersion, models = models.Count, renderer_profiles = original.renderers.Length,
                curves = curves, bones = bones, animations = animations,
                outputs = outputs.Select(p => new Output { path = "unity/" + p, sha256 = Hash(File.ReadAllBytes(p)), meta_sha256 = Hash(File.ReadAllBytes(p + ".meta")) }).ToArray() });
            Debug.Log($"Original Map13 native skeletons imported: {models.Count} models, {bones} bones, {animations} animations, {curves} curves, {original.renderers.Length} profiles.");
        }
        public static void RecordExecution(Execution execution)
        {
            string path = Path.Combine(Repository, BindingProof); var proof = JsonUtility.FromJson<Proof>(File.ReadAllText(path));
            proof.play_mode = execution; WriteProof(proof);
        }
        private static void WriteProof(Proof proof)
        {
            string session = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR") ?? throw new InvalidDataException("Validation workspace is not set.");
            string validationRoot = Path.GetFullPath(Path.Combine(Repository, "output/validation")) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(session).StartsWith(validationRoot, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(session))
                throw new InvalidDataException("Use an existing validation session inside output/validation.");
            string path = Path.Combine(Repository, BindingProof), temp = Path.Combine(session, "map13-skeleton-bindings.tmp");
            File.WriteAllText(temp, JsonUtility.ToJson(proof, true) + "\n", new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        public static AnimationCurve ConvertCurve(NativeMap13CurveKey[] keys)
        {
            // Reuse the project's independent summon converter, with the same source
            // cubic verification. No third-party animation runtime participates.
            var frames = keys.Select(k => new Keyframe(k.time, k.value) { weightedMode = WeightedMode.Both, inWeight = 1f / 3f, outWeight = 1f / 3f }).ToArray();
            for (int i = 0; i < frames.Length - 1; i++)
            {
                var a = frames[i]; var b = frames[i + 1]; float duration = b.time - a.time;
                if (duration <= 0) throw new InvalidDataException("Original curve times are not increasing.");
                var control = keys[i].curve;
                if (keys[i].stepped) a.outTangent = float.PositiveInfinity;
                else if (control.Length == 0) a.outTangent = b.inTangent = (b.value - a.value) / duration;
                else
                {
                    if (control.Length != 4) throw new InvalidDataException("Original Bezier control count differs.");
                    float first = control[0] - a.time, last = b.time - control[2];
                    if (first <= 0 || last <= 0 || first > duration || last > duration) throw new InvalidDataException("Original Bezier time handles differ.");
                    a.outWeight = first / duration; b.inWeight = last / duration;
                    a.outTangent = (control[1] - a.value) / first; b.inTangent = (b.value - control[3]) / last;
                }
                frames[i] = a; frames[i + 1] = b;
            }
            var result = new AnimationCurve(frames);
            for (int i = 0; i < keys.Length - 1; i++)
            {
                var a = keys[i]; var b = keys[i + 1]; if (a.stepped || a.curve.Length != 4) continue;
                foreach (float u in new[] { .17f, .37f, .71f })
                {
                    float v = 1 - u, time = v * v * v * a.time + 3 * v * v * u * a.curve[0] + 3 * v * u * u * a.curve[2] + u * u * u * b.time;
                    float expected = v * v * v * a.value + 3 * v * v * u * a.curve[1] + 3 * v * u * u * a.curve[3] + u * u * u * b.value;
                    if (Mathf.Abs(result.Evaluate(time) - expected) > .002f * Mathf.Max(1, Mathf.Abs(expected))) throw new InvalidDataException("Original Bezier differs after native curve import.");
                }
            }
            return result;
        }
    }
}
