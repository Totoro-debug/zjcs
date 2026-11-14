using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.HomeLevelUpUI
{
    public sealed class OfflineHomeLevelUpAssets : ScriptableObject
    {
        public const uint SoundEventId = 2648322711;
        public GameObject prefab;
        public TextAsset texts;
        public OfflineProfessionImageCatalog images;
        public AudioClip sound;

        public static OfflineHomeLevelUpAssets LoadDefault()
        {
            // Load the catalog root before its dependency-only ScriptableObject.
            var catalog = Resources.Load<OfflineProfessionImageCatalog>("Home/HomeLevelUpImages");
            var result = Resources.Load<OfflineHomeLevelUpAssets>("Home/HomeLevelUpAssets");
            if (!result || !result.prefab || !result.texts || !result.sound || !catalog || result.images != catalog)
                throw new InvalidDataException("The selected original level-up notification is incompletely bound.");
            return result;
        }

        public FontAssetFontService CreateFonts(IProfessionImageResourceLoader resources)
            => new FontAssetFontService(resources, json => JsonConvert.DeserializeObject<FontAssetFontConfig>(json));
        public OfflinePartnerLocalizationService CreateLanguage() => new OfflinePartnerLocalizationService(texts);
    }
}
