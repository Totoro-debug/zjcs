using System;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // AwardListPanelItem.ReturnPool is invoked by the original 1.6 second clip.
    public sealed class WorldAwardAnimationEvents : MonoBehaviour
    {
        public Action Returned;
        public void ReturnPool() => Returned?.Invoke();
    }
}
