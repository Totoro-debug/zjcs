namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void RestoreStackCountDefinition(FightBattleStatusState status)
        {
            if (statusCatalog == null || status == null || !status.HasStackCountTrigger
                || status.StackCountSkillsResolved) return;
            var definition = statusCatalog.CreateStatus(status.ClassId, status.OwnerId,
                status.CreatorId, status.InstanceId, status.RootSkillId, status.ApplyingSkillItemId,
                status.SourceSkillRank, status.SourceSkillLevel, status.SourceSubRank,
                status.SourceProperties, replacementProperties: status.ReplacementProperties,
                blessPropertyScales: status.BlessPropertyScales);
            status.StackCountSkills = definition.StackCountSkills;
            status.StackCountRulesSupported = definition.StackCountRulesSupported;
            status.StackCountSkillsResolved = true;
            // Retain saved quotas; no missing history is synthesized.
            if (status.StackCountTriggered > 0) status.StackCountStartChecked = true;
        }

        private static bool IsPendingStackCountStart(FightBattleUnitState owner, FightBattleStatusState status)
        {
            return owner != null && !owner.Ended && status != null && status.HasStackCountTrigger
                && status.StackCountRulesSupported && !status.StackCountStartChecked;
        }
    }
}
