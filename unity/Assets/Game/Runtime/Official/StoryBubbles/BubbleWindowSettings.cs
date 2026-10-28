using UnityEngine;

namespace Zjcs.Official.StoryBubbles
{
    // Original PopupWindow metadata consumed by the window host, separate from its clip player.
    public sealed class BubbleWindowSettings : MonoBehaviour
    {
        public int mMaskType, mWindowType;
        public bool mFullScreen, mCloseByClickMask, mAutoCloseWhenContainerDisable, mIsDuplicate;
    }
}
