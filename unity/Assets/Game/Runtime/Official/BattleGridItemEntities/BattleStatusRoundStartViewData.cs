using System;

namespace Zjcs.Official.BattleGridItemEntities
{
    // The actual local component supplies its authored counter and transition event.
    // This client view never consumes a trigger or derives lifetime from rendered frames.
    public abstract class BattleStatusRoundStartViewData
    {
        public event Action<BattleStatusRoundStartViewData> OnRemainCountUpdate;
        public abstract int GetRemainCount();
        public void NotifyRemainCountUpdate() => OnRemainCountUpdate?.Invoke(this);
    }
}
