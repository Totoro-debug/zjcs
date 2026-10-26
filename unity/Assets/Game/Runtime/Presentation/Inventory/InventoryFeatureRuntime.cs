using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryFeatureRuntime : MonoBehaviour
    {
        [SerializeField] private OfficialInventoryPanelView panelView;
        private OfflineMainlineProgressSession session;
        private OfficialEquipmentCatalog equipmentCatalog;

        public event Action<OfflineInventoryItemView> ItemSelected;
        public event Action Closed;

        public void OpenProfile(string selectedSlotId)
        {
            OpenProfile(selectedSlotId, Application.persistentDataPath);
        }

        public void OpenProfile(string selectedSlotId, string savesRoot)
        {
            if (!ProfileSlotCatalog.TryParseSlotId(selectedSlotId))
                throw new ArgumentException("A selected profile slot is required.", nameof(selectedSlotId));

            var slots = new ProfileSlotCatalog(string.IsNullOrEmpty(savesRoot) ? Application.persistentDataPath : savesRoot);
            if (!slots.Contains(selectedSlotId))
                throw new InvalidOperationException("The selected profile slot no longer exists.");

            OfficialInventoryAssetCatalog inventoryAssets = OfficialInventoryAssetCatalog.LoadDefault();
            equipmentCatalog = OfficialEquipmentAssetCatalog.LoadDefault().Load();
            session = OfflineMainlineProgressSession.OpenCharacter(slots, selectedSlotId);
            if (panelView == null) panelView = GetComponent<OfficialInventoryPanelView>();
            if (panelView == null) panelView = gameObject.AddComponent<OfficialInventoryPanelView>();
            panelView.Initialize(inventoryAssets, equipmentCatalog, session.Save);
            panelView.SetTreasureGachaSourceProvider(ResolveTreasureGachaSourceTypes);
            panelView.TreasureGachaSourceSelected -= OpenTreasureGachaSource;
            panelView.TreasureGachaSourceSelected += OpenTreasureGachaSource;
            panelView.ItemSelected -= ForwardItemSelection;
            panelView.ItemSelected += ForwardItemSelection;
            panelView.Closed -= ForwardClosed;
            panelView.Closed += ForwardClosed;
        }

        public void Refresh()
        {
            if (session == null || panelView == null)
                throw new InvalidOperationException("Open a selected profile before refreshing the inventory.");
            session.Reload();
            panelView.Refresh(session.Save);
        }

        public void Close()
        {
            if (panelView != null) panelView.Close();
        }

        private void ForwardItemSelection(OfflineInventoryItemView item) => ItemSelected?.Invoke(item);
        private void ForwardClosed() => Closed?.Invoke();

        private IReadOnlyList<int> ResolveTreasureGachaSourceTypes(OfflineInventoryItemView item)
        {
            IInventoryHomeNavigationContext home = FindHomeNavigationContext();
            return item == null || home == null
                ? Array.Empty<int>()
                : home.TreasureGachaSourceTypes(item.ItemId);
        }

        private void OpenTreasureGachaSource(OfflineInventoryItemView item, int gachaType)
        {
            IInventoryHomeNavigationContext home = FindHomeNavigationContext();
            if (item == null || home == null) return;
            Action<int> returnToSource = ReopenSourceItem;
            if (!home.OpenTreasureGachaSource(item.ItemId, gachaType, returnToSource))
                home.ShowInventoryNotice("该扭蛋来源暂时无法打开");
        }

        private static IInventoryHomeNavigationContext FindHomeNavigationContext()
        {
            foreach (MonoBehaviour component in FindObjectsOfType<MonoBehaviour>())
                if (component is IInventoryHomeNavigationContext context) return context;
            return null;
        }

        private void ReopenSourceItem(int itemId)
        {
            if (session == null || panelView == null) return;
            session.Reload();
            panelView.Refresh(session.Save);
            panelView.ReopenItemDetail(itemId);
        }
    }
}
