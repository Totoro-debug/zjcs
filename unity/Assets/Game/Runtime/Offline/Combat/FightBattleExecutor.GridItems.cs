using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private List<FightBattleUnitState> StatusOwners(bool includeEnded = false)
        {
            var result = new List<FightBattleUnitState>(units);
            foreach (var item in checkpoint.GridItems)
                if (item != null && item.HasRuntime && item.Runtime != null
                    && (includeEnded || !item.Ended && !item.Runtime.Ended))
                    result.Add(item.Runtime);
            return result;
        }

        private static bool IsGridSkillSource(FightBattleUnitState unit)
        {
            return unit != null && unit.CharacterType == 6 && !unit.Ended;
        }

        private void RestoreGridItems()
        {
            if (checkpoint.GridItems == null) checkpoint.GridItems = new List<FightBattleGridItemState>();
            foreach (var item in checkpoint.GridItems)
            {
                if (item == null || !item.HasRuntime || item.Runtime == null) continue;
                if (item.Runtime.Id != item.Id || item.Runtime.CharacterType != 6)
                    throw new ArgumentException("Invalid grid-item runtime identity.");
                unitById.Add(item.Id, item.Runtime);
            }
            CleanupGridItems();
            if (statusCatalog == null) return;
            foreach (var unit in units)
                foreach (var skill in unit.Skills)
                {
                    if (skill?.HitGraphEntities == null || !skill.HitGraphEntities.Exists(hit =>
                        hit.SummonConfigType == "FightHitSummonGridItemCfg"
                        && (hit.GridSummonCells == null || hit.GridSummonCells.Count == 0))) continue;
                    var original = statusCatalog.CreateSkill(skill.EntityId, skill.Slot, skill.ItemId,
                        skill.Rank, skill.Level, skill.SubRank, skill.Properties);
                    foreach (var hit in skill.HitGraphEntities)
                    {
                        if (hit.SummonConfigType != "FightHitSummonGridItemCfg") continue;
                        var definition = original.HitGraphEntities.Find(value => value.EntityId == hit.EntityId);
                        if (definition == null) continue;
                        hit.SummonGridItemClassId = definition.SummonGridItemClassId;
                        hit.GridItemFindAdjacentIfBlocked = definition.GridItemFindAdjacentIfBlocked;
                        hit.GridSummonCells = definition.GridSummonCells;
                    }
                }
        }

        private void CleanupGridItems()
        {
            var owners = StatusOwners(true);
            foreach (var item in checkpoint.GridItems)
            {
                if (item == null || item.Ended || !item.HasRuntime || item.Runtime == null) continue;
                var owner = item.Runtime;
                foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
                {
                    int clockId = FightStatusLifecycle.ResolveTargetUnitId(status, owners);
                    bool hasClock = unitById.TryGetValue(clockId, out var clock);
                    bool clockDied = hasClock && clock.Hp <= 0 && clock.CharacterType != 6;
                    if (status.RemoveAtRoundTargetDie && clockDied
                        || status.RemoveAtRoundTargetDestroy && hasClock && clock.Ended
                        || status.RemoveAtApplyEnded && owner.Ended)
                        FightStatusLifecycle.End(owner, status);
                }
                if (owner.Ended) FightGridItemRuntime.End(item);
            }
        }

        private float SpawnGridItems(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            FightBattleHitDefinition hit, List<FightBattleUnitState> targets,
            FightBattleUnitState targetPosition)
        {
            if (statusCatalog == null)
                throw new InvalidOperationException("Official combat catalog is required to spawn grid items.");
            var definition = FightOfficialGridItemRules.Get(hit.SummonGridItemClassId);
            if (hit.GridItemFindAdjacentIfBlocked)
                throw new InvalidOperationException("Adjacent grid-item search has not been migrated.");
            var reference = ResolveSummonReference(actor, hit.SummonReferenceType, targets);
            if (reference == null || checkpoint.Grid == null) return 0f;
            var primary = targetPosition ?? (targets.Count > 0 ? targets[0] : null);
            var center = hit.Anchor == 0 && primary != null ? primary : actor;
            float lastCreation = 0f;
            foreach (var cell in hit.GridSummonCells)
            {
                // The original per-cell probability belongs to this cell, not to a monster pool.
                if (cell == null || cell.Rate <= 0f || cell.Rate < 1f && random.Next() >= cell.Rate)
                    continue;
                int dx = actor.Direction == 1 ? -cell.Position.X : cell.Position.X;
                var point = new FightGridPoint(center.X + dx, center.Y + cell.Position.Y);
                int ground = checkpoint.Grid.GroundAt(point);
                if (ground < 0 || ground >= 31 || (definition.MoveMask & (1 << ground)) == 0
                    || !definition.IgnoreGridBlocked && checkpoint.Grid.BlockedPositions != null
                        && checkpoint.Grid.BlockedPositions.Contains(point)) continue;
                // User-confirmed offline override: end every overridden item/status before creating
                // its replacement, regardless of faction. Ordinary actors may share a burning cell.
                foreach (var previous in checkpoint.GridItems)
                    if (previous != null && !previous.Ended && previous.X == point.X && previous.Y == point.Y
                        && Array.IndexOf(definition.OverrideTypes, previous.GridItemClassId) >= 0)
                        FightGridItemRuntime.End(previous);
                var item = FightGridItemRuntime.Create(NextSummonUnitId(), definition.ClassId,
                    actor, reference, point, hit.Delay + cell.Delay);
                checkpoint.GridItems.Add(item);
                lastCreation = Math.Max(lastCreation, item.CreationDelay);
                unitById.Add(item.Id, item.Runtime);
                item.Runtime.StatusStarted = OnStatusStarted;
                item.Runtime.StatusEnded = OnStatusEnded;
                item.Runtime.StatusRoundResolver = StatusClockRound;
                item.Runtime.RoundStartClockResolver = RoundStartClockRound;
                float previousTime = movementStartContext == null ? 0f : movementStartContext.StatusStartTime;
                if (movementStartContext != null) movementStartContext.StatusStartTime = item.CreationDelay;
                foreach (int statusId in hit.SummonStatusIds)
                    ApplySummonStatus(item.Runtime, actor, skill, statusId);
                if (movementStartContext != null) movementStartContext.StatusStartTime = previousTime;
            }
            return lastCreation;
        }
    }
}
