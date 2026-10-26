using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfflineMainlineProgressSession
    {
        public ProgressionResult RecordGoddessActivated(int mapClassId, int region, string eventId)
        {
            if (!HasMapRegion(Save, mapClassId, region) || string.IsNullOrWhiteSpace(eventId))
                return Result(ProgressionResultStatus.InvalidState, message: "A real activated map region and receipt are required.");
            if (FindRegion(Save, mapClassId, region).Progress < 10000)
                return Result(ProgressionResultStatus.InvalidState,
                    message: "A goddess can only be activated after its map region reaches 100% exploration.");
            if (Save.World.AppliedEventIds.Contains(eventId)) return Result(ProgressionResultStatus.Unchanged);
            OfflineMainlineSave next = Clone(Save);
            if (!next.World.ActivatedGoddessRegions.Any(value => value.MapClassId == mapClassId && value.Region == region))
                next.World.ActivatedGoddessRegions.Add(new OfflineMapRegionKey { MapClassId = mapClassId, Region = region });
            next.World.AppliedEventIds.Add(eventId);
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        public int ActiveWitchCount => Save.World.ActivatedWitches.Count;

        public ProgressionResult RecordWitchActivated(int mapClassId, int placementId, string eventId)
        {
            if (placementId <= 0 || !Save.World.Maps.Any(value => value.MapClassId == mapClassId)
                || string.IsNullOrWhiteSpace(eventId) || eventId.Length > 256)
                return Result(ProgressionResultStatus.InvalidState, message: "An initialized map, witch placement and receipt are required.");
            if (Save.World.AppliedEventIds.Contains(eventId)
                || Save.World.ActivatedWitches.Any(value => value.MapClassId == mapClassId && value.PlacementId == placementId))
                return Result(ProgressionResultStatus.Unchanged);
            OfflineMainlineSave next = Clone(Save);
            next.World.ActivatedWitches.Add(new OfflineMapEntityKey { MapClassId = mapClassId, PlacementId = placementId });
            next.World.AppliedEventIds.Add(eventId);
            return Commit(next, Result(ProgressionResultStatus.Applied));
        }

        public bool HasVisitedStatueOn(string localDate) =>
            !string.IsNullOrWhiteSpace(localDate) && Save.Player.StatueVisitDate == localDate;

        public ProgressionResult RecordStatueVisit(string localDate, string kind, int mapClassId,
            int placementId, string eventId)
        {
            if (!DateTime.TryParseExact(localDate, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _) || string.IsNullOrWhiteSpace(kind) || kind.Length > 64
                || mapClassId <= 0 || placementId <= 0 || string.IsNullOrWhiteSpace(eventId)
                || eventId.Length > 160)
                return Result(ProgressionResultStatus.InvalidState, message: "A statue visit date, type and receipt are required.");
            Reload();
            if (HasVisitedStatueOn(localDate))
                return Result(ProgressionResultStatus.NotReady, message: "A statue has already been visited today.");
            return ApplyInventoryTransaction(eventId, save =>
            {
                save.Player.StatueVisitDate = localDate;
                save.Player.StatueVisitKind = kind;
                save.Player.StatueVisitMapClassId = mapClassId;
                save.Player.StatueVisitPlacementId = placementId;
            });
        }

        public ProgressionResult ApplyWitchActivation(int mapClassId, int placementId, string eventId,
            float hpPercentChange, IReadOnlyList<int> statusEffectClassIds,
            IReadOnlyList<OfflineItemStack> awards, long acquiredAtUnixSeconds)
        {
            if (placementId <= 0 || !Save.World.Maps.Any(value => value.MapClassId == mapClassId)
                || string.IsNullOrWhiteSpace(eventId) || eventId.Length > 160
                || float.IsNaN(hpPercentChange) || float.IsInfinity(hpPercentChange)
                || hpPercentChange < -1f || hpPercentChange > 1f || acquiredAtUnixSeconds < 0
                || statusEffectClassIds == null || awards == null)
                return Result(ProgressionResultStatus.InvalidState, message: "Witch activation effects are malformed.");
            Reload();
            if (Save.Player.AppliedInventoryEventIds.Contains(eventId)
                || Save.World.AppliedEventIds.Contains(eventId)
                || Save.World.ActivatedWitches.Any(value => value.MapClassId == mapClassId
                    && value.PlacementId == placementId))
                return Result(ProgressionResultStatus.Unchanged);
            if (statusEffectClassIds.Any(value => value <= 0)
                || statusEffectClassIds.Distinct().Count() != statusEffectClassIds.Count
                || awards.Any(value => value == null || value.ItemId <= 0 || value.Count <= 0))
                return Result(ProgressionResultStatus.InvalidState, message: "Witch activation rewards are malformed.");

            return ApplyInventoryTransaction(eventId, save =>
            {
                save.World.ActivatedWitches.Add(new OfflineMapEntityKey
                    { MapClassId = mapClassId, PlacementId = placementId });
                save.World.AppliedEventIds.Add(eventId);
                if (hpPercentChange != 0f || statusEffectClassIds.Count > 0)
                {
                    save.Player.PendingStatueBattleEffects.Add(new OfflineStatueBattleEffect
                    {
                        EventId = eventId,
                        HpPercentChange = hpPercentChange,
                        StatusEffectClassIds = statusEffectClassIds.ToList()
                    });
                }
                foreach (OfflineItemStack award in awards)
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player,
                        award.ItemId, award.Count, acquiredAtUnixSeconds);
                    if (!granted.IsApplied) throw new InvalidOperationException("Witch reward item could not be granted: " + award.ItemId);
                }
            });
        }

        /// <summary>One shared hero/witch daily visit, rewards and next-battle effects in one save transaction.</summary>
        public ProgressionResult ApplyStatueVisit(string localDate, string kind, int mapClassId,
            int placementId, string eventId, float hpPercentChange, IReadOnlyList<int> statusEffectClassIds,
            IReadOnlyList<OfflineItemStack> awards, long acquiredAtUnixSeconds)
        {
            if (!DateTime.TryParseExact(localDate, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _) || string.IsNullOrWhiteSpace(kind) || kind.Length > 64
                || mapClassId <= 0 || placementId <= 0 || string.IsNullOrWhiteSpace(eventId)
                || eventId.Length > 160 || float.IsNaN(hpPercentChange) || float.IsInfinity(hpPercentChange)
                || hpPercentChange < -1f || hpPercentChange > 1f || acquiredAtUnixSeconds < 0
                || statusEffectClassIds == null || awards == null
                || statusEffectClassIds.Any(value => value <= 0)
                || statusEffectClassIds.Distinct().Count() != statusEffectClassIds.Count
                || awards.Any(value => value == null || value.ItemId <= 0 || value.Count <= 0))
                return Result(ProgressionResultStatus.InvalidState, message: "Statue visit effects are malformed.");
            Reload();
            if (Save.Player.AppliedInventoryEventIds.Contains(eventId))
                return Result(ProgressionResultStatus.Unchanged);
            if (HasVisitedStatueOn(localDate))
                return Result(ProgressionResultStatus.NotReady, message: "A hero or witch statue has already been visited today.");
            return ApplyInventoryTransaction(eventId, save =>
            {
                save.Player.StatueVisitDate = localDate;
                save.Player.StatueVisitKind = kind;
                save.Player.StatueVisitMapClassId = mapClassId;
                save.Player.StatueVisitPlacementId = placementId;
                if (hpPercentChange != 0f || statusEffectClassIds.Count > 0)
                    save.Player.PendingStatueBattleEffects.Add(new OfflineStatueBattleEffect
                    {
                        EventId = eventId, HpPercentChange = hpPercentChange,
                        StatusEffectClassIds = statusEffectClassIds.ToList()
                    });
                foreach (OfflineItemStack award in awards)
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player,
                        award.ItemId, award.Count, acquiredAtUnixSeconds);
                    if (!granted.IsApplied)
                        throw new InvalidOperationException("Statue reward item could not be granted: " + award.ItemId);
                }
            });
        }
    }
}
