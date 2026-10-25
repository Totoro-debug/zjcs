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
        private string activeSlotId;
        private string activeSavesRoot;
        private long nextLegionBuildingSettlementUnixSeconds;

        public OfflineCharacterProgressionService Progression => progression;
        public event Action<string> NavigationRequested;

        public void OpenProfile(string selectedSlotId)
        {
            OpenProfile(selectedSlotId, Application.persistentDataPath);
        }

        public void OpenProfile(string selectedSlotId, string savesRoot)
        {
            if (!ProfileSlotCatalog.TryParseSlotId(selectedSlotId))
                throw new ArgumentException("A selected profile slot is required.", nameof(selectedSlotId));

            OfficialCharacterAssetCatalog assetCatalog = OfficialCharacterAssetCatalog.LoadDefault();
            var slots = new ProfileSlotCatalog(string.IsNullOrEmpty(savesRoot)
                ? Application.persistentDataPath : savesRoot);
            if (!slots.Contains(selectedSlotId))
                throw new InvalidOperationException("The selected profile slot no longer exists.");

            activeSlotId = selectedSlotId;
            activeSavesRoot = string.IsNullOrEmpty(savesRoot) ? Application.persistentDataPath : savesRoot;
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
            ProgressionResult buildingSettlement = progression.LegionBuildings.Settle(now);
            if (buildingSettlement.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException(buildingSettlement.Message
                    ?? "Guild building HangUp progress could not be settled.");
            nextLegionBuildingSettlementUnixSeconds = checked(now
                + Math.Max(1, progression.LegionBuildings.SettlePeriodSeconds));
            if (panelView == null) panelView = GetComponent<OfficialCharacterPanelView>();
            if (panelView == null) panelView = gameObject.AddComponent<OfficialCharacterPanelView>();
            panelView.Initialize(assetCatalog, progression, activeSlotId, activeSavesRoot);
            panelView.OfficialModuleRequested -= ForwardNavigation;
            panelView.OfficialModuleRequested += ForwardNavigation;
        }

        private void Update()
        {
            if (progression == null || progression.LegionBuildings.ActiveOperation == null) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now < nextLegionBuildingSettlementUnixSeconds) return;
            nextLegionBuildingSettlementUnixSeconds = checked(now
                + Math.Max(1, progression.LegionBuildings.SettlePeriodSeconds));
            ProgressionResult settlement = progression.LegionBuildings.Settle(now);
            if (settlement.Status == ProgressionResultStatus.InvalidState)
            {
                Debug.LogWarning("Guild building HangUp progress could not be settled: " + settlement.Message);
                return;
            }
            if (settlement.IsApplied && panelView != null) panelView.Refresh();
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
