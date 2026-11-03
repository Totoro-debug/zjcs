using System;
using UnityEngine;

namespace Zjcs.Offline.World
{
    public sealed class MainlineEntityPrefabVisualFactory : IDisposable
    {
        private readonly IMainlineEntityPrefabProvider provider;

        public MainlineEntityPrefabVisualFactory(IMainlineEntityPrefabProvider provider, int expectedMapClassId)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            if (expectedMapClassId <= 0 || provider.MapClassId != expectedMapClassId)
                throw new ArgumentException("Entity prefab provider belongs to a different map.", nameof(provider));
        }

        public GameObject Create(int classId, Transform parent)
        {
            if (classId <= 0) throw new ArgumentOutOfRangeException(nameof(classId));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            GameObject instance = UnityEngine.Object.Instantiate(provider.LoadClass(classId), parent, false);
            instance.name = "Map" + provider.MapClassId + " original entity " + classId;
            return instance;
        }

        public void Dispose() { }
    }
}
