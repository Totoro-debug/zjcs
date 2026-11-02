using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Current = Zjcs.Official.CombatAppearance;

namespace Zjcs.Offline.ProfessionUI
{
    // The original clipping application captures these actor-owned ports in
    // Awake. Current UI appearance shares the current scene application classes.
    public sealed class OfflineProfessionCurrentUiSkinClipBindings : IProfessionAppearanceSkinClipBindings
    {
        private readonly IProfessionAppearanceSkinClipBindings fallback;
        public OfflineProfessionCurrentUiSkinClipBindings(IProfessionAppearanceSkinClipBindings fallback = null)
        { this.fallback = fallback; }
        public Component FindCharacter(Component owner) => owner.GetComponent<Current.AppearanceCharacter>()
            ?? fallback?.FindCharacter(owner);
        public Component FindSpine(Component owner) => owner.GetComponent<Current.AppearanceUI>()
            ?? fallback?.FindSpine(owner);
        public IProfessionSkinClipCharacter GetCharacter(Component character)
            => character is Current.AppearanceCharacter value ? new Character(value)
                : fallback?.GetCharacter(character) ?? throw new ArgumentException("Current original UI character is unbound.", nameof(character));
        public IProfessionSkinClipAppearance GetAppearance(Component spine)
        {
            if (!(spine is Current.AppearanceUI value))
                return fallback?.GetAppearance(spine) ?? throw new ArgumentException("Current original UI appearance is unbound.", nameof(spine));
            var graphic = value.GetComponent<ProfessionNativeUiGraphic>();
            if (!graphic) throw new InvalidOperationException("Bind the current UI appearance's actual native Canvas endpoint.");
            return new Appearance(value, graphic);
        }
        private sealed class Character : IProfessionSkinClipCharacter
        {
            private readonly Current.AppearanceCharacter value;
            internal Character(Current.AppearanceCharacter value) { this.value = value; }
            public bool TryGetSkinName(SkinType type, out string name) => value.TryGetSkinName(type, out name);
        }
        private sealed class Appearance : IProfessionSkinClipAppearance
        {
            private readonly Current.AppearanceUI value;
            private readonly ProfessionNativeUiGraphic graphic;
            internal Appearance(Current.AppearanceUI value, ProfessionNativeUiGraphic graphic)
            { this.value = value; this.graphic = graphic; }
            public Action OnSkinUpdate { get => value.OnSkinUpdate; set => this.value.OnSkinUpdate = value; }
            public IProfessionSkinClipSkeleton Skeleton => graphic.State;
            public IProfessionSkinClipGraphic Graphic => graphic;
            public bool TryGetMaterial(string skin, out Material material) => value.TryGetMaterial(skin, out material);
        }
    }
}
