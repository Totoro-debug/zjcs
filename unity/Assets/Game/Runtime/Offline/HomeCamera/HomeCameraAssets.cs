using System;
using UnityEngine;
using Zjcs.Official.HomeCamera;

namespace Zjcs.Offline.HomeCamera
{
    // Explicit local references to the selected original camera objects. Scene
    // instances and their service dependencies are bound by the home host.
    public sealed class HomeCameraAssets : ScriptableObject
    {
        public GameObject FreeCamera;
        public GameObject MainCamera;
        public GameObject UICamera;
        public CameraControlSettings Settings;
        public Vector3 UIParentPosition;

        public static HomeCameraAssets LoadDefault()
        {
            var assets = Resources.Load<HomeCameraAssets>("Home/HomeCameraAssets");
            if (!assets || !assets.FreeCamera || !assets.MainCamera || !assets.UICamera || !assets.Settings)
                throw new InvalidOperationException("The original home camera assets are not packaged.");
            return assets;
        }
    }
}
