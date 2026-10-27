using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    public static class OfficialBattleReportPlayerRowBinder
    {
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
            if (rows == null || data == null || rows.Count != data.Count) throw new ArgumentException("Player report rows and records differ.");
            long damage = 0, cure = 0;
            long? beDamage = 0, status = 0;
            foreach (var item in data)
            {
                damage += item.Damage;
                beDamage += item.BeAbsoluteDamage;
                cure += item.Cure;
                status += item.ApplyStatusScore;
            }
            for (int i = 0; i < rows.Count; i++)
                rows[i].Init(data[i], currentPlayerId, damage, beDamage, cure, status, iconProvider);
        }
    }
}
