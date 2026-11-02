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
            if (mapClassId != 14 || avatar == null || worldCamera == null)
                throw new InvalidOperationException("The native Map14 world requires its initialized player and camera.");
            nativeMap14World = OfficialMap14NativeWorldView.Create(mapRoot, worldCamera, new MainlineNativeGroundState(this));
            nativeMap14RefreshAt = Time.unscaledTime + 0.25f;
        }

        private void RefreshNativeMap14State()
        {
            if (nativeMap14World == null || Time.unscaledTime < nativeMap14RefreshAt) return;
            nativeMap14RefreshAt = Time.unscaledTime + 0.25f;
            nativeMap14World.RefreshState();
        }

        private void DisposeNativeMap14()
        {
            nativeMap14World?.Dispose();
            nativeMap14World = null;
        }
    }
}
