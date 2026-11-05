using System;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattlePlayerAppearanceSource
    {
        public int UnitId { get; }
        public long PlayerId { get; }
        public Func<OfflinePlayerProgress> ReadPlayer { get; }
        public Func<int, int?> ItemType { get; }

        public OfficialBattlePlayerAppearanceSource(int unitId, long playerId,
            Func<OfflinePlayerProgress> readPlayer, Func<int, int?> itemType)
        {
            if (unitId <= 0 || playerId <= 0) throw new ArgumentOutOfRangeException(nameof(unitId));
            UnitId = unitId; PlayerId = playerId;
            ReadPlayer = readPlayer ?? throw new ArgumentNullException(nameof(readPlayer));
            ItemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
        }

        public bool Matches(FightBattleUnitState state) => state != null && state.Id == UnitId
            && state.CharacterType == 1 && (state.RoleEntityId == 0 || state.RoleEntityId == 3100);
    }
}
