using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Zjcs.Offline.World
{
    // Original compiled animation payloads in a verified Unity Windows carrier.
    // Keep this owner alive until the UI objects using its controllers are gone.
    public sealed class NativeMap12InviteAnimations : IDisposable
    {
        [Serializable] private sealed class Catalog { public string manifest, bundleSha256; public Entry[] assets; }
        [Serializable] private sealed class Entry { public string name, type; }
        private AssetBundle bundle;
        private readonly Dictionary<string, AnimationClip> clips;
        private readonly Dictionary<string, RuntimeAnimatorController> controllers;

        public NativeMap12InviteAnimations()
        {
            const string resource = "Map12/TeamAnimations/";
            var metadata = Resources.Load<TextAsset>(resource + "catalog");
            var payload = Resources.Load<TextAsset>(resource + "native-windows");
            if (!metadata || !payload) throw new InvalidOperationException("Original Windows invitation animations are unbound.");
            var catalog = JsonUtility.FromJson<Catalog>(metadata.text);
            byte[] bytes = payload.bytes;
            string digest;
            using (var hash = SHA256.Create()) digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            if (catalog == null || catalog.manifest != "871_156598" || catalog.bundleSha256 != digest || catalog.assets?.Length != 18)
                throw new InvalidOperationException("Original Windows invitation animation source differs.");
            bundle = AssetBundle.LoadFromMemory(bytes);
            if (!bundle) throw new InvalidOperationException("Original Windows invitation animations failed to load.");
            try
            {
                clips = bundle.LoadAllAssets<AnimationClip>().ToDictionary(asset => asset.name, StringComparer.Ordinal);
                controllers = bundle.LoadAllAssets<RuntimeAnimatorController>().ToDictionary(asset => asset.name, StringComparer.Ordinal);
                foreach (var entry in catalog.assets)
                {
                    bool valid = entry.type == "AnimationClip" ? clips.ContainsKey(entry.name) :
                        controllers.TryGetValue(entry.name, out var controller) &&
                        (entry.type == "AnimatorOverrideController") == (controller is AnimatorOverrideController);
                    if (!valid) throw new InvalidOperationException("Original Windows animation object is unbound: " + entry.name);
                }
                if (clips.Count != 14 || controllers.Count != 4)
                    throw new InvalidOperationException("Original Windows animation closure differs.");
            }
            catch { bundle.Unload(true); bundle = null; throw; }
        }

        public AnimationClip Clip(string originalName)
        {
            if (!bundle) throw new ObjectDisposedException(nameof(NativeMap12InviteAnimations));
            return clips[originalName];
        }

        public void Bind(GameObject originalPrefab)
        {
            if (!bundle) throw new ObjectDisposedException(nameof(NativeMap12InviteAnimations));
            foreach (var animator in originalPrefab.GetComponentsInChildren<Animator>(true))
            {
                var source = animator.runtimeAnimatorController;
                if (!source) continue;
                if (!controllers.TryGetValue(source.name, out var native) ||
                    !source.animationClips.Select(clip => clip.name).OrderBy(name => name)
                        .SequenceEqual(native.animationClips.Select(clip => clip.name).OrderBy(name => name)))
                    throw new InvalidOperationException("Original invitation Animator dependency differs: " + source.name);
                animator.runtimeAnimatorController = native;
            }
        }

        public void Dispose()
        {
            if (!bundle) return;
            bundle.Unload(true);
            bundle = null;
        }
    }
}
