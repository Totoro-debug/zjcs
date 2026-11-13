using System;
using UnityEngine;
using Zjcs.Official.HomeScreenUI;
using Zjcs.Offline.HomeHudUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private ScreenAdaptService nativeFrameScreen;
        private Transform nativeHomeSlot, nativePublicSlot;

        private void CreateNativeFrame()
        {
            var staging = new GameObject("HomeFrameDependencyBinding");
            staging.SetActive(false);
            try
            {
                nativeFrameScreen = new ScreenAdaptService();
                nativeFrameScreen.Initialize();
                var root = UnityEngine.Object.Instantiate(
                    OfflineHomeHudAssets.LoadDefault().framePrefab, staging.transform, false);
                root.SetActive(false);
                canvas = root.GetComponent<Canvas>();
                if (!canvas) throw new InvalidOperationException("Original home frame Canvas is unavailable.");
                // Keep the current desktop window host; geometry, scaler, masks,
                // safe areas and Main/Public ancestors come from the prefab.
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 60;
                root.GetComponent<CanvasWidthAdjuster>().ScreenAdaptService = nativeFrameScreen;
                foreach (var area in root.GetComponentsInChildren<SafeArea>(true))
                    area.ScreenAdaptService = nativeFrameScreen;
                nativeHomeSlot = root.transform.Find("Content/Main/Anim");
                nativePublicSlot = root.transform.Find("Content/Main/Public/Anim");
                if (!nativeHomeSlot || !nativePublicSlot)
                    throw new InvalidOperationException("Original home Main/Public slots are unavailable.");
                root.transform.SetParent(null, false);
                root.SetActive(true);
            }
            catch
            {
                if (canvas) UnityEngine.Object.Destroy(canvas.gameObject);
                canvas = null;
                ReleaseNativeFrame();
                throw;
            }
            finally { UnityEngine.Object.Destroy(staging); }
        }

        private void ReleaseNativeFrame()
        {
            nativeHomeSlot = null;
            nativePublicSlot = null;
            nativeFrameScreen?.Dispose();
            nativeFrameScreen = null;
        }

        private void BindNativeHomeSafeArea(GameObject root)
        {
            var prefab = OfflineHomeHudAssets.LoadDefault().homePrefab;
            foreach (var original in prefab.GetComponentsInChildren<SafeArea>(true))
            {
                var path = original.transform.name;
                for (var parent = original.transform.parent; parent != prefab.transform; parent = parent.parent)
                    path = parent.name + "/" + path;
                var target = root.transform.Find(path);
                if (!target) throw new InvalidOperationException("Original HomeUI safe-area path is unavailable: " + path);
                var previous = target.GetComponent<Zjcs.Official.WorldUI.WorldSafeArea>();
                if (previous)
                {
                    previous.enabled = false;
                    UnityEngine.Object.Destroy(previous);
                }
                var area = target.gameObject.AddComponent<SafeArea>();
                area.PaddingTop = original.PaddingTop;
                area.PaddingBottom = original.PaddingBottom;
                area.DisableTopAdapt = original.DisableTopAdapt;
                area.DisableBottomAdapt = original.DisableBottomAdapt;
                area.ScreenAdaptService = nativeFrameScreen;
            }
        }
    }
}
