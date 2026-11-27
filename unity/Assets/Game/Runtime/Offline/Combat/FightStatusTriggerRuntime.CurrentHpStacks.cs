using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static partial class FightStatusTriggerRuntime
    {
        // Shared current-HP layer rule for 10547/10582/10589. The original HP-unit components
        // supply the percentage and target entities, but contain no server execution body.
        private static bool IsCurrentHpStackSource(FightBattleStatusState source) =>
            source != null && (source.ClassId == 25034 || source.ClassId == 25063 || source.ClassId == 25094);

        internal static void RecalculateCurrentHpStackSources(FightBattleUnitState owner,
            FightOfficialCombatCatalog catalog, FightRandomStream random)
        {
            foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
                if (IsCurrentHpStackSource(status) && owner.Statuses.Contains(status))
                    RecalculateCurrentHpStacks(owner, status, catalog, false, random);
        }

        private static int RecalculateCurrentHpStacks(FightBattleUnitState owner,
            FightBattleStatusState source, FightOfficialCombatCatalog catalog, bool atStart,
            FightRandomStream random)
        {
            if (owner.Hp <= 0 || source.ForbiddenType != 0 || source.HpTriggerPer <= 0f
                || float.IsNaN(source.HpTriggerPer) || float.IsInfinity(source.HpTriggerPer)
                || atStart && !source.HpTriggerAtStart) return 0;
            // Older checkpoints may still contain a healing remainder from the former path.
            if (source.Components?.HpIncreaseUnit != null) source.Components.HpIncreaseUnit.AccumulatedHp = 0;
            if (source.HpTriggerStatuses.Count != 1) return 0;
            int entityId = source.HpTriggerStatuses[0];
            var template = catalog.CreateStatus(entityId, owner.Id, source.CreatorId,
                0, source.RootSkillId, source.ApplyingSkillItemId, source.SourceSkillRank,
                source.SourceSkillLevel, source.SourceSubRank, source.SourceProperties, 0,
                source.ReplacementProperties, source.BlessPropertyScales);
            if (!template.IsOpenStack || template.MaxStackedCount <= 0) return 0;
            decimal missing = Math.Max(0m, (decimal)owner.MaxHp - owner.Hp);
            decimal unit = owner.MaxHp * (decimal)source.HpTriggerPer;
            // Decimal conversion preserves the configured 15%, including exact tier boundaries.
            int layers = (int)Math.Min((decimal)template.MaxStackedCount, decimal.Floor(missing / unit));
            FightBattleStatusState current = null;
            int instanceId = 1;
            foreach (var status in owner.Statuses)
            {
                if (status == null) continue;
                instanceId = Math.Max(instanceId, status.InstanceId + 1);
                if (status.ClassId == entityId && FightBattleStatusOrigin.Matches(
                    FightBattleStatusOrigin.Resolve(status), FightBattleStatusOrigin.Resolve(source)))
                    current = status;
            }
            if (current != null)
            {
                if (current.StackCount == layers) return 0;
                if (layers == 0) FightStatusLifecycle.End(owner, current);
                else FightStatusLifecycle.AddStack(owner, current, layers - current.StackCount);
                return 1;
            }
            if (layers == 0) return 0;
            template.InstanceId = instanceId;
            template.ReportSource = FightBattleStatusReportSource.Resolve(source).Copy();
            template.Origin = FightBattleStatusOrigin.Resolve(source).Copy();
            template.StackCount = Math.Min(layers, template.MaxStackedCount);
            return FightStatusLifecycle.Apply(owner, template, -1, instanceId, random) != null ? 1 : 0;
        }
    }
}
