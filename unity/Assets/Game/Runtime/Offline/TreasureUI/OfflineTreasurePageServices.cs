using System;
using Zjcs.Offline.Progression;
using Zjcs.Official.TreasureSlots;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.TreasureUI;

namespace Zjcs.Offline.TreasureUI
{
    public sealed class OfflineTreasurePageServices : ITreasurePageServices
    {
        private readonly OfflineTreasureSlotService slots;
        public ITreasureIconServices Icons { get; }
        public OfflineTreasurePageServices(OfflineTreasureSlotService slots, ITreasureIconServices icons)
        {
            this.slots=slots??throw new ArgumentNullException(nameof(slots));
            Icons=icons??throw new ArgumentNullException(nameof(icons));
        }
        public PlayerTreasureElementSlotsDataWrap GetTreasureElementSlotData(ElementType type) =>
            slots.Snapshot().GetTreasureElementSlotData(type);
    }
}
