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
        private IDisposable uiBinding;
        private OfflinePartnerUiSoundScope soundScope;

        public void Open(OfficialPartnerGiftPageAssetCatalog assets, IPartnerGiftUiServices services,
            IPartnerGiftNpcData npc, PartnerGiftNpcInfo info, OfflinePartnerLocalizationService localization,
            Action onGiftApplied)
        {
            if (assets == null || services == null || npc == null || info == null || localization == null)
                throw new ArgumentNullException();
            if (uiBinding != null) throw new InvalidOperationException("Original partner content is already open.");
            try
            {
                soundScope = new OfflinePartnerUiSoundScope();
                uiBinding = OfflineUiBindingModule.Register(transform, new OfflineUiBindings {
                    Font = () => Resources.Load<TMPro.TMP_FontAsset>("Combat/FZY4K-Dynamic"),
                    Localize = key => localization.GetText(key, LanguageType.zh_CN),
                    Pool = (scope, prefab, parent, keepWorld) => Instantiate(prefab, parent, keepWorld)
                });
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
            catch { Close(); throw; }
        }

        public void Close()
        {
            uiBinding?.Dispose(); uiBinding = null;
            if (content != null) { content.SetActive(false); Destroy(content); }
            content = null;
            soundScope?.Dispose(); soundScope = null;

        }
        private void OnDestroy() => Close();
    }
}
