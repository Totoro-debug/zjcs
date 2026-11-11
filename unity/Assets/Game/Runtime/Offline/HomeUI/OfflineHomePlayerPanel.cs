using System;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.PartnersUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.HomeUI
{
    // Instantiates the full original subtree once. The enclosing MainCanvas and
    // profile services remain the caller's responsibility.
    public sealed class OfflineHomePlayerPanel : IDisposable
    {
        IDisposable uiScope;
        OfflinePartnerUiSoundScope sound;
        FontAssetFontService fonts;
        public GameObject Root { get; private set; }
        public TeamPlayerIcon Icon { get; private set; }
        public HomePlayerExpSlider Experience { get; private set; }
        public PublicPlayerInfoUI Visibility { get; private set; }

        public OfflineHomePlayerPanel(Transform parent, ITeamIconServices player, ICharacterPortraitManager portraits,
            IHomePlayerPanelBindings services, IProfessionPropertyBindings properties,
            OfficialHomePlayerPanelAssetCatalog assets = null, OfficialHomePlayerInfoAssetCatalog textAssets = null)
        {
            if (!parent || player == null || portraits == null || services == null || properties == null)
                throw new ArgumentException("Complete original player-panel services are required.");
            assets = assets ? assets : Resources.Load<OfficialHomePlayerPanelAssetCatalog>("Home/OfficialHomePlayerPanelAssetCatalog");
            textAssets = textAssets ? textAssets : OfficialHomePlayerInfoAssetCatalog.LoadDefault();
            if (!assets || !assets.prefab) throw new InvalidOperationException("Original complete player panel is unbound.");
            var staging = new GameObject("HomePlayerPanelStaging", typeof(RectTransform));
            staging.transform.SetParent(parent, false); staging.SetActive(false);
            try
            {
                fonts = textAssets.CreateFontService(); fonts.Initialize();
                sound = new OfflinePartnerUiSoundScope(id => assets.sounds.FirstOrDefault(row => row.eventId == id)?.clip);
                Root = Object.Instantiate(assets.prefab, staging.transform, false); Root.SetActive(false);
                Root.AddComponent<HomePlayerPanelScope>().Bindings = services;
                Root.AddComponent<HomeHudResourceScope>().Loader = services.Resources;
                var originalTexts = Root.GetComponentsInChildren<TMP_Text>(true);
                foreach (var text in originalTexts) fonts.SetupFont(text.font);
                uiScope = OfflineUiBindingModule.Register(Root.transform, new OfflineUiBindings {
                    Localize = services.Text, Font = () => originalTexts[0].font,
                    Pool = (_, prefab, slot, keepWorld) => Object.Instantiate(prefab, slot, keepWorld)
                });
                foreach (var label in Root.GetComponentsInChildren<WorldMultiLanguageText>(true)) label.Localizer = services.Text;
                foreach (var controller in Root.GetComponentsInChildren<HomePlayerInfoUIBase>(true)) controller.Configure(services);
                foreach (var button in Root.GetComponentsInChildren<HomeIntroductionPopBtn>(true)) button.Configure(services);
                foreach (var button in Root.GetComponentsInChildren<ProfessionBlessPropBtn>(true)) button.Configure(properties);
                Icon = Root.GetComponentInChildren<TeamPlayerIcon>(true); Icon.Configure(player);
                foreach (var portrait in Root.GetComponentsInChildren<TeamHeadCharacterPortrait>(true)) portrait.Bind(portraits);
                Experience = Root.GetComponentInChildren<HomePlayerExpSlider>(true); Experience.Configure(services);
                Visibility = Root.GetComponent<PublicPlayerInfoUI>();
                Root.transform.SetParent(parent, false); Root.SetActive(true);
            }
            catch { Dispose(); throw; }
            finally { Object.Destroy(staging); }
        }

        public void Dispose()
        {
            if (Root) { Root.SetActive(false); Object.Destroy(Root); Root = null; }
            Icon = null; Experience = null; Visibility = null;
            uiScope?.Dispose(); uiScope = null;
            sound?.Dispose(); sound = null;
            fonts?.Dispose(); fonts = null;
        }
    }
}
