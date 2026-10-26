using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfflineMainlineProgressSession
    {
        public ProgressionResult ApplyHeroStatueActivation(int mapClassId, int placementId, string eventId,
            IReadOnlyList<OfflineExtraProperty> properties, float hpPercentChange)
        {
            if (mapClassId <= 0 || placementId <= 0 || string.IsNullOrWhiteSpace(eventId) || eventId.Length > 160
                || properties == null || properties.Count == 0 || properties.Any(value => value == null
                    || value.PropId <= 0 || value.Level < 0 || value.PropType <= 0 || value.Count != 1)
                || float.IsNaN(hpPercentChange) || float.IsInfinity(hpPercentChange)
                || hpPercentChange < -1 || hpPercentChange > 1)
                return Result(ProgressionResultStatus.InvalidState, message: "Hero statue effects are malformed.");
            Reload();
            if (!Save.World.Maps.Any(value => value.MapClassId == mapClassId))
                return Result(ProgressionResultStatus.InvalidState, message: "The statue map has not been initialized.");
            // The receipt and permanent property source survive a crash before the
            // presentation's private EC state is saved. Re-entry never grants twice.
            if (Save.Player.AppliedInventoryEventIds.Contains(eventId)) return Result(ProgressionResultStatus.Unchanged);
            return ApplyInventoryTransaction(eventId, save =>
            {
                foreach (OfflineExtraProperty property in properties)
                    save.Player.CharacterProperties.ExtraProperties.Add(new OfflineExtraProperty
                    {
                        SourceId = eventId, PropId = property.PropId, Level = property.Level,
                        PropType = property.PropType, Count = 1
                    });
                if (hpPercentChange != 0)
                    save.Player.PendingStatueBattleEffects.Add(new OfflineStatueBattleEffect
                    {
                        EventId = eventId, HpPercentChange = hpPercentChange,
                        StatusEffectClassIds = new List<int>()
                    });
            });
        }
    }
}
