using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Applies official <c>fight_stage_status</c> rows: the stage-wide statuses, per character type
    /// and profession statuses (and per target monster id) at battle start, plus the global-round
    /// statuses on the configured global-round cadence. The battlefield has no separate stage entity
    /// in this offline model, so stage-wide entries are applied to every unit.
    /// </summary>
    public static class FightStageStatusRuntime
    {
        public static void ApplyStart(FightBattleStageStatusConfig config,
            FightOfficialCombatCatalog statuses, IReadOnlyList<FightBattleUnitState> units, int round,
            FightRandomStream random)
        {
            if (config == null || statuses == null || units == null) return;
            ApplyToAll(config.Status, statuses, units, round, random);
            foreach (FightBattleStatusGroup group in config.CharacterStatus)
            {
                if (group == null) continue;
                foreach (FightBattleUnitState unit in units)
                    if (unit != null && unit.CharacterType == group.Key)
                        ApplyStatuses(unit, group.Status, statuses, round, random);
            }
            foreach (FightBattleStatusGroup group in config.ProfessionStatus)
            {
                if (group == null) continue;
                foreach (FightBattleUnitState unit in units)
                    if (unit != null && unit.Profession == group.Key)
                        ApplyStatuses(unit, group.Status, statuses, round, random);
            }
            if (config.TargetIdStatus.Count > 0)
                foreach (FightBattleUnitState unit in units)
                {
                    if (unit == null || unit.MonsterId <= 0) continue;
                    if (config.TargetIdStatus.Contains(unit.MonsterId))
                        ApplyStatuses(unit, config.TargetIdStatus, statuses, round, random);
                }
        }

        /// <summary>
        /// Global-round statuses are released on global round boundaries spaced by
        /// <see cref="FightBattleStageStatusConfig.GlobalRoundStatusDelay"/>; a non-positive delay
        /// means the entries were already applied at battle start.
        /// </summary>
        public static void ApplyGlobalRound(FightBattleStageStatusConfig config,
            FightOfficialCombatCatalog statuses, IReadOnlyList<FightBattleUnitState> units,
            int globalRound, FightRandomStream random)
        {
            if (config == null || statuses == null || units == null || globalRound <= 0) return;
            if (config.GlobalRoundStatusDelay <= 0f) return;
            if (globalRound % Math.Max(1, (int)Math.Round(config.GlobalRoundStatusDelay)) != 0) return;
            ApplyToAll(config.GlobalRoundStatus, statuses, units, globalRound, random);
            foreach (FightBattleStatusGroup group in config.FactionGlobalRoundStatus)
            {
                if (group == null) continue;
                foreach (FightBattleUnitState unit in units)
                    if (unit != null && unit.Side == group.Key + 1)
                        ApplyStatuses(unit, group.Status, statuses, globalRound, random);
            }
        }

        /// <summary>Visible status ids for the stage status panel (union minus the blacklist).</summary>
        public static List<int> ViewStatusIds(FightBattleStageStatusConfig config)
        {
            var result = new List<int>();
            if (config == null) return result;
            var seen = new HashSet<int>();
            void Add(IEnumerable<int> values)
            {
                foreach (int value in values) if (seen.Add(value)) result.Add(value);
            }
            Add(config.Status);
            foreach (FightBattleStatusGroup group in config.CharacterStatus) Add(group.Status);
            Add(config.GlobalRoundStatus);
            foreach (FightBattleStatusGroup group in config.FactionGlobalRoundStatus) Add(group.Status);
            Add(config.TargetIdStatus);
            foreach (int ignored in config.ViewStatusBlackList) seen.Remove(ignored);
            result.RemoveAll(value => !seen.Contains(value));
            return result;
        }

        private static void ApplyToAll(List<int> classIds, FightOfficialCombatCatalog statuses,
            IReadOnlyList<FightBattleUnitState> units, int round, FightRandomStream random)
        {
            if (classIds == null || classIds.Count == 0) return;
            foreach (FightBattleUnitState unit in units)
                ApplyStatuses(unit, classIds, statuses, round, random);
        }

        private static void ApplyStatuses(FightBattleUnitState unit, List<int> classIds,
            FightOfficialCombatCatalog statuses, int round, FightRandomStream random)
        {
            if (unit == null || classIds == null) return;
            foreach (int classId in classIds)
            {
                if (classId <= 0) continue;
                int instanceId = 1;
                foreach (FightBattleStatusState existing in unit.Statuses)
                    if (existing != null && existing.InstanceId >= instanceId)
                        instanceId = existing.InstanceId + 1;
                FightBattleStatusState template = statuses.CreateStatus(classId, unit.Id, unit.Id,
                    instanceId);
                FightStatusLifecycle.Apply(unit, template, round, instanceId, random);
            }
        }
    }
}
