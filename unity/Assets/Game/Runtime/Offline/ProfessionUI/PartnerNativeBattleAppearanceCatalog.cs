using System;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class PartnerNativeBattleAppearanceCatalog : ScriptableObject
    {
        [Serializable] public sealed class RendererBinding
        {
            public string Path, SkeletonDataName;
            public bool Enabled;
            public LevelOfResolution LevelOfResolution;
            public ProfessionNativeWeaponTextureResources Textures;
            public Material[] Materials;
        }

        [Serializable] public sealed class Binding
        {
            public int NpcId, EntityId;
            public string PropertyPath, AppearanceTargetPath;
            public bool PropertyEnabled;
            public RendererBinding[] Renderers;
        }

        [SerializeField] private Binding[] bindings;

        public static Binding Find(int npcId)
        {
            var catalog = Resources.Load<PartnerNativeBattleAppearanceCatalog>("Combat/NativePartnerAppearance/catalog");
            return catalog ? catalog.bindings.SingleOrDefault(value => value.NpcId == npcId) : null;
        }
    }
}
