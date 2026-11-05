using System;
using UnityEngine;
using Zjcs.Official.PartnersUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class PartnerNativeDetailPresentationAssets : ScriptableObject
    {
        [Serializable] public sealed class SpriteAddress { public string Path; public Sprite Sprite; }
        [Serializable] public sealed class IconAddress { public string Guid; public Sprite Sprite; }
        [Serializable] public sealed class LanguageEntry { public string Key, Text; }
        [SerializeField] private GameObject adventureTip;
        [SerializeField] private NpcAdventureSettings settings;
        [SerializeField] private TextAsset regions;
        [SerializeField] private SpriteAddress[] sprites;
        [SerializeField] private IconAddress[] icons;
        [SerializeField] private LanguageEntry[] texts;
        public GameObject AdventureTip => adventureTip;
        public NpcAdventureSettings Settings => settings;
        public TextAsset Regions => regions;
        public Sprite LoadSprite(string path)
        {
            foreach (var item in sprites) if (item.Path == path && item.Sprite) return item.Sprite;
            throw new InvalidOperationException("Packaged original partner detail sprite is unavailable: " + path);
        }
        public Sprite LoadIcon(string guid)
        {
            foreach (var item in icons) if (item.Guid == guid && item.Sprite) return item.Sprite;
            throw new InvalidOperationException("Packaged original adventure icon is unavailable: " + guid);
        }
        public string GetText(string key)
        {
            foreach (var item in texts) if (item.Key == key) return item.Text;
            throw new InvalidOperationException("Bind original partner adventure text: " + key);
        }
    }
}
