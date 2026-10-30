using System;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14EntityAssets
{
    public sealed class NativeMap14EntityPrefabVisualFactory : IDisposable
    {
        private readonly NativeMap14EntityPrefabCatalog catalog;

        public NativeMap14EntityPrefabVisualFactory(NativeMap14EntityPrefabCatalog catalog)
        { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }

        public bool TryCreate(int classId, Transform parent, out GameObject instance)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            instance = UnityEngine.Object.Instantiate(catalog.LoadClass(classId), parent, false);
            instance.name = "Map14 original entity " + classId;
            return true;
        }

        public void Dispose() { }
    }
}
