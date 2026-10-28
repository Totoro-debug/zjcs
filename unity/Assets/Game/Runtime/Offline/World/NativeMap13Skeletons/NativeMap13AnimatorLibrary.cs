using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13Skeletons
{
    // Original platform-neutral native animation payloads in a verified Unity-built
    // Windows container. Loading stays local and never requires a Spine runtime.
    public sealed class NativeMap13AnimatorLibrary : IDisposable
    {
        [Serializable] public sealed class Catalog { public Entry[] entries; public Model[] models; }
        [Serializable] public sealed class Entry
        {
            public string key, kind, name, asset_path;
            public float duration;
            public bool loop;
            public State[] states;
            public Parameter[] parameters;
            public Transition[] transitions;
        }
        [Serializable] public sealed class Model { public string name, cab, path_id, controller_key; }
        [Serializable] public sealed class State { public string name, clip_key; public uint hash; public float speed; public bool loop; }
        [Serializable] public sealed class Parameter { public string name; public int type; }
        [Serializable] public sealed class Condition { public string name; public int mode; public float threshold; }
        [Serializable] public sealed class Transition
        {
            public int source, destination;
            public float duration, exit_time;
            public bool fixed_duration, has_exit_time;
            public Condition[] conditions;
        }
        private AssetBundle bundle;
        private readonly Dictionary<string, Entry> entries;
        public Catalog Source { get; }
        public NativeMap13AnimatorLibrary(TextAsset binary, TextAsset catalog, string expectedSha256)
        {
            if (!binary || !catalog || string.IsNullOrEmpty(expectedSha256)) throw new InvalidOperationException("Original Map13 native animations are not bound.");
            byte[] bytes = binary.bytes;
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() != expectedSha256)
                    throw new InvalidOperationException("Original Map13 Windows animation payload changed.");
            Source = JsonUtility.FromJson<Catalog>(catalog.text);
            if (Source?.entries == null || Source.entries.Length != 147) throw new InvalidOperationException("Original Map13 native animation inventory differs.");
            entries = Source.entries.ToDictionary(e => e.key, StringComparer.Ordinal);
            bundle = AssetBundle.LoadFromMemory(bytes);
            if (!bundle) throw new InvalidOperationException("Original Map13 Windows animation bundle did not load.");
        }
        public RuntimeAnimatorController Controller(string key) => Load<RuntimeAnimatorController>(key, "AnimatorController");
        public AnimationClip Clip(string key) => Load<AnimationClip>(key, "AnimationClip");
        private T Load<T>(string key, string kind) where T : UnityEngine.Object
        {
            if (!bundle) throw new ObjectDisposedException(nameof(NativeMap13AnimatorLibrary));
            if (!entries.TryGetValue(key, out var entry) || entry.kind != kind) throw new InvalidOperationException("Original native animation identity is not bound: " + key);
            var value = bundle.LoadAsset<T>(entry.asset_path);
            if (!value || value.name != entry.name) throw new InvalidOperationException("Original native animation identity changed: " + key);
            return value;
        }
        public void Dispose() { if (bundle) bundle.Unload(true); bundle = null; }
    }
}
