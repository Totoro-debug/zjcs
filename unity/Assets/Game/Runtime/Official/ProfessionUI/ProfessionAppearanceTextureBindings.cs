using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // The original application proxy consumes these operations. No package
    // loader or skeleton SDK implementation crosses this boundary.
    public interface IProfessionUnsafeAssetHandle<T> where T : UnityEngine.Object
    {
        bool IsValid { get; }
        int Status { get; }
        T Result { get; }
        void WaitForAsyncComplete();
        void Release();
        event Action<IProfessionUnsafeAssetHandle<T>> Completed;
    }
    public interface IProfessionAppearanceTextureBindings
    {
        IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : UnityEngine.Object;
        Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex);
    }
    public static class ProfessionAppearanceTextureBindings
    {
        private static readonly Dictionary<string, List<Registration>> byGuid = new Dictionary<string, List<Registration>>(StringComparer.Ordinal);
        private static readonly Dictionary<ScriptableObject, List<Registration>> byAtlas = new Dictionary<ScriptableObject, List<Registration>>();
        private static readonly Router router = new Router();
        public static IProfessionAppearanceTextureBindings Current { get; set; }
        public static IProfessionAppearanceTextureBindings Required => byGuid.Count != 0 || byAtlas.Count != 0 ? router : Fallback;
        private static IProfessionAppearanceTextureBindings Fallback => Current ??
            throw new InvalidOperationException("Original appearance texture/atlas resources are unbound.");

        // Original application proxies keep their request and unload semantics.
        // Explicit packaged identities can coexist with the host's UI endpoint;
        // a battle owner never replaces that endpoint for another renderer.
        public static IDisposable Register(IProfessionAppearanceTextureBindings bindings,
            IEnumerable<string> guids, IEnumerable<ScriptableObject> atlases)
        {
            if (bindings == null || ReferenceEquals(bindings, router)) throw new ArgumentException(nameof(bindings));
            if (guids == null || atlases == null) throw new ArgumentNullException();
            var keys = guids.Distinct(StringComparer.Ordinal).ToArray();
            var assets = atlases.Distinct().ToArray();
            if (keys.Any(string.IsNullOrEmpty) || assets.Any(asset => !asset)) throw new ArgumentException("Packaged texture and atlas identities are required.");
            return new Registration(bindings, keys, assets);
        }

        private sealed class Router : IProfessionAppearanceTextureBindings
        {
            public IProfessionUnsafeAssetHandle<T> LoadAssetSyncByGuid<T>(string guid) where T : UnityEngine.Object
            {
                var owner = byGuid.TryGetValue(guid, out var owners) ? owners[owners.Count - 1].Bindings : Fallback;
                return owner.LoadAssetSyncByGuid<T>(guid);
            }
            public Material GetAtlasMaterial(ScriptableObject atlas, int materialIndex)
            {
                var owner = byAtlas.TryGetValue(atlas, out var owners) ? owners[owners.Count - 1].Bindings : Fallback;
                return owner.GetAtlasMaterial(atlas, materialIndex);
            }
        }

        private sealed class Registration : IDisposable
        {
            internal readonly IProfessionAppearanceTextureBindings Bindings;
            private readonly string[] guids;
            private readonly ScriptableObject[] atlases;
            private bool disposed;
            internal Registration(IProfessionAppearanceTextureBindings bindings, string[] guids, ScriptableObject[] atlases)
            {
                Bindings = bindings; this.guids = guids; this.atlases = atlases;
                foreach (var guid in guids)
                {
                    if (!byGuid.TryGetValue(guid, out var owners)) byGuid.Add(guid, owners = new List<Registration>());
                    owners.Add(this);
                }
                foreach (var atlas in atlases)
                {
                    if (!byAtlas.TryGetValue(atlas, out var owners)) byAtlas.Add(atlas, owners = new List<Registration>());
                    owners.Add(this);
                }
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                foreach (var guid in guids)
                {
                    var owners = byGuid[guid]; owners.Remove(this);
                    if (owners.Count == 0) byGuid.Remove(guid);
                }
                foreach (var atlas in atlases)
                {
                    var owners = byAtlas[atlas]; owners.Remove(this);
                    if (owners.Count == 0) byAtlas.Remove(atlas);
                }
            }
        }
    }
}
