using Zjcs.Official.TreasureSlots;
using Zjcs.Official.TreasureUI;

namespace Zjcs.Official.TreasureSlotsUI
{
    public interface ITreasurePageServices
    {
        ITreasureIconServices Icons { get; }
        PlayerTreasureElementSlotsDataWrap GetTreasureElementSlotData(ElementType type);
    }
}
