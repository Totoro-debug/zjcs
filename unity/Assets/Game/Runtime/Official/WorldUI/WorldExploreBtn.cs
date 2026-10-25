using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    /// <summary>WorldExploreBtn.Start's original serialized one/multiple icon switch.</summary>
    public sealed class WorldExploreBtn : MonoBehaviour
    {
        public bool mIsExploreOneTimes;
        public GoddessTouchDialog.ObjectState[] IsOneTimesStateSwitcher;
        private void Start() => GoddessTouchDialog.SetState(IsOneTimesStateSwitcher, mIsExploreOneTimes ? 1 : 0);
    }
}
