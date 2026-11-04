using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using SceneCharacter = Zjcs.Official.CombatAppearance.AppearanceCharacter;

namespace Zjcs.Offline.ProfessionUI
{
    // Install only around activation. Original Awake captures these actor-owned ports.
    public sealed class OfflineProfessionSceneSkinClipBindings : IProfessionAppearanceSkinClipBindings
    {
        private sealed class Character : IProfessionSkinClipCharacter
        {
            private readonly SceneCharacter value;
            internal Character(SceneCharacter value) { this.value = value; }
            public bool TryGetSkinName(SkinType type, out string name) => value.TryGetSkinName(type, out name);
        }
        private sealed class Appearance : IProfessionSkinClipAppearance
        {
            private readonly ProfessionNativeSceneAppearance value;
            internal Appearance(ProfessionNativeSceneAppearance value) { this.value = value; }
            public Action OnSkinUpdate { get => value.OnSkinUpdate; set => this.value.OnSkinUpdate = value; }
            public IProfessionSkinClipSkeleton Skeleton => value.GetComponent<ProfessionNativeSceneRenderer>().State;
            public IProfessionSkinClipGraphic Graphic => null;
            public bool TryGetMaterial(string skin, out Material material) => value.TryGetMaterial(skin, out material);
        }
        public Component FindCharacter(Component owner) => owner.GetComponent<SceneCharacter>();
        public Component FindSpine(Component owner) => owner.GetComponent<ProfessionNativeSceneAppearance>();
        public IProfessionSkinClipCharacter GetCharacter(Component character) => character is SceneCharacter value
            ? new Character(value) : throw new ArgumentException("Original scene character is unbound.");
        public IProfessionSkinClipAppearance GetAppearance(Component appearance) => appearance is ProfessionNativeSceneAppearance value
            ? new Appearance(value) : throw new ArgumentException("Original scene renderer is unbound.");
    }
}
