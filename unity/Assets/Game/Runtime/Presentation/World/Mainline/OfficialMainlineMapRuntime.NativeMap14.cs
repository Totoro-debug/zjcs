using System;
using UnityEngine;
using Zjcs.Presentation.World.NativeMap14;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMap14NativeWorldView nativeMap14World;
        private float nativeMap14RefreshAt;

        private void InitializeNativeMap14()
        {
            if (mapClassId != 14 || avatar == null || worldCamera == null || mainlineSharedWorld?.IsReady != true)
                throw new InvalidOperationException("The native Map14 terrain requires its running shared world, player and camera.");
            nativeMap14World = OfficialMap14NativeWorldView.Create(mapRoot, worldCamera,
                new MainlineNativeGroundState(this), mainlineSharedWorld);
            InitializeRoadblockDisplays();
            nativeMap14RefreshAt = Time.unscaledTime + 0.25f;
        }

        private void RefreshNativeMap14State()
        {
            if (nativeMap14World == null || Time.unscaledTime < nativeMap14RefreshAt) return;
            nativeMap14RefreshAt = Time.unscaledTime + 0.25f;
            nativeMap14World.RefreshState();
            RefreshRoadblockDisplays();
        }

        private void DisposeNativeMap14()
        {
            DisposeRoadblockDisplays();
            nativeMap14World?.Dispose();
            nativeMap14World = null;
        }
    }
}
