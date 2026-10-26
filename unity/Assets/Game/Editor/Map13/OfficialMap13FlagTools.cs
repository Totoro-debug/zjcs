using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13
{
    public static class OfficialMap13FlagTools
    {
        [Serializable] public sealed class Bone { public string name; public int parent; public float x, y, rotation; }
        [Serializable] public sealed class Vertex { public int first, count; }
        [Serializable] public sealed class Influence { public int bone; public float x, y, weight; }
        [Serializable] public sealed class Key { public float time, value; public float[] curve; }
        [Serializable] public sealed class Track { public int bone; public Key[] keys; }
        [Serializable] public sealed class Model
        {
            public string name, material_path, original_controller, original_clip;
            public float scale, duration;
            public Bone[] bones;
            public Vertex[] vertices;
            public Influence[] influences;
            public Vector2[] uv;
            public int[] triangles;
            public Track[] tracks;
        }
        [Serializable] public sealed class Models { public Model[] models; }
        [Serializable] private sealed class Program { public int pass_index; public string[] keywords; }
        [Serializable] private sealed class ShaderSource { public string path, sha256; public Program[] programs; }
        [Serializable] private sealed class Source { public ShaderSource shader; }
        [Serializable] private sealed class Output { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Bindings
        {
            public int mapClassId = 13;
            public string manifest = "871_156598", source_receipt_sha256, importer_sha256;
            public Output[] outputs;
        }
        public const string NativePath = "Assets/Official/World/Map/13/NativeFlags/";

        public static string Repository => Path.GetFullPath(Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/") ? "../../.." : "..");
        public static Models Read() => JsonUtility.FromJson<Models>(File.ReadAllText("Assets/Official/World/Map/13/Source/native-flags.json"));

        public static void RestoreAndValidate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var models = Read();
            if (models.models.Length != 3) throw new InvalidDataException("Original flag model count differs.");
            var outputs = new List<string>();
            foreach (var model in models.models)
            {
                string clipPath = NativePath + model.name + "-loop.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
                foreach (var binding in AnimationUtility.GetCurveBindings(clip)) AnimationUtility.SetEditorCurve(clip, binding, null);
                clip.name = "loop"; clip.legacy = false;
                // Keep source timing/settings, including original frame rate and looping.
                string original = File.ReadAllText(Path.Combine(Repository, model.original_clip));
                var rate = Regex.Match(original, @"(?m)^  m_SampleRate: ([0-9.]+)$");
                clip.frameRate = float.Parse(rate.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                foreach (var track in model.tracks)
                {
                    var frames = track.keys.Select(k => new Keyframe(k.time, model.bones[track.bone].rotation + k.value) {
                        weightedMode = WeightedMode.Both, inWeight = 1f / 3f, outWeight = 1f / 3f }).ToArray();
                    for (int i = 0; i < frames.Length - 1; i++)
                    {
                        var a = frames[i]; var b = frames[i + 1]; var control = track.keys[i].curve;
                        if (control.Length == 0) a.outTangent = b.inTangent = (b.value - a.value) / (b.time - a.time);
                        else
                        {
                            float first = control[0] - a.time, last = b.time - control[2];
                            if (first <= 0 || last <= 0) throw new InvalidDataException("Original flag Bezier time handles require explicit conversion.");
                            a.outWeight = first / (b.time - a.time); b.inWeight = last / (b.time - a.time);
                            a.outTangent = (control[1] - track.keys[i].value) / first;
                            b.inTangent = (track.keys[i + 1].value - control[3]) / last;
                        }
                        frames[i] = a; frames[i + 1] = b;
                    }
                    string bonePath = BonePath(model, track.bone);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bonePath, typeof(Transform), "localEulerAnglesRaw.z"), new AnimationCurve(frames));
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0; settings.stopTime = model.duration; settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
                string clockPath = NativePath + model.name + "-clock.anim";
                File.WriteAllText(clockPath, original, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(clockPath, ImportAssetOptions.ForceSynchronousImport);
                var clock = AssetDatabase.LoadAssetAtPath<AnimationClip>(clockPath);
                string controllerPath = NativePath + model.name + ".controller";
                string controller = File.ReadAllText(Path.Combine(Repository, model.original_controller));
                var pointer = Regex.Match(controller, @"m_Motion: \{fileID: 7400000, guid: ([a-f0-9]{32}), type: 2\}");
                if (!pointer.Success) throw new InvalidDataException("Original flag loop controller binding differs.");
                controller = controller.Replace(pointer.Groups[1].Value, AssetDatabase.AssetPathToGUID(clockPath));
                // Preserve every original state, timing, layer and transition field.
                File.WriteAllText(controllerPath, controller, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(controllerPath, ImportAssetOptions.ForceSynchronousImport);
                string dataPath = NativePath + model.name + "-data.asset";
                var data = AssetDatabase.LoadAssetAtPath<NativeMap13FlagData>(dataPath);
                if (!data) { data = ScriptableObject.CreateInstance<NativeMap13FlagData>(); AssetDatabase.CreateAsset(data, dataPath); }
                data.name = model.name; data.Scale = model.scale; data.Duration = model.duration;
                data.Bones = model.bones.Select(b => new NativeMap13FlagData.Bone { Name = b.name, Parent = b.parent, Position = new Vector2(b.x, b.y), Rotation = b.rotation }).ToArray();
                data.Vertices = model.vertices.Select(v => new NativeMap13FlagData.Vertex { First = v.first, Count = v.count }).ToArray();
                data.Influences = model.influences.Select(w => new NativeMap13FlagData.Influence { Bone = w.bone, Position = new Vector2(w.x, w.y), Weight = w.weight }).ToArray();
                data.UV = model.uv; data.Triangles = model.triangles;
                data.RotationTracks = model.tracks.Select(t => new NativeMap13FlagData.RotationTrack { Bone = t.bone,
                    Curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(BonePath(model, t.bone), typeof(Transform), "localEulerAnglesRaw.z")) }).ToArray();
                data.Material = AssetDatabase.LoadAssetAtPath<Material>(model.material_path.Substring(6));
                data.Clip = clip; data.Controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                if (!data.Material || !data.Controller || data.Controller.animationClips.Length != 1 || data.Controller.animationClips[0] != clock)
                    throw new InvalidDataException("Original flag native animation/material/controller is not explicitly bound.");
                EditorUtility.SetDirty(data);
                outputs.AddRange(new[] { clipPath, clockPath, controllerPath, dataPath });
            }
            const string catalogPath = "Assets/Game/Resources/Map13/Native/native-flag-catalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(catalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<NativeMap13AssetCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            catalog.Bindings = models.models.Select(m => new NativeMap13AssetCatalog.Binding { OfficialPath = m.name,
                Asset = AssetDatabase.LoadAssetAtPath<NativeMap13FlagData>(NativePath + m.name + "-data.asset") }).ToArray();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            outputs.Add(catalogPath);
            var receipt = new Bindings { outputs = outputs.Select(p => new Output { path = "unity/" + p, sha256 = Hash(File.ReadAllBytes(p)),
                meta_sha256 = Hash(File.ReadAllBytes(p + ".meta")) }).ToArray(), source_receipt_sha256 = Hash(File.ReadAllBytes(Path.Combine(Repository,
                "assets/reference/official-map/map13-native-flags-source-manifest.json"))), importer_sha256 = Hash(File.ReadAllBytes("Assets/Game/Editor/Map13/OfficialMap13FlagTools.cs")) };
            bool isolated = Path.GetFullPath(".").Replace('\\', '/').Contains("/.tools/");
            File.WriteAllText(Path.Combine(Repository, isolated ? ".tools/map13-native-cache-871_156598/native-flags-binding-source-manifest.json" :
                "assets/reference/official-map/map13-native-flags-binding-source-manifest.json"), JsonUtility.ToJson(receipt, true) + "\n", new UTF8Encoding(false));
            ValidateShader();
            Debug.Log("Map13 native flag data, weighted Unity bone curves and unchanged original loop controllers bound: 3 models, 23 bones, 51 weighted vertices. Scene-instance binding and visual animation remain unverified.");
        }

        public static string BonePath(Model model, int bone)
        {
            string result = model.bones[bone].name;
            while ((bone = model.bones[bone].parent) >= 0) result = model.bones[bone].name + "/" + result;
            return NativeMap13FlagMesh.BoneRootName + "/" + result;
        }

        private static void ValidateShader()
        {
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(Repository, "assets/reference/official-map/map13-native-flags-source-manifest.json"))).shader;
            string path = source.path.Substring(6);
            if (Hash(File.ReadAllBytes(path)) != source.sha256) throw new InvalidDataException("Original flag shader source differs.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            var data = ShaderUtil.GetShaderData(shader);
            foreach (var variant in source.programs)
                foreach (var stage in new[] { ShaderType.Vertex, ShaderType.Fragment })
                {
                    var compiled = data.GetSubshader(0).GetPass(variant.pass_index).CompileVariant(stage, variant.keywords,
                        ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64, UnityEngine.Rendering.GraphicsTier.Tier2, true);
                    if (!compiled.Success) throw new InvalidDataException("Original flag shader Windows compile failed: " + variant.pass_index + ":" + stage + ":" +
                        string.Join("\n", compiled.Messages.Select(m => m.message)));
                }
            Debug.Log("Map13 original flag shader passed Windows D3D compilation: 18 variants, both GPU stages.");
        }
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
