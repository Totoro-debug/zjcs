using System;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.TreasureUI
{
    // The original shared red-dot lifecycle; only its scene/player dependencies change.
    public class TreasurePlayerRedDotItemBase : ProfessionRedDotItemBase
    {
        protected ITreasureIconServices Services { get; private set; }
        public void ConfigureTreasure(ITreasureIconServices services)
            => Services=services??throw new ArgumentNullException(nameof(services));
        protected override bool PlayerAvailable => Services.PlayerAvailable;
        protected override bool PlayerManagerAvailable => Services.ManagerAvailable;
        protected override SafeAction PlayerUpdateEvent => Services.PlayerUpdated;
        protected override IProfessionTipDotSystem TipDotSystem => Services.TipDotSystem;
    }
}
