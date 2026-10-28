using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class OfflineProfessionImageCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Path;
            public string AssetGuid;
            public UnityEngine.Object Asset;
        }
        public Entry[] Entries;
    }
}
