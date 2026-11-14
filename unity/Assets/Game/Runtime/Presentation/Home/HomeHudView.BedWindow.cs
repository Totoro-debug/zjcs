using System;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.HomeHangUpWindowsUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        OfflineHomeHangUpWindows nativeBedWindows;
        OfflineHomeBedServices nativeBedServices;
        PartnerWindowBlurCapture nativeBedCapture;

        void ShowNativeBed()
        {
            CloseDialog();
            try
            {
                var masks = OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
                nativeBedCapture = host.gameObject.AddComponent<PartnerWindowBlurCapture>();
                nativeBedCapture.Initialize(masks.BlurMaterial);
                // There are no remote function-disable records in the offline
                // player. Activity bonus records remain the actual saved list.
                nativeBedServices = new OfflineHomeBedServices(host, _ => false,
                    text => Notify(text), ShowBedAward, BedOutputTextFor, IsMapRegionPublic);
                nativeBedWindows = new OfflineHomeHangUpWindows(canvas, host, masks.BlurMaskPrefab,
                    nativeBedCapture, nativeFrameScreen, PostPartnerPromptSound,
                    nativeBedServices, nativeBedServices.Output);
                nativeBedServices.Windows = nativeBedWindows;
                nativeBedServices.ConfirmWindow = nativeBedWindows.ShowConfirm;
                nativeBedServices.OpenFastWindow = (id, parent) => ShowFastForward(1);
                nativeBedServices.FastOperation = (id, autoReceive, cost, completed) => {
                    if (cost != null) throw new NotSupportedException("Bind the original paid acceleration transaction before using its operation.");
                    if (host.TryFastForward(1) > 0) completed?.Invoke();
                };
                nativeBedServices.OpenOutputWindow = _ => throw new NotSupportedException("The original activity output-detail owner is not installed.");
                nativeBedServices.OpenBlessingWindow = nativeBedWindows.ShowBlessingRule;
                nativeBedWindows.ActiveWindowChanged += root => {
                    activeDialog = root;
                    activeDialogRenderer = null;
                    activePanelKey = root ? "bed" : null;
                    bedOpen = root;
                };
                nativeBedWindows.WindowOpened += root => bedAwards?.NotifyWindowOpened(root);
                nativeBedWindows.ShowBed();
            }
            catch { ReleaseNativeBedWindow(); throw; }
        }

        void ReleaseNativeBedWindow()
        {
            nativeBedWindows?.Dispose(); nativeBedWindows = null;
            nativeBedServices?.Dispose(); nativeBedServices = null;
            if (nativeBedCapture) UnityEngine.Object.Destroy(nativeBedCapture);
            nativeBedCapture = null;
        }
    }
}
