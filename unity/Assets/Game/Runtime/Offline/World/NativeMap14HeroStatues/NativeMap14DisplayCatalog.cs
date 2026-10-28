using System;
using System.IO;
using UnityEngine;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.MapDisplays;

namespace Zjcs.Offline.World.NativeMap14HeroStatues
{
    public sealed class NativeMap14DisplayCatalog : ScriptableObject, IMapDisplayResourceLoader
    {
        [Serializable] public sealed class Display { public string path; public GameObject prefab; }
        public string manifest;
        public EntityMapDisplayInfo[] selectors;
        public Display[] displays;
        [NonSerialized] private EntityMapDisplayInfoParser selector;
        public EntityMapDisplayInfoParser Selector
        {
            get
            {
                if (manifest != "871_156598") throw new InvalidDataException("Original Map14 display version differs.");
                if (selector == null) { selector = new EntityMapDisplayInfoParser(); selector.Load(selectors); }
                return selector;
            }
        }
        public MapDisplayAssetLease LoadPrefab(string path)
        {
            foreach (var display in displays)
                if (display.path == path)
                {
                    if (!display.prefab) throw new InvalidDataException("Original display prefab is not bound: " + path);
                    return new Lease(display.prefab);
                }
            throw new InvalidDataException("Original map display has not been migrated: " + path);
        }
        private sealed class Lease : MapDisplayAssetLease
        {
            private GameObject prefab;
            public Lease(GameObject value) { prefab = value; }
            public override bool IsValid => prefab;
            public override int Status => prefab ? 2 : 0;
            public override GameObject Result => prefab;
            public override void Dispose() { prefab = null; }
        }
    }
}
