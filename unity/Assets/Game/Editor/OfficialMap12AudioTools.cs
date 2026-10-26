using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Zjcs.Editor
{
    public static class OfficialMap12AudioTools
    {
        [Serializable] private sealed class FileRecord { public string path, sha256, meta_sha256; }
        [Serializable] private sealed class Source { public FileRecord[] outputs; public FileRecord generator; }
        [Serializable] private sealed class Clip
        {
            public FileRecord asset;
            public string resource, loadType;
            public int samples, channels, frequency;
        }
        [Serializable] private sealed class Receipt
        {
            public int mapClassId = 12;
            public string manifest = "871_156598";
            public FileRecord[] sources, adapters;
            public Clip[] clips;
        }

        public static void ImportAndValidate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string root = Path.GetFullPath("..");
            FileRecord Record(string path) => new FileRecord { path = path, sha256 = Hash(Path.Combine(root, path)),
                meta_sha256 = File.Exists(Path.Combine(root, path + ".meta")) ? Hash(Path.Combine(root, path + ".meta")) : null };
            var sources = new[] { "assets/reference/official-map/map12-audio-source-manifest.json",
                "assets/reference/official-map/map12-dialogue-audio-source-manifest.json" };
            var records = sources.SelectMany(path =>
            {
                var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(root, path)));
                if (Hash(Path.Combine(root, source.generator.path)) != source.generator.sha256)
                    throw new InvalidDataException("Original audio generator changed: " + path);
                foreach (var output in source.outputs)
                    if (Hash(Path.Combine(root, output.path)) != output.sha256)
                        throw new InvalidDataException("Original audio payload changed: " + output.path);
                return source.outputs;
            }).ToArray();
            var clips = records.Where(r => r.path.EndsWith(".ogg", StringComparison.Ordinal)).Select(record =>
            {
                string path = record.path.Substring(6);
                string resource = path.Substring(path.IndexOf("/Resources/", StringComparison.Ordinal) + 11);
                resource = resource.Substring(0, resource.Length - 4);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!clip || clip.samples <= 0 || clip.channels <= 0 || clip.frequency <= 0
                    || Resources.Load<AudioClip>(resource) != clip)
                    throw new InvalidDataException("Original local audio binding failed: " + resource);
                if (resource.Contains("/Voices/") && clip.loadType != AudioClipLoadType.Streaming)
                    throw new InvalidDataException("Original voice must stream from its packaged local clip: " + resource);
                return new Clip { asset = Record(record.path), resource = resource, loadType = clip.loadType.ToString(),
                    samples = clip.samples, channels = clip.channels, frequency = clip.frequency };
            }).ToArray();
            if (clips.Length != 237 || clips.Count(c => c.resource.Contains("/Voices/")) != 236)
                throw new InvalidDataException("Original Map12 BGM/voice closure changed.");
            string[] adapters = {
                "unity/Assets/Game/Runtime/Presentation/World/OfficialMap11MusicPlayer.cs",
                "unity/Assets/Game/Runtime/Presentation/World/OfficialMap11StoryAudioPlayer.cs",
                "unity/Assets/Game/Runtime/Presentation/World/OfficialWorldVoiceGain.cs",
                "unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Audio.cs",
                "unity/Assets/Game/Editor/OfficialMap12AudioTools.cs",
                "unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.Audio.cs" };
            var result = new Receipt { sources = sources.Select(Record).ToArray(), clips = clips,
                adapters = adapters.Select(Record).ToArray() };
            File.WriteAllText(Path.Combine(root, "assets/reference/official-map/map12-audio-binding-source-manifest.json"),
                JsonUtility.ToJson(result, true) + "\n", new UTF8Encoding(false));
            ProjectTools.Validate();
            Debug.Log("Map12 original audio bound: one BGM and 236 voices, explicit local Resources paths, actual Windows AudioClip metadata and migrated adapter hashes.");
        }

        private static string Hash(string path)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
}
