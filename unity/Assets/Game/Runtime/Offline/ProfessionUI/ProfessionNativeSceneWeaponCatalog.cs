using System;
using Zjcs.Official.ProfessionUI;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Lossless scene prefab inputs plus explicit reused native dependencies.
    // Legacy diagnostic sources and complete native prefab entries coexist.
    public sealed class ProfessionNativeSceneWeaponCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string Path, Guid, InitialSkin, InitialAnimation;
            public string Skin;
            public GameObject Prefab;
            public TextAsset Hierarchy;
            public Material OriginalMaterial;
        }
        [SerializeField] private ProfessionNativeWeaponCatalog weapon;
        [SerializeField] private ProfessionNativeWeaponTextureResources textures;
        [SerializeField] private Entry[] entries;
        public ProfessionNativeWeaponCatalog Weapon => weapon ? weapon : throw new InvalidOperationException("Original scene weapon model is unbound.");
        public ProfessionNativeWeaponTextureResources Textures => textures ? textures : throw new InvalidOperationException("Original scene weapon texture resources are unbound.");
        public Entry Find(string skin)
        {
            foreach (var entry in entries)
                if ((string.IsNullOrEmpty(entry.Skin) ? entry.InitialSkin : entry.Skin) == skin) return entry;
            throw new NotSupportedException("Original scene weapon prefab is not converted: " + skin);
        }
        public Entry FindPath(string path)
        {
            foreach (var entry in entries)
                if (ProfessionAppearancePartPaths.GetPlayerPartPrefabRenderer(PlayerPartMode.Integrated,
                    string.IsNullOrEmpty(entry.Skin) ? entry.InitialSkin : entry.Skin) == path)
                    return entry;
            throw new NotSupportedException("Original independent scene part path is not converted: " + path);
        }
    }
}
