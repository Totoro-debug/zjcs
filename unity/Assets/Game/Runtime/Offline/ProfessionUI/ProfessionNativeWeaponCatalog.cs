using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Explicit source prefab metadata/model/page bindings. Prefab metadata is
    // retained for conversion; it is not instantiated as a substitute UI.
    public sealed class ProfessionNativeWeaponCatalog : ScriptableObject
    {
        [Serializable] public sealed class Page
        {
            public string Name, MainGuid, DyeGuid;
            public Texture2D Placeholder, Main, Dye;
            public Material Material;
        }
        [Serializable] public sealed class PrefabSource
        {
            public string Path, Guid;
            public TextAsset Hierarchy;
        }
        [SerializeField] private ProfessionNativeSkeletonAsset model;
        [SerializeField] private Page[] pages;
        [SerializeField] private PrefabSource[] prefabs;
        public ProfessionNativeSkeletonAsset Model => model ? model : throw new InvalidOperationException("Original weapon model is unbound.");
        public Page FindPage(string name)
        {
            foreach (var page in pages) if (page.Name == name) return page;
            throw new NotSupportedException("Original weapon page is not converted: " + name);
        }
        public PrefabSource FindPrefabSource(string path)
        {
            foreach (var prefab in prefabs) if (prefab.Path == path) return prefab;
            throw new NotSupportedException("Original weapon prefab source is not converted: " + path);
        }
    }
}
