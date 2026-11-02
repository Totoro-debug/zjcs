using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Bind the original application components to their actual native UI graphic.
    // A deferred avatar has no skeleton until the original player branch selects it.
    public sealed class OfflineProfessionNativeSkinClipBindings : IProfessionAppearanceSkinClipBindings, IDisposable
    {
        private sealed class Character : IProfessionSkinClipCharacter
        {
            private readonly AppearanceCharacter value;
            internal Character(AppearanceCharacter value) { this.value = value; }
            public bool TryGetSkinName(SkinType type, out string name) => value.TryGetSkinName(type, out name);
        }
        private sealed class Appearance : IProfessionSkinClipAppearance
        {
            private readonly AppearanceUI value;
            private readonly ProfessionNativeUiGraphic graphic;
            internal Appearance(AppearanceUI value, ProfessionNativeUiGraphic graphic)
            { this.value = value; this.graphic = graphic; }
            public Action OnSkinUpdate { get => value.OnSkinUpdate; set => this.value.OnSkinUpdate = value; }
            public IProfessionSkinClipSkeleton Skeleton => graphic.State;
            public IProfessionSkinClipGraphic Graphic => graphic;
            public bool TryGetMaterial(string name, out Material material) => value.TryGetMaterial(name, out material);
        }
        private readonly Dictionary<AppearanceCharacter, Character> characters = new Dictionary<AppearanceCharacter, Character>();
        private readonly Dictionary<AppearanceUI, Appearance> appearances = new Dictionary<AppearanceUI, Appearance>();
        private readonly IProfessionAppearanceSkinClipBindings fallback;
        public OfflineProfessionNativeSkinClipBindings(IProfessionAppearanceSkinClipBindings fallback = null) { this.fallback = fallback; }
        public Component FindCharacter(Component owner) => owner.GetComponent<AppearanceCharacter>();
        public Component FindSpine(Component owner) => owner.GetComponent<AppearanceUI>();
        public IProfessionSkinClipCharacter GetCharacter(Component character)
        {
            if (character && !character.GetComponent<ProfessionNativeUiGraphic>() && fallback != null)
                return fallback.GetCharacter(character);
            if (!(character is AppearanceCharacter value)) throw new ArgumentException("Original UI appearance character is unbound.", nameof(character));
            if (!characters.TryGetValue(value, out var binding)) characters.Add(value, binding = new Character(value));
            return binding;
        }
        public IProfessionSkinClipAppearance GetAppearance(Component spine)
        {
            if (spine && !spine.GetComponent<ProfessionNativeUiGraphic>() && fallback != null)
                return fallback.GetAppearance(spine);
            if (!(spine is AppearanceUI value)) throw new ArgumentException("Original native UI appearance is unbound.", nameof(spine));
            if (!appearances.TryGetValue(value, out var binding))
            {
                var graphic = value.GetComponent<ProfessionNativeUiGraphic>();
                if (!graphic) throw new InvalidOperationException("Original UI appearance has no native graphic on its GameObject.");
                appearances.Add(value, binding = new Appearance(value, graphic));
            }
            return binding;
        }
        public void Dispose() { characters.Clear(); appearances.Clear(); }
    }
}
