using System;
using System.Linq;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureCultivation;

namespace Zjcs.Offline.Progression
{
    // Read-only window projection. The enclosing owner supplies the original task/count source.
    public sealed class OfflineTreasurePropertyPlayer:ITreasurePropertyPlayer
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflineTreasureSlotService slots;
        private readonly Func<Zjcs.Official.TreasureProperties.TreasureInfo,long?> currentCount;
        public OfflineTreasurePropertyPlayer(OfflineMainlineProgressSession session,OfflineTreasureSlotService slots,
            Func<Zjcs.Official.TreasureProperties.TreasureInfo,long?> currentCount)
        {
            this.session=session??throw new ArgumentNullException(nameof(session));this.slots=slots??throw new ArgumentNullException(nameof(slots));
            this.currentCount=currentCount??throw new ArgumentNullException(nameof(currentCount));
        }
        public int SubRank=>session.Save.Player.SubRank;
        public TreasureState GetTreasureState(int id)
        {
            // Original GetItemById returns the first existing stack independently of Count.
            if(!session.Save.Player.Inventory.Any(row=>row.ItemId==id))return null;
            var value=session.Save.Player.CharacterProperties.Treasures.FirstOrDefault(row=>row.ClassId==id);
            return value==null?null:new TreasureState{Active=value.Active,Star=value.Star,HitCount=value.HitCount};
        }
        public bool IsTreasureInSlot(int id)=>slots.Snapshot().IsTreasureInSlot(id);
        public int GetTreasureFinalLevel(int id)=>slots.Snapshot().GetTreasureFinalLevel(id);
        public long? GetCurrentCount(Zjcs.Official.TreasureProperties.TreasureInfo info)=>currentCount(info);
    }
}
