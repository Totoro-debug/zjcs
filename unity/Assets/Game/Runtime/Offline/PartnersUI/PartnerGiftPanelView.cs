using System;
using UnityEngine;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.WorldUI;

namespace Zjcs.Offline.PartnersUI
{
    // Hosts the recovered official gift-content subtree with local inventory,
    // friendship and transaction services.
    public sealed class PartnerGiftPanelView : MonoBehaviour
    {
        private GameObject content;
        private OfflinePartnerUiSoundScope soundScope;
        private Func<string, string> previousLocalize;
        private Func<TMPro.TMP_FontAsset> previousLanguageFont;
        private Func<Transform, GameObject, Transform, bool, GameObject> previousPool;

        public void Open(OfficialPartnerGiftPageAssetCatalog assets, IPartnerGiftUiServices services,
            IPartnerGiftNpcData npc, PartnerGiftNpcInfo info, OfflinePartnerLocalizationService localization,
            Action onGiftApplied)
        {
            if (assets == null || services == null || npc == null || info == null || localization == null)
                throw new ArgumentNullException();
            soundScope = new OfflinePartnerUiSoundScope();
            previousLocalize = WorldMultiLanguageText.Localize;
            previousLanguageFont = WorldMultiLanguageText.LanguageFont;
            WorldMultiLanguageText.LanguageFont = () => Resources.Load<TMPro.TMP_FontAsset>("Combat/FZY4K-Dynamic");
            WorldMultiLanguageText.Localize = key => localization.GetText(key, LanguageType.zh_CN);
            previousPool = ProfessionGameObjectPoolBridge.Provider;
            ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, keepWorld) => Instantiate(prefab, parent, keepWorld);
            content = Instantiate(assets.GiftPrefab, transform, false);
            content.name = "OfficialPartnerGiftContent";
            var panel = content.GetComponent<NpcDetailPanelGiftContent>();
            var level = content.GetComponentInChildren<NpcFriendshipLevelContent>(true);
            if (panel == null || level == null) throw new InvalidOperationException("Official partner gift controllers are missing.");
            panel.Configure(services);
            level.Configure(services);
            level.Init(npc, info);
            panel.Init(npc, info, onGiftApplied, level.ResetExpPreview, level.ShowExpPreview);
            content.SetActive(true);
        }

        public void Close()
        {
            if (content != null) { content.SetActive(false); Destroy(content); }
            content = null;
            soundScope?.Dispose(); soundScope = null;
            ProfessionGameObjectPoolBridge.Provider = previousPool;
            previousPool = null;
            WorldMultiLanguageText.Localize = previousLocalize;
            previousLocalize = null;
            WorldMultiLanguageText.LanguageFont = previousLanguageFont;
            previousLanguageFont = null;
        }
    }
}
