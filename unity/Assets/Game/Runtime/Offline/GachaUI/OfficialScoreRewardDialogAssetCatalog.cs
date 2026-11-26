using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Zjcs.Offline.GachaUI
{
    [Serializable]
    public sealed class ScoreRewardDialogImage
    {
        public string path, assetGuid;
        public Sprite sprite;
    }
    public sealed class OfficialScoreRewardDialogAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private TextAsset texts;
        [SerializeField] private ScoreRewardDialogImage[] rewardImages;
        private Dictionary<string, string> localized;
        public GameObject Prefab => prefab;
        public IReadOnlyList<ScoreRewardDialogImage> RewardImages => rewardImages;
        public string Text(string key)
        {
            if (key == "{0}" || key == "{0}/{1}") return key;
            if (localized == null)
            {
                localized = new Dictionary<string, string>();
                foreach (var row in JObject.Parse(texts.text)["texts"])
                    localized.Add((string)row["key"], (string)row["text"]);
            }
            return localized.TryGetValue(key, out var value) ? value
                : throw new InvalidOperationException("Original score detail localization is unbound: " + key);
        }
        public static OfficialScoreRewardDialogAssetCatalog LoadDefault()
        {
            var asset = Resources.Load<OfficialScoreRewardDialogAssetCatalog>("PetGacha/OfficialScoreRewardDialogAssetCatalog");
            if (!asset || !asset.prefab || !asset.texts) throw new InvalidOperationException("Original score reward dialog assets are missing.");
            return asset;
        }
    }
}
