namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void RestoreRoundEndDefinition(FightBattleStatusState status)
        {
            if (statusCatalog == null || status == null || !status.HasRoundEndTrigger) return;
            var definition = statusCatalog.CreateStatus(status.ClassId, status.OwnerId,
                status.CreatorId, status.InstanceId, status.RootSkillId, status.ApplyingSkillItemId,
                status.SourceSkillRank, status.SourceSkillLevel, status.SourceSubRank, status.SourceProperties,
                replacementProperties: status.ReplacementProperties, blessPropertyScales: status.BlessPropertyScales);
            // Definitions can gain a newly migrated source/target branch; the saved
            // invocation count remains authoritative across restoration.
            status.RoundEndRulesSupported = definition.RoundEndRulesSupported;
            status.RoundEndSkills = definition.RoundEndSkills;
        }
    }
}
