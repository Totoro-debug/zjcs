using System;
using UnityEngine;

namespace Zjcs.Official.StelePlayables
{
    public sealed class OfficialStelePlayableCatalog : ScriptableObject
    {
        public const string ResourcePath = "Map12/Native/stele-playables-catalog";

        [Serializable]
        public sealed class Entry
        {
            public int entityClassId;
            public GameObject mCanInteractionPlayable;
            public GameObject mEndInteractionPlayable;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        public Entry[] Entries => (Entry[])entries.Clone();

        public Entry Find(int entityClassId)
        {
            foreach (var entry in entries)
                if (entry != null && entry.entityClassId == entityClassId) return entry;
            throw new InvalidOperationException("Original stele performance is unbound: " + entityClassId);
        }

        public static OfficialStelePlayableCatalog Load()
        {
            var catalog = Resources.Load<OfficialStelePlayableCatalog>(ResourcePath);
            if (catalog == null) throw new InvalidOperationException("Original stele performance catalog is missing.");
            if (catalog.entries.Length != 2 || catalog.entries[0]?.entityClassId != 6650
                || catalog.entries[1]?.entityClassId != 6655)
                throw new InvalidOperationException("Original stele performance coverage differs.");
            foreach (var entry in catalog.entries)
                if (entry.mCanInteractionPlayable == null || entry.mEndInteractionPlayable == null)
                    throw new InvalidOperationException("Original stele performance prefab is missing.");
            return catalog;
        }
    }
}
