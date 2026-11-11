using System;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // The original prefab selects the renderer, atlas and texture loader together.
    // Keep that binding as data so every native monster can use the same owner.
    public sealed class BattleNativeMonsterAppearanceCatalog : ScriptableObject
    {
        [Serializable] public sealed class Binding
        {
            public int EntityId;
            public string SkeletonName, RendererPath, MaterialPropertyPath;
            public bool AppearanceEnabled, MaterialPropertyEnabled;
            public ProfessionNativeWeaponTextureResources Textures;
            public Material[] Materials;
        }
        [SerializeField] private Binding[] bindings;

        public static Binding Find(int entityId)
        {
            var catalog = Resources.Load<BattleNativeMonsterAppearanceCatalog>("Combat/NativeMonsterAppearance/catalog");
            return catalog ? catalog.bindings.SingleOrDefault(value => value.EntityId == entityId) : null;
        }
    }
}
