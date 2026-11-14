using System;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        OfflineHomeHangUpWindows nativeBedWindows;
        OfflineHomeBedServices nativeBedServices;
        PartnerWindowBlurCapture nativeBedCapture;
        OfflineHomeFastHangUpProgress nativeFastProgress;
        readonly HashSet<UIBase> nativeHiddenUiOwners = new HashSet<UIBase>();

        void SetNativeFastUiVisible(UIBase owner, bool visible)
        {
            bool changed = visible ? nativeHiddenUiOwners.Remove(owner) : nativeHiddenUiOwners.Add(owner);
            if (!changed) return;
            var group = canvas.transform.Find("Content/Main").GetComponent<CanvasGroup>();
            if (!group) throw new InvalidOperationException("Original home Main CanvasGroup is unavailable.");
            group.alpha = nativeHiddenUiOwners.Count == 0 ? 1 : 0;
            group.interactable = group.alpha > 0;
            group.blocksRaycasts = group.alpha > 0;
        }

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
                nativeBedServices.FastAwards = GetBedAwards;
                nativeFastProgress = new OfflineHomeFastHangUpProgress(host, canvas, nativeBedServices,
                    GetBedAwards().Images, SetNativeFastUiVisible);
                nativeBedServices.OpenFastProgress = nativeFastProgress.Show;
                nativeBedServices.OpenFastWindow = (id, parent) => nativeBedWindows.ShowFastSelection(id, parent);
                nativeBedServices.FastOperation = (id, autoReceive, cost, completed) =>
                    Zjcs.Official.HomeFastHangUpUI.HangUpUIUtils.OperFastHangUp(nativeBedServices,
                        id, autoReceive, cost, completed);
                nativeBedServices.OpenOutputWindow = _ => throw new NotSupportedException("The original activity output-detail owner is not installed.");
                nativeBedServices.OpenBlessingWindow = nativeBedWindows.ShowBlessingRule;
                nativeBedWindows.ActiveWindowChanged += root => {
                    activeDialog = root;
                    activeDialogRenderer = null;
                    activePanelKey = root ? "bed" : null;
                    bedOpen = root;
                };
                nativeBedWindows.WindowOpened += root => bedAwards?.NotifyWindowOpened(root);
                var bed = nativeBedWindows.ShowBed();
                bed.FastHangUpSoundEvent.InstanceHost = GetBedAwards();
            }
            catch { ReleaseNativeBedWindow(); throw; }
        }

        void ReleaseNativeBedWindow()
        {
            nativeFastProgress?.Dispose(); nativeFastProgress = null;
            nativeBedWindows?.Dispose(); nativeBedWindows = null;
            nativeBedServices?.Dispose(); nativeBedServices = null;
            if (nativeBedCapture) UnityEngine.Object.Destroy(nativeBedCapture);
            nativeBedCapture = null;
        }
    }
}
