using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    public static class OfficialBattleReportPlayerRowBinder
    {
        public static List<WorldBattleReportPlayerRowData> PlayerRows(FightBattleReportPlayerGroups groups,
            FightBattleReportScoreSettings settings)
        {
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            // A partial group must not become a seemingly complete team denominator.
            if (groups.Unassigned.Count != 0) throw new InvalidOperationException("Player report contains unresolved ownership records.");
            var rows = new List<WorldBattleReportPlayerRowData>();
            foreach (var player in groups.Players)
                rows.Add(new WorldBattleReportPlayerRowData
                {
                    TargetId = player.TargetId,
                    NormalReport = Sum(player.Normal, settings),
                    SummonNormalReport = Sum(player.Summon, settings),
                    PetNormalReport = Sum(player.Pet, settings)
                });
            return rows;
        }

        private static WorldBattleReportPlayerRoleData Sum(IReadOnlyList<FightBattleReportUnit> units,
            FightBattleReportScoreSettings settings)
        {
            var sum = new WorldBattleReportPlayerRoleData();
            foreach (var unit in units)
            {
                var role = Role(unit, settings);
                checked
                {
                    sum.Damage += role.Damage;
                    sum.BeAbsoluteDamage += role.BeAbsoluteDamage;
                    sum.RealCure += role.RealCure;
                    sum.ApplyStatusScore += role.ApplyStatusScore;
                }
            }
            return sum;
        }

        // One resolved unit supplies one role record. Player/pet/summon ownership grouping is a
        // separate provider and must not silently merge every allied unit into the main player.
        public static WorldBattleReportPlayerRoleData Role(FightBattleReportUnit unit,
            FightBattleReportScoreSettings settings)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return new WorldBattleReportPlayerRoleData
            {
                Damage = unit.DamageDealt,
                BeAbsoluteDamage = unit.HasAbsoluteDamageTaken ? unit.AbsoluteDamageTaken : (long?)null,
                RealCure = unit.HealDealt,
                ApplyStatusScore = settings.TryGetApplyStatusScore(unit, out long score) ? score : (long?)null
            };
        }

        // Original FightEndReportPlayerTabView sums all supplied player records before creating
        // rows. An unavailable record leaves that team ratio unavailable for every affected row.
        public static void InitRows(IReadOnlyList<WorldBattleReportPlayerRow> rows,
            IReadOnlyList<WorldBattleReportPlayerRowData> data, long currentPlayerId,
            Action<UnityEngine.Component, long> iconProvider = null)
        {
            WorldBattleReportPlayerView.InitRows(rows, data, currentPlayerId, iconProvider);
        }
    }
}
