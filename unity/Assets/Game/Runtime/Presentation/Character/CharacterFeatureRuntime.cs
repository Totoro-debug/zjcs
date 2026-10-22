using System;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Character
{
    [DisallowMultipleComponent]
    public sealed class CharacterFeatureRuntime : MonoBehaviour
    {
        [SerializeField] private OfficialCharacterPanelView panelView;

        private OfflineMainlineProgressSession session;
        private OfflineCharacterProgressionService progression;

        public OfflineCharacterProgressionService Progression => progression;
        public event Action<string> NavigationRequested;

        public void OpenProfile(string selectedSlotId)
        {
            if (!ProfileSlotCatalog.TryParseSlotId(selectedSlotId))
                throw new ArgumentException("A selected profile slot is required.", nameof(selectedSlotId));

            OfficialCharacterAssetCatalog assetCatalog = OfficialCharacterAssetCatalog.LoadDefault();
            var slots = new ProfileSlotCatalog(Application.persistentDataPath);
            if (!slots.Contains(selectedSlotId))
                throw new InvalidOperationException("The selected profile slot no longer exists.");

            session = OfflineMainlineProgressSession.OpenCharacter(slots, selectedSlotId);
            progression = new OfflineCharacterProgressionService(session, assetCatalog.Load());
            TryRegisterEquipmentProperties();
            try
            {
                OfficialSkillRecommendationSet recommendations = assetCatalog.LoadSkillRecommendations();
                progression.AttachSkillRules(assetCatalog.LoadSkillRules(), recommendations.recommendations);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Official skill progression rules are unavailable: " + error.Message);
            }
            ProgressionResult wardrobe = progression.InitializeWardrobe("wardrobe-initialize");
            if (wardrobe.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException(wardrobe.Message ?? "Character wardrobe initialization failed.");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ProgressionResult expiry = progression.SynchronizeExpiredItems(now,
                "wardrobe-expiry-sync-" + now.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (expiry.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException(expiry.Message ?? "Expired character appearances could not be synchronized.");
            if (panelView == null) panelView = GetComponent<OfficialCharacterPanelView>();
            if (panelView == null) panelView = gameObject.AddComponent<OfficialCharacterPanelView>();
            panelView.Initialize(assetCatalog, progression);
            panelView.OfficialModuleRequested -= ForwardNavigation;
            panelView.OfficialModuleRequested += ForwardNavigation;
        }

        private void TryRegisterEquipmentProperties()
        {
            try
            {
                OfficialEquipmentCatalog equipment = OfficialEquipmentAssetCatalog.LoadDefault().Load();
                progression.AttachEquipmentCatalog(equipment);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Official equipment properties are unavailable: " + error.Message);
            }
        }

        private void OnEnable()
        {
            Application.lowMemory += OnLowMemory;
        }

        private void OnDisable()
        {
            Application.lowMemory -= OnLowMemory;
        }

        private void OnLowMemory()
        {
            if (panelView != null) panelView.HandleLowMemory();
        }

        public void Refresh()
        {
            if (progression == null) throw new InvalidOperationException("Open a selected profile before refreshing the character page.");
            panelView.Refresh();
        }

        private void ForwardNavigation(string module)
        {
            NavigationRequested?.Invoke(module);
        }
    }
}
