using System;
using Zjcs.Official.GachaUI;
using Zjcs.Offline.GachaUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private void ConfirmPetHundredDraw(int count, Action<int> confirmed)
        {
            var assets = OfficialMarkableConfirmAssetCatalog.LoadDefault();
            var services = new OfflinePetHundredReminderServices(host.PetGacha, () => DateTime.Now,
                initialize => nativeCurrencyWindows.PopupConfirm(assets.Prefab, assets.Text,
                    dialog => initialize(dialog.GetComponent<MarkableConfirmDialog>())),
                result => Notify(result.Message));
            GachaPanelUtils.HundredGachePreRemind(services, host.PetGachaCatalog.Gacha.GachaType, count, confirmed);
        }
    }
}
