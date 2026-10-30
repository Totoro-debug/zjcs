using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14EntityAssets
{
    [CreateAssetMenu(menuName = "Zjcs/Map14/Entity Prefab Catalog")]
    public sealed class NativeMap14EntityPrefabCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string OriginalPath;
            public int ClassId;
            public GameObject Prefab;
        }

        public string Manifest;
        public Entry[] Entries;

        private Dictionary<string, GameObject> lookup;

        public GameObject Load(string originalPath)
        {
            if (string.IsNullOrWhiteSpace(originalPath))
                throw new ArgumentException("An original entity prefab path is required.", nameof(originalPath));
            lookup ??= (Entries ?? Array.Empty<Entry>())
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.OriginalPath) && entry.Prefab)
                .ToDictionary(entry => entry.OriginalPath, entry => entry.Prefab, StringComparer.OrdinalIgnoreCase);
            if (!lookup.TryGetValue(originalPath, out var prefab))
                throw new InvalidOperationException("Original Map14 entity prefab is not packaged: " + originalPath);
            return prefab;
        }

        public GameObject LoadClass(int classId)
        {
            var entry = (Entries ?? Array.Empty<Entry>()).FirstOrDefault(value => value != null && value.ClassId == classId && value.Prefab);
            if (entry == null)
                throw new InvalidOperationException("Original Map14 entity class prefab is not packaged: " + classId);
            return entry.Prefab;
        }

        public void ResetLookup() => lookup = null;
    }
}
