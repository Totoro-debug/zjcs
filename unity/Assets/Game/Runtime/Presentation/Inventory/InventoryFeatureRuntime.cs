using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Home;

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
            if (!ProfileSlotCatalog.TryParseSlotId(selectedSlotId))
                throw new ArgumentException("A selected profile slot is required.", nameof(selectedSlotId));

            var slots = new ProfileSlotCatalog(Application.persistentDataPath);
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
            HomeFeatureRuntime home = FindObjectOfType<HomeFeatureRuntime>();
            if (item == null || home == null || home.TreasureGacha == null) return Array.Empty<int>();
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            var sourceTypes = new List<int>(2);
            if (home.TreasureGacha.ResolveItemSource(item.ItemId, now) != null)
                sourceTypes.Add(0);
            TreasureItemConfig sourceItem = home.TreasureGacha.Catalog.Item(item.ItemId);
            if (sourceItem == null) return sourceTypes;
            TreasureGachaSourceNavigation elementSource = home.TreasureGacha.ResolveElementItemSource(
                item.ItemId, sourceItem.ElementType, now);
            if (elementSource != null) sourceTypes.Add(elementSource.SelectedElementGachaType);
            return sourceTypes;
        }

        private void OpenTreasureGachaSource(OfflineInventoryItemView item, int gachaType)
        {
            HomeFeatureRuntime home = FindObjectOfType<HomeFeatureRuntime>();
            if (item == null || home == null) return;
            TreasureItemConfig sourceItem = home.TreasureGacha?.Catalog.Item(item.ItemId);
            if (gachaType >= 8 && sourceItem == null) return;
            Action<int> returnToSource = ReopenSourceItem;
            TreasureGachaSourceNavigation navigation = gachaType >= 8
                ? home.OpenTreasureGachaFromElementItemSource(item.ItemId, sourceItem.ElementType, returnToSource)
                : home.OpenTreasureGachaFromItemSource(item.ItemId, 0, returnToSource);
            if (navigation == null) home.Hud?.ShowNotice("该扭蛋来源暂时无法打开");
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
