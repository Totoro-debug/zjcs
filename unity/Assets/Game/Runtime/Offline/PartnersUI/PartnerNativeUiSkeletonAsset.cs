using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class PartnerNativeUiSkeletonAsset : ScriptableObject
    {
        [Serializable] public sealed class AtlasPage
        {
            public string Name;
            public Texture Placeholder;
        }
        [SerializeField] private TextAsset payload;
        [SerializeField] private int npcId;
        [SerializeField] private string[] combineSkins;
        [SerializeField] private float defaultMix;
        [SerializeField] private AtlasPage[] pages = Array.Empty<AtlasPage>();
        [SerializeField] private string[] splitAnimationNames = Array.Empty<string>();
        private ProfessionNativeSkeletonData data;
        public ProfessionNativeSkeletonData Data => data ??= ProfessionNativeSkeletonData.LoadPartnerUi(
            payload ? payload.bytes : throw new InvalidOperationException("Original UI payload is unbound."), npcId);
        public string[] CombineSkins => combineSkins;
        public float DefaultMix => defaultMix;
        public Texture TextureForPage(string name)
        {
            foreach (var page in pages) if (page.Name == name && page.Placeholder) return page.Placeholder;
            throw new InvalidOperationException("Bind the original partner UI atlas page: " + name);
        }
        public bool ContainsSplitAnimation(string name) => Array.IndexOf(splitAnimationNames, name) >= 0;
    }
}
