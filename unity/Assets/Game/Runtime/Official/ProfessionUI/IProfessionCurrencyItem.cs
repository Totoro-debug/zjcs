using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // The original TopBarItem consumes only these operations on MePlayerItemUI.
    // A native currency-item binding supplies its original component and method.
    public interface IProfessionCurrencyItem
    {
        Component Component { get; }
        void SetStopUpdateCount(bool isStop);
    }
}
