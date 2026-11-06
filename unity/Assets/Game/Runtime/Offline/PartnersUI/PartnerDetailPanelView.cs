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
        private IDisposable uiBinding;
        private OfflinePartnerUiSoundScope soundScope;
        public void Open(OfficialPartnerDetailPropertyAssetCatalog assets, IPartnerDetailPropertyServices services,
            IPartnerDetailNpcData npc, PartnerDetailNpcInfo info, OfflinePartnerLocalizationService localization)
        {
            if (assets == null || services == null || npc == null || info == null || localization == null) throw new ArgumentNullException();
            if (uiBinding != null) throw new InvalidOperationException("Original partner content is already open.");
            try
            {
                soundScope = new OfflinePartnerUiSoundScope();
                uiBinding = OfflineUiBindingModule.Register(transform, new OfflineUiBindings {
                    Font = () => Resources.Load<TMPro.TMP_FontAsset>("Combat/FZY4K-Dynamic"),
                    Localize = key => localization.GetText(key, LanguageType.zh_CN),
                    Pool = (scope, prefab, parent, keepWorld) => Instantiate(prefab, parent, keepWorld)
                });
                content = Instantiate(assets.PropertyPrefab, transform, false);
                content.name = "OfficialPartnerDetailPropertyContent";
                var panel = content.GetComponent<NpcDetailPanelPropContent>();
                if (panel == null) throw new InvalidOperationException("Official partner detail-property controller is missing.");
                panel.Configure(services); panel.Init(npc, info); content.SetActive(true);
            }
            catch { Close(); throw; }
        }

        public void Close()
        {
            uiBinding?.Dispose(); uiBinding = null;
            if (content != null) { content.SetActive(false); Destroy(content); }
            soundScope?.Dispose(); soundScope = null;
            content = null;

        }
        private void OnDestroy() => Close();
    }
}
