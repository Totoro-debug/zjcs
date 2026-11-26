using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.GachaUI
{
    public class GachaBtnContent : MonoBehaviour
    {
        public enum BtnMode { TenMode, HundredMode }
        [SerializeField] private WorldStateSwitcher<BtnMode> mBtnStateSwicher;
        public void SetState(BtnMode btnMode) => mBtnStateSwicher.SetState(btnMode);
    }
}
