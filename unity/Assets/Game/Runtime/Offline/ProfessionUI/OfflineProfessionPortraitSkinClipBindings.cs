using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using AppearanceCharacter = Zjcs.Official.CombatAppearance.AppearanceCharacter;

namespace Zjcs.Offline.ProfessionUI
{
    // Awake captures actor-owned ports, so the shared static resolver is only
    // installed while the inactive original studio is being activated.
    public sealed class OfflineProfessionPortraitSkinClipBindings : IProfessionAppearanceSkinClipBindings
    {
        private sealed class Character : IProfessionSkinClipCharacter
        {
            private readonly AppearanceCharacter value;
            internal Character(AppearanceCharacter value) { this.value = value; }
            public bool TryGetSkinName(SkinType type, out string name) => value.TryGetSkinName(type, out name);
        }
        private sealed class Appearance : IProfessionSkinClipAppearance
        {
            private readonly ProfessionNativePortraitAppearance value;
            internal Appearance(ProfessionNativePortraitAppearance value) { this.value = value; }
            public Action OnSkinUpdate { get => value.OnSkinUpdate; set => this.value.OnSkinUpdate = value; }
            public IProfessionSkinClipSkeleton Skeleton => value.GetComponent<ProfessionNativeSceneRenderer>().State;
            public IProfessionSkinClipGraphic Graphic => null; // Original scene MeshRenderer branch.
            public bool TryGetMaterial(string skin, out Material material) => value.TryGetMaterial(skin, out material);
        }
        public Component FindCharacter(Component owner) => owner.GetComponent<AppearanceCharacter>();
        public Component FindSpine(Component owner) => owner.GetComponent<ProfessionNativePortraitAppearance>();
        public IProfessionSkinClipCharacter GetCharacter(Component character) => character is AppearanceCharacter value
            ? new Character(value) : throw new ArgumentException("Original portrait character is unbound.");
        public IProfessionSkinClipAppearance GetAppearance(Component appearance) => appearance is ProfessionNativePortraitAppearance value
            ? new Appearance(value) : throw new ArgumentException("Original portrait renderer is unbound.");
    }
}
