using System;
using UnityEngine;

namespace Zjcs.Offline.PartnersUI
{
    [ExecuteAlways]
    public sealed class OfflineUiBindingLifetime : MonoBehaviour
    {
        internal IDisposable Token;
        private void OnDestroy() { Token?.Dispose(); Token = null; }
    }
}
