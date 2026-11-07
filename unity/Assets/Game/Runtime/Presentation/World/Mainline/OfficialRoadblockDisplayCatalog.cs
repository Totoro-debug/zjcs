using System;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed class OfficialRoadblockDisplayCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry { public int classId; public GameObject prefab, dialogIcon; }
        [Serializable] public sealed class Background { public string areaTag; public Sprite sprite; }
        [Serializable] public sealed class Area { public int area; public string areaTag; }
        public string manifest;
        public Entry[] entries;
        public AudioClip activationSound;
        public Background[] backgrounds;
        public Area[] areas;
    }
}
