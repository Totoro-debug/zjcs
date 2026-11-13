using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeFastHangUpUI
{
    public sealed class OfficialHomeFastHangUpAssetCatalog : ScriptableObject
    {
        [Serializable] public sealed class Image { public string path, assetGuid; public Sprite sprite; }
        public GameObject selectionPrefab, itemIconPrefab, hintPrefab;
        public ItemIconConfig iconConfig;
        public ItemIconAttachmentConfig attachmentConfig;
        public TextAsset texts, rules;
        public Image[] images;
        public static OfficialHomeFastHangUpAssetCatalog LoadDefault()
            => Resources.Load<OfficialHomeFastHangUpAssetCatalog>("Home/OfficialHomeFastHangUpAssetCatalog")
                ?? throw new InvalidOperationException("Original home acceleration assets are unavailable.");
    }
}
