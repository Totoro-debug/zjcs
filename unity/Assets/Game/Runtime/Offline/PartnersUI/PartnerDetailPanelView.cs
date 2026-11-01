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
        private Func<string, string> previousLocalize;
        private Func<Transform, GameObject, Transform, bool, GameObject> previousPool;
        public void Open(OfficialPartnerDetailPropertyAssetCatalog assets, IPartnerDetailPropertyServices services,
            IPartnerDetailNpcData npc, PartnerDetailNpcInfo info, OfflinePartnerLocalizationService localization)
        {
            if (assets == null || services == null || npc == null || info == null || localization == null) throw new ArgumentNullException();
            previousLocalize = WorldMultiLanguageText.Localize;
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
            if (content != null) Destroy(content);
            content = null; ProfessionGameObjectPoolBridge.Provider = previousPool; previousPool = null;
            WorldMultiLanguageText.Localize = previousLocalize; previousLocalize = null;
        }
    }
}
