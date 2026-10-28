using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13EntityViews
{
    public sealed class NativeMap13TreeModelAnimatorLibrary : IDisposable
    {
        [Serializable] private sealed class Catalog { public string manifest, sha256; public Entry[] entries; }
        [Serializable] private sealed class Entry { public string key, type, name, assetName; }
        private AssetBundle bundle;
        private readonly Dictionary<string, RuntimeAnimatorController> controllers = new Dictionary<string, RuntimeAnimatorController>();
        public IReadOnlyDictionary<string, RuntimeAnimatorController> Controllers => controllers;
        public AnimationClip Clip { get; }
        public NativeMap13TreeModelAnimatorLibrary(TextAsset binary, TextAsset catalog)
        {
            if (!binary || !catalog) throw new InvalidDataException("Original tree Model clock is not retained.");
            var source = JsonUtility.FromJson<Catalog>(catalog.text);
            if (source?.manifest != "871_156598" || source.entries.Length != 2) throw new InvalidDataException("Original tree Model clock inventory differs.");
            byte[] bytes = binary.bytes;
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() != source.sha256)
                    throw new InvalidDataException("Original tree Model clock bytes differ.");
            bundle = AssetBundle.LoadFromMemory(bytes);
            if (!bundle) throw new InvalidDataException("Windows native Model clock failed to load.");
            var entry = source.entries.Single(e => e.type == "AnimatorController");
            var controller = bundle.LoadAsset<RuntimeAnimatorController>(entry.assetName);
            if (!controller || controller.name != entry.name) throw new InvalidDataException("Original Model controller failed to load.");
            controllers.Add(entry.key, controller);
            entry = source.entries.Single(e => e.type == "AnimationClip"); Clip = bundle.LoadAsset<AnimationClip>(entry.assetName);
            if (!Clip || Clip.name != entry.name) throw new InvalidDataException("Original Model dissolve clip failed to load.");
        }
        public void Dispose() { controllers.Clear(); if (bundle) bundle.Unload(true); bundle = null; }
    }
}
