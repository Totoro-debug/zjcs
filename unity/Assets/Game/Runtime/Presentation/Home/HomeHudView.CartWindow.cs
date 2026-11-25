using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.HomeOutputAddition;
using Zjcs.Official.HomeCartWindowsUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private OfflineHomeCartWindows nativeCartWindows;
        private OfflineHomeCartServices nativeCartServices;
        private OfflineHomeBedServices nativeCartBedServices;
        private PartnerWindowBlurCapture nativeCartCapture;

        internal bool NativeCartModalOpen => nativeCartWindows != null && cartOpen;
        internal OfflineHomeCartWindows NativeCartWindows => nativeCartWindows;

        private void EnsureNativeCartOwner()
        {
            if (nativeCartWindows != null && nativeCartServices != null) return;
            ReleaseNativeCartWindow();
            try
            {
                var masks = OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
                nativeCartCapture = host.gameObject.AddComponent<PartnerWindowBlurCapture>();
                nativeCartCapture.Initialize(masks.BlurMaterial);

                nativeCartBedServices = new OfflineHomeBedServices(host, _ => false,
                    text => Notify(text), ShowBedAward, BedOutputTextFor, IsMapRegionPublic);
                nativeCartServices = new OfflineHomeCartServices(host, nativeCartBedServices,
                    GetBedAwards(), null);
                nativeCartWindows = new OfflineHomeCartWindows(host, canvas, host, masks.BlurMaskPrefab,
                    nativeCartCapture, nativeFrameScreen, PostPartnerPromptSound,
                    nativeCartServices, nativeCartBedServices, nativeCurrencyBar?.Manager);

                nativeCartServices.Windows = nativeCartWindows;
                nativeCartBedServices.ConfirmWindow = (content, title, confirmed) => { };
                nativeCartWindows.BindOutputDetails(nativeCartBedServices.OutputAddition);
                nativeCartBedServices.OpenOutputWindow = type => nativeCartWindows.ShowOutputAddition(
                    (OutputGroupType)type);
                nativeCartBedServices.OpenBlessingWindow = nativeCartWindows.ShowBlessingRule;
                nativeCartWindows.ActiveWindowChanged += root =>
                {
                    if (root)
                    {
                        nativeCartServices.AttachGlobalSoundHost();
                        activeDialog = root;
                        activeDialogRenderer = null;
                        activePanelKey = "cart";
                        cartOpen = true;
                        return;
                    }
                    nativeCartServices?.DetachGlobalSoundHost();
                    if (activePanelKey == "cart")
                    {
                        activeDialog = null;
                        activeDialogRenderer = null;
                        activePanelKey = null;
                    }
                    cartOpen = false;
                };
                nativeCartWindows.WindowOpened += root => bedAwards?.NotifyWindowOpened(root);
            }
            catch
            {
                ReleaseNativeCartWindow();
                throw;
            }
        }

        private void ShowNativeCart()
        {
            CloseDialog();
            EnsureNativeCartOwner();
            nativeCartWindows.ShowCart();
        }

        internal void ShowCartAwards(IReadOnlyList<OfflineHomeCollectedReward> rewards)
        {
            if (rewards == null || rewards.Count == 0) return;
            EnsureNativeCartOwner();
            var list = nativeCartServices.ToAwardDataList(rewards);
            if (list.Count > 0)
                nativeCartWindows.ShowAwardPanel(list);
        }

        private void ReleaseNativeCartWindow()
        {
            nativeCartWindows?.Dispose();
            nativeCartWindows = null;
            nativeCartServices?.Dispose();
            nativeCartServices = null;
            nativeCartBedServices?.Dispose();
            nativeCartBedServices = null;
            if (nativeCartCapture) UnityEngine.Object.Destroy(nativeCartCapture);
            nativeCartCapture = null;
        }
    }
}
