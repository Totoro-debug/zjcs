using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerProfileAssetCatalog : ScriptableObject
    {
        [Serializable] private sealed class TextRow { public string key, text; }
        [Serializable] private sealed class TextRows { public TextRow[] texts; }
        [SerializeField] private GameObject profilePrefab, rewardIconPrefab;
        [SerializeField] private TextAsset texts;
        [SerializeField] private OfflineProfessionImageCatalog images;
        [SerializeField] private OfficialPartnerGiftPageAssetCatalog common;
        private Dictionary<string, string> words;
        public GameObject ProfilePrefab => profilePrefab;
        public GameObject RewardIconPrefab => rewardIconPrefab;
        public OfflineProfessionImageCatalog Images => images;
        public OfficialPartnerGiftPageAssetCatalog Common => common;
        public IReadOnlyDictionary<string, string> Texts
        {
            get
            {
                if (words == null)
                {
                    words = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var row in JsonUtility.FromJson<TextRows>(texts.text).texts) words.Add(row.key, row.text);
                }
                return words;
            }
        }
        public static OfficialPartnerProfileAssetCatalog Load()
        {
            var value = Resources.Load<OfficialPartnerProfileAssetCatalog>("Partners/OfficialPartnerProfileAssetCatalog");
            if (!value) throw new InvalidOperationException("Original partner profile catalog is unbound.");
            if (!value.profilePrefab || !value.rewardIconPrefab || !value.texts || !value.images || !value.common)
                throw new InvalidOperationException("Original partner profile assets are unbound: profile=" + (bool)value.profilePrefab
                    + ", reward=" + (bool)value.rewardIconPrefab + ", texts=" + (bool)value.texts
                    + ", images=" + (bool)value.images + ", common=" + (bool)value.common);
            return value;
        }
    }
}
