using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class OfficialPartnerAchievementAssetCatalog : ScriptableObject
    {
        [Serializable] private sealed class TextRow { public string key, text; }
        [Serializable] private sealed class TextRows { public TextRow[] texts; }
        [SerializeField] private GameObject panelPrefab, rewardIconPrefab;
        [SerializeField] private TextAsset texts;
        [SerializeField] private OfflineProfessionImageCatalog images;
        [SerializeField] private OfficialPartnerGiftPageAssetCatalog common;
        private Dictionary<string, string> words;
        public GameObject PanelPrefab => panelPrefab;
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
        public static OfficialPartnerAchievementAssetCatalog Load()
        {
            var value = Resources.Load<OfficialPartnerAchievementAssetCatalog>("Partners/OfficialPartnerAchievementAssetCatalog");
            if (!value || !value.panelPrefab || !value.rewardIconPrefab || !value.texts || !value.images || !value.common)
                throw new InvalidOperationException("Original partner achievement resources are unbound.");
            return value;
        }
    }
}
