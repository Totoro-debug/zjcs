using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // Original WindowMaskEffectBase, with the migrated mask type alias only.
    public class WorldWindowMaskEffectBase : MonoBehaviour
    {
        public virtual void ApplyEffect(WorldWindowMask mask) { }
        public virtual void CancelEffect(WorldWindowMask mask) { }
    }
}
