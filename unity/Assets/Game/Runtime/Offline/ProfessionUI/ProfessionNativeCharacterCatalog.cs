using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class ProfessionNativeCharacterCatalog : ScriptableObject
    {
        [Serializable] private sealed class Entry
        {
            public string Path, Guid;
            public ScriptableObject Asset;
        }
        [SerializeField] private Entry[] entries;
        private Dictionary<string, Entry> byPath, byGuid;
        public int Count => entries?.Length ?? 0;
        private void EnsureIndex()
        {
            if (byPath != null) return;
            var paths = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var guids = new Dictionary<string, Entry>(StringComparer.Ordinal);
            if (entries == null) throw new InvalidOperationException("Original character resource catalog is unbound.");
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Path) || string.IsNullOrEmpty(entry.Guid) || !entry.Asset)
                    throw new InvalidOperationException("Original character resource binding is incomplete.");
                paths.Add(entry.Path, entry); guids.Add(entry.Guid, entry);
            }
            byPath = paths; byGuid = guids;
        }
        public ScriptableObject LoadPath(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Original character resource path is empty.", nameof(path));
            EnsureIndex();
            return byPath.TryGetValue(path, out var entry) ? entry.Asset :
                throw new InvalidOperationException("Original character resource has not been converted: " + path);
        }
        public ScriptableObject LoadGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid)) throw new ArgumentException("Original character resource GUID is empty.", nameof(guid));
            EnsureIndex();
            return byGuid.TryGetValue(guid, out var entry) ? entry.Asset :
                throw new InvalidOperationException("Original character resource GUID has not been converted: " + guid);
        }
    }
}
