using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView : IOfflineHomeRepairState
    {
        private OfflineHomeRepairWindows repairWindows;
        private PartnerWindowBlurCapture repairCapture;

        private void ShowNativeRepair(int type)
        {
            if (host.Rules.FurnitureOf(type) == null) return;
            CloseDialog();
            var masks = OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
            repairCapture = host.gameObject.AddComponent<PartnerWindowBlurCapture>();
            repairCapture.Initialize(masks.BlurMaterial);
            repairWindows = new OfflineHomeRepairWindows(canvas, catalog.Font,
                masks.BlurMaskPrefab, host, repairCapture, PostPartnerPromptSound, this);
            repairWindows.ActiveWindowChanged += (root, key) =>
            {
                activeDialog = root;
                activeDialogRenderer = null;
                activePanelKey = key;
                if (root) bedAwards?.NotifyWindowOpened(root);
            };
            repairWindows.WindowClosed += (furniture, notification) =>
            {
                if (notification) host.OnFruitTreeRepairDialogClosed((int)furniture);
            };
            repairWindows.ShowRepair((EFurniture)type, false);
        }

        bool IOfflineHomeRepairState.HasRepairCost(EFurniture furniture)
        {
            var record = host.Rules.FurnitureOf((int)furniture);
            if (record == null) return false;
            foreach (var cost in record.repairCost ?? Array.Empty<Zjcs.Offline.Home.HomeCostEntry>())
                if (cost != null && host.ItemCount(cost.itemId) < cost.count) return false;
            return true;
        }

        void IOfflineHomeRepairState.Repair(EFurniture furniture, Action onSuccess)
        {
            if (host.TryRepair((int)furniture)) onSuccess();
        }
        void IOfflineHomeRepairState.SystemMessage(string text) => Notify(text);
        void IOfflineHomeRepairState.GetValidHangUpIdList(EFurniture furniture, List<int> result)
        {
            result.Clear();
            foreach (int id in OfficialHomeRepairAssetCatalog.LoadDefault().RecordedHangUpIds(furniture))
                if (host.Session.Definitions.Any(definition => definition.HangUpId == id && definition.Unlocked))
                    result.Add(id);
        }
        ProfessionPropertyType IOfflineHomeRepairState.GetEffectFixedPropType(int hangUpId)
            => (ProfessionPropertyType)host.Rules.HangUp(hangUpId).effectFixedProp;
        double IOfflineHomeRepairState.GetHangUpReward(int hangUpId, double hours)
            => host.Session.Preview(hangUpId).HourlyRate * hours;
    }
}
