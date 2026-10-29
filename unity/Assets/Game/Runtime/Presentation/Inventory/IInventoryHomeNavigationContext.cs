using System;
using System.Collections.Generic;

namespace Zjcs.Presentation.Inventory
{
    public interface IInventoryHomeNavigationContext
    {
        IReadOnlyList<int> TreasureGachaSourceTypes(int itemId);
        bool OpenTreasureGachaSource(int itemId, int gachaType, Action<int> returnToSource);
        void ShowInventoryNotice(string message);
    }
}
