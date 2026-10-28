using System;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14StatueEntities
{
    public sealed class NativeMap14StatueEntityCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public int ClassId;
            public GameObject Prefab;
        }
        public string Manifest;
        public Entry[] Entries;
    }
}
