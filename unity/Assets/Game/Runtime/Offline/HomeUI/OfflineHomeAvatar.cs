using System;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;
using Zjcs.Offline.PartnersUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.HomeUI
{
    // Bind the complete original avatar before its AutoInit/PlayerUIBase Start.
    // Character data, photographer and reward display are owned by the profile.
    public sealed class OfflineHomeAvatar : IDisposable
    {
        private OfflinePartnerUiSoundScope sound;
        public GameObject Root { get; private set; }
        public TeamPlayerIcon Icon { get; private set; }
        public HomePlayerExpSlider Experience { get; private set; }

        public OfflineHomeAvatar(Transform parent, ITeamIconServices player,
            ICharacterPortraitManager portraits, IHomeExperienceServices experience,
            OfficialHomeAvatarAssetCatalog assets = null)
        {
            if (!parent || player == null || portraits == null || experience == null)
                throw new ArgumentException("Complete original home avatar bindings are required.");
            assets = assets ?? OfficialHomeAvatarAssetCatalog.LoadDefault();
            var staging = new GameObject("HomeAvatarStaging", typeof(RectTransform));
            staging.transform.SetParent(parent, false); staging.SetActive(false);
            try
            {
                sound = new OfflinePartnerUiSoundScope(assets.Sound);
                Root = Object.Instantiate(assets.prefab, staging.transform, false); Root.SetActive(false);
                Icon = Root.GetComponent<TeamPlayerIcon>(); Icon.Configure(player);
                foreach (var portrait in Root.GetComponentsInChildren<TeamHeadCharacterPortrait>(true))
                    portrait.Bind(portraits);
                Experience = Root.GetComponentInChildren<HomePlayerExpSlider>(true);
                Experience.Configure(experience);
                Root.transform.SetParent(parent, false); Root.SetActive(true);
            }
            catch { Dispose(); throw; }
            finally { Object.Destroy(staging); }
        }

        public void Dispose()
        {
            if (Root) { Root.SetActive(false); Object.Destroy(Root); Root = null; }
            Icon = null; Experience = null;
            sound?.Dispose(); sound = null;
        }
    }
}
