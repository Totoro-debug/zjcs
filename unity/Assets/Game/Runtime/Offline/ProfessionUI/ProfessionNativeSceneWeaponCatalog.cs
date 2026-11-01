using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Lossless scene prefab inputs plus explicit reused native dependencies.
    // Source records are not yet production scene prefabs.
    public sealed class ProfessionNativeSceneWeaponCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string Path, Guid, InitialSkin, InitialAnimation;
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
            foreach (var entry in entries) if (entry.InitialSkin == skin) return entry;
            throw new NotSupportedException("Original scene weapon prefab is not converted: " + skin);
        }
    }
}
