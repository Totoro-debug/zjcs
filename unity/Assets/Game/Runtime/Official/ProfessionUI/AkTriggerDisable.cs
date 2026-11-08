using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // APK AOT AkTriggerDisable.OnDisable at RVA 0x79EF480 invokes
    // AkTriggerBase.triggerDelegate with null (ARM64 mov x1, xzr).
    public sealed class AkTriggerDisable : MonoBehaviour
    {
        public Action<GameObject> triggerDelegate;
        private void OnDisable() => triggerDelegate?.Invoke(null);
    }
}
