using UnityEngine;

namespace Zjcs.Offline.Startup
{
    public sealed class OfficialStartupAssets : ScriptableObject
    {
        public GameObject bootSplash, themeCanvas, launchCanvas, ageTipsDialog;
        public string resourceStatus, initializationStatus;
        public const string ResourcePath = "Startup/OfficialStartupAssets";
    }
}
