using UnityEngine;

namespace Zjcs.Offline.HomeHudUI
{
    // Attached only to the original HUD root. Hiding or destroying that root
    // stops polling together with its original OnDisable unsubscriptions.
    public sealed class OfflineHomeHudInputLoop : MonoBehaviour
    {
        internal OfflineHomeHudInput Input;

        private void Update()
        {
            Input?.Update();
        }
    }
}
