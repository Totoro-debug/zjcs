namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void RestoreConditionalHitDefinition(FightBattleStatusState status)
        {
            var previous=status?.Components?.HitDmgAddPer;
            var apply=status?.Components?.HitApplyDamage;
            bool restoreHit=previous!=null && !previous.DefinitionResolved;
            bool restoreApply=apply!=null && (!apply.DefinitionResolved
                || apply.HasEntityCountScale && apply.EntityCountScale==null);
            bool restoreRound=status!=null && status.RoundEndEntityCount==null
                && statusCatalog?.HasRoundEndEntityCountDefinition(status.ClassId)==true;
            bool restoreBeforeEnd=status!=null && status.Components?.RoundBeforeEnd==null
                && statusCatalog?.HasRoundBeforeEndDefinition(status.ClassId)==true;
            if((!restoreHit && !restoreApply && !restoreRound && !restoreBeforeEnd) || statusCatalog==null) return;
            var template=statusCatalog.CreateStatus(status.ClassId,status.OwnerId,status.CreatorId,
                status.InstanceId,status.RootSkillId,status.ApplyingSkillItemId,status.SourceSkillRank,
                status.SourceSkillLevel,status.SourceSubRank,status.SourceProperties,
                replacementProperties:status.ReplacementProperties,
                blessPropertyScales:status.BlessPropertyScales);
            var components=template.Components;
            if(restoreBeforeEnd)
            {
                if(status.Components==null) status.Components=new FightBattleStatusComponentState();
                status.Components.RoundBeforeEnd=components.RoundBeforeEnd;
            }
            if(restoreRound)
            {
                status.RoundEndEntityCount=template.RoundEndEntityCount;
                status.RoundEndRulesSupported=template.RoundEndRulesSupported;
            }
            if(restoreApply && components.HitApplyDamage!=null)
                status.Components.HitApplyDamage=components.HitApplyDamage;
            var definition=components.HitDmgAddPer;
            if(!restoreHit || definition==null) return;
            definition.InvokeCount=previous.InvokeCount;
            definition.ConditionProgress=previous.ConditionProgress;
            definition.DurationSkillCount=previous.DurationSkillCount;
            definition.LastResetRound=previous.LastResetRound;
            status.Components.HitDmgAddPer=definition;
        }
    }
}
