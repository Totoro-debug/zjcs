using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    /// <summary>GoddessDialogIcon.UpdateState and its original StateSwitcher object lists.</summary>
    public sealed class GoddessDialogIcon : MonoBehaviour
    {
        public GoddessTouchDialog.ObjectState[] mActivedStateSwitcher;

        public void UpdateState(bool currentState) =>
            GoddessTouchDialog.SetState(mActivedStateSwitcher, currentState ? 1 : 0);
    }
}
