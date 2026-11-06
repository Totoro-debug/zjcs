using UnityEngine;

namespace Zjcs.Official.BattleResultUI
{
    // Selected client EndPanelAnimEventReceiver; namespace adapted only.
    public class EndPanelAnimEventReceiver : MonoBehaviour
    {
        [SerializeField] private GameEndPanelBase mGameEndPanelBase;
        public void OnAnimFinished()
        {
            if (mGameEndPanelBase != null) mGameEndPanelBase.OnAnimFinished();
        }
    }
}
