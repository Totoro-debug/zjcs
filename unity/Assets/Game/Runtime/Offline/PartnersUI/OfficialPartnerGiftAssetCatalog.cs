using System;
using UnityEngine;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerGiftAssetCatalog : ScriptableObject
    {
        [SerializeField] private OfficialPartnerAssetCatalog configuration;
        [SerializeField] private TextAsset customCharacters;
        private OfflinePartnerGiftConfiguration loaded;
        public OfflinePartnerGiftConfiguration Load()
        {
            if (configuration == null || customCharacters == null)
                throw new InvalidOperationException("Official partner gift configuration/character bindings are missing.");
            return loaded ?? (loaded = new OfflinePartnerGiftConfiguration(configuration.Load(), customCharacters.text));
        }
        public static OfflinePartnerGiftConfiguration LoadDefault()
        {
            var binding = Resources.Load<OfficialPartnerGiftAssetCatalog>("Partners/OfficialPartnerGiftAssetCatalog");
            if (binding == null) throw new InvalidOperationException("Official partner gift asset catalog is unbound.");
            return binding.Load();
        }
    }
}
