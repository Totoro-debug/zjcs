using System;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // The original TalkDialogContentSwitch / TalkDialogCharacter clip receivers.
    public sealed class WorldDialogueAnimationEvents : MonoBehaviour
    {
        public Action ContentSwitchHalf, PortraitSwitchHalf;
        private void OnTalkDialogContentSwitchHalf() => ContentSwitchHalf?.Invoke();
        private void OnSwitchAnimHalf() => PortraitSwitchHalf?.Invoke();
    }
}
