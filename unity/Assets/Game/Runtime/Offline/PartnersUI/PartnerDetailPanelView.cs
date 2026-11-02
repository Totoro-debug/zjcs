using System;
using UnityEngine;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.WorldUI;

namespace Zjcs.Offline.PartnersUI
{
    public sealed class PartnerDetailPanelView : MonoBehaviour
    {
        private GameObject content;
        private OfflinePartnerUiSoundScope soundScope;
        private Func<string, string> previousLocalize;
        private Func<TMPro.TMP_FontAsset> previousLanguageFont;
        private Func<Transform, GameObject, Transform, bool, GameObject> previousPool;
        public void Open(OfficialPartnerDetailPropertyAssetCatalog assets, IPartnerDetailPropertyServices services,
            IPartnerDetailNpcData npc, PartnerDetailNpcInfo info, OfflinePartnerLocalizationService localization)
        {
            if (assets == null || services == null || npc == null || info == null || localization == null) throw new ArgumentNullException();
            soundScope = new OfflinePartnerUiSoundScope();
            previousLocalize = WorldMultiLanguageText.Localize;
            previousLanguageFont = WorldMultiLanguageText.LanguageFont;
            WorldMultiLanguageText.LanguageFont = () => Resources.Load<TMPro.TMP_FontAsset>("Combat/FZY4K-Dynamic");
            WorldMultiLanguageText.Localize = key => localization.GetText(key, LanguageType.zh_CN);
            previousPool = ProfessionGameObjectPoolBridge.Provider;
            ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, keepWorld) => Instantiate(prefab, parent, keepWorld);
            content = Instantiate(assets.PropertyPrefab, transform, false);
            content.name = "OfficialPartnerDetailPropertyContent";
            var panel = content.GetComponent<NpcDetailPanelPropContent>();
            if (panel == null) throw new InvalidOperationException("Official partner detail-property controller is missing.");
            panel.Configure(services); panel.Init(npc, info); content.SetActive(true);
        }
        public void Close()
        {
            if (content != null) { content.SetActive(false); Destroy(content); }
            soundScope?.Dispose(); soundScope = null;
            content = null; ProfessionGameObjectPoolBridge.Provider = previousPool; previousPool = null;
            WorldMultiLanguageText.Localize = previousLocalize; previousLocalize = null;
            WorldMultiLanguageText.LanguageFont = previousLanguageFont; previousLanguageFont = null;
        }
    }
}
