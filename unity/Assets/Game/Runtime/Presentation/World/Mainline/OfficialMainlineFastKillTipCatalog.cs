using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed class OfficialMainlineFastKillTipCatalog : ScriptableObject
    {
        public string Manifest;
        public GameObject Prefab;
        public GameObject FastKillPrefab;
        public Slot[] Slots;
        [System.Serializable] public sealed class Slot
        {
            public string ModelName;
            public Node[] Transforms;
        }
        [System.Serializable] public sealed class Node
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }
    }
}
