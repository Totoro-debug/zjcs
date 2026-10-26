using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    public abstract class WorldTabTransition : MonoBehaviour
    {
        public abstract void PlayEffect(bool value, bool instant = false);
    }
}
