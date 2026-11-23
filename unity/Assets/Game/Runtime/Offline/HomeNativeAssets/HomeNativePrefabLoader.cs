using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeFurniture;

namespace Zjcs.Offline.HomeNativeAssets
{
    // Scene-owned local resource boundary for the unchanged original callers.
    // Instances and their dependencies keep their normal Unity ownership.
    public sealed class HomeNativePrefabLoader : IHomeEntityResourceLoader, IDisposable
    {
        internal sealed class Entry
        {
            public string Location;
            public HomeNativePrefabReference Reference;
            public int Count;
        }
        public sealed class Handle : IHomeEntityPrefabHandle, IHomeFurniturePrefabHandle
        {
            internal HomeNativePrefabLoader owner;
            internal Entry entry;
            internal Handle(HomeNativePrefabLoader owner) { this.owner = owner; }
            public GameObject Result => entry?.Reference ? entry.Reference.Prefab : null;
            public void Release() => Dispose();
            public void Dispose() => owner?.Release(this);
        }
        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        readonly HashSet<Handle> handles = new HashSet<Handle>();
        bool disposed;

        public static string ResourcePath(string originalLocation)
        {
            if (originalLocation == null) throw new ArgumentNullException(nameof(originalLocation));
            using (var hash = SHA256.Create()) {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(originalLocation));
                var token = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes) token.Append(value.ToString("x2"));
                return "Home/NativePrefabs/" + token;
            }
        }
        public Handle LoadPrefab(string originalLocation)
        {
            if (disposed) throw new ObjectDisposedException(nameof(HomeNativePrefabLoader));
            Entry entry = null;
            if (!string.IsNullOrEmpty(originalLocation) && !entries.TryGetValue(originalLocation, out entry)) {
                var reference = Resources.Load<HomeNativePrefabReference>(ResourcePath(originalLocation));
                if (reference) {
                    if (!string.Equals(reference.OriginalLocation, originalLocation, StringComparison.Ordinal) || !reference.Prefab)
                        throw new InvalidOperationException("Local native home prefab reference differs: " + originalLocation);
                    entry = new Entry { Location = originalLocation, Reference = reference };
                    entries.Add(originalLocation, entry);
                }
            }
            var handle = new Handle(this) { entry = entry };
            if (entry != null) entry.Count++;
            handles.Add(handle);
            // Missing addresses retain the original creator's null-result path.
            return handle;
        }
        IHomeEntityPrefabHandle IHomeEntityResourceLoader.LoadPrefab(string path) => LoadPrefab(path);
        void Release(Handle handle)
        {
            if (!handles.Remove(handle)) return;
            Entry entry = handle.entry;
            handle.owner = null; handle.entry = null;
            if (entry != null && --entry.Count == 0) {
                entries.Remove(entry.Location);
                entry.Reference = null;
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var handle in new List<Handle>(handles)) Release(handle);
            entries.Clear();
        }
    }
}
