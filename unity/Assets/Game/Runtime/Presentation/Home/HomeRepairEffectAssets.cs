using System;
using UnityEngine;

namespace Zjcs.Presentation.Home
{
    // Original prefab references and the serialized FurnitureBase timing values.
    public sealed class HomeRepairEffectAssets : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public int Type;
            public GameObject Prefab;
            public Vector3 RootOffset;
            public float ShowTime;
            public float FullTime;
        }

        public Entry[] Entries;

        public Entry Find(int type)
        {
            foreach (Entry entry in Entries ?? Array.Empty<Entry>())
                if (entry != null && entry.Type == type) return entry;
            return null;
        }
    }
}
