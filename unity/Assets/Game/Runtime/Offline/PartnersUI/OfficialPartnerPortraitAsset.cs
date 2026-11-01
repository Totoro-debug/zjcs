using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // One resource per illustration retains the original live controller's lazy
    // lifetime. A roster must not preload every large portrait texture.
    public sealed class OfficialPartnerPortraitAsset : ScriptableObject
    {
        [Serializable] public sealed class Page
        {
            public string Name;
            public Texture2D Texture;
        }
        [SerializeField] private string manifest;
        [SerializeField] private int npcId;
        [SerializeField] private TextAsset data;
        [SerializeField] private TextAsset originalConfiguration;
        [SerializeField] private Vector3 liveOffset, liveScale;
        [SerializeField] private float defaultMix;
        [SerializeField] private Page[] pages;
        public int NpcId => npcId;
        public Vector3 LiveOffset => liveOffset;
        public Vector3 LiveScale => liveScale;
        public float DefaultMix => defaultMix;
        public TextAsset OriginalConfiguration => originalConfiguration;
        public ReadOnlySpan<Page> Pages => pages;
        public ProfessionNativeSkeletonData CreateSkeleton()
            => ProfessionNativeSkeletonData.LoadPartnerPortrait(data.bytes, npcId);
        public Texture2D TextureForPage(string name)
        {
            foreach (var page in pages) if (page.Name == name) return page.Texture;
            throw new InvalidOperationException("Original partner portrait page is unbound: " + name);
        }
        public static OfficialPartnerPortraitAsset Load(int id)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            var value = Resources.Load<OfficialPartnerPortraitAsset>("Partners/Portraits/Npc" + id);
            if (value == null || value.manifest != "884_156692" || value.npcId != id || value.data == null ||
                value.originalConfiguration == null || value.pages == null || value.pages.Length == 0)
                throw new InvalidOperationException("Original current partner portrait is unavailable: " + id);
            foreach (var page in value.pages)
                if (page == null || string.IsNullOrEmpty(page.Name) || page.Texture == null)
                    throw new InvalidOperationException("Original partner portrait texture is unbound: " + id);
            return value;
        }
    }
}
