using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static partial class FightStatusTriggerRuntime
    {
        // User-confirmed server gap for 10531. Existing TryAtRemove listeners retain their path.
        public static List<FightBattleStatusSkillInvocation> CollectRevivalSkills(
            IReadOnlyList<FightBattleUnitState> units, FightRandomStream random,
            FightBattleUnitState listener = null)
        {
            var result = new List<FightBattleStatusSkillInvocation>();
            if (units == null) return result;
            foreach (var owner in units)
            {
                if (owner == null || owner.Ended || owner.Statuses == null
                    || listener != null && owner != listener) continue;
                foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
                {
                    if (status == null || status.ClassId != 25014 || status.ForbiddenType != 0
                        || !status.RoleDieRulesSupported || !owner.Statuses.Contains(status)
                        || status.RoleDieMaxCount >= 0
                            && status.RoleDieTriggeredCount >= status.RoleDieMaxCount) continue;
                    FightBattleUnitState target = null;
                    foreach (var candidate in units)
                    {
                        if (candidate == null || candidate.Ended || candidate.Hp > 0
                            || status.RoleDieSameFaction && candidate.Side != owner.Side
                            || status.RoleDieCharacterTypes.Count > 0
                                && !status.RoleDieCharacterTypes.Contains(candidate.CharacterType)) continue;
                        if (target == null || candidate.EntryCombatRating > target.EntryCombatRating
                            || candidate.EntryCombatRating == target.EntryCombatRating && candidate.Id < target.Id)
                            target = candidate;
                    }
                    if (target == null) continue;
                    status.RoleDieMeetProgress++;
                    if (status.RoleDieMeetProgress < Math.Max(1, status.RoleDieMeetCount)) continue;
                    status.RoleDieMeetProgress = 0;
                    int added = 0;
                    foreach (var trigger in status.RoleDieSkills)
                    {
                        if (trigger == null || trigger.EntityId <= 0 || !Roll(trigger.BasePercent, random)) continue;
                        result.Add(new FightBattleStatusSkillInvocation {
                            Trigger = trigger.WithSource(status), SourceUnitId = owner.Id,
                            TargetUnitId = target.Id, TargetPositionUnitId = target.Id,
                            AllowDeadSource = true, AllowDeadTarget = true,
                            Sequential = true, SourceRootSkillId = status.RootSkillId,
                            DamageFactor = trigger.StackedCountToDamageFactor ? status.StackCount : 1.0
                        });
                        added++;
                    }
                    // Reserve before nested execution, including a dead holder reviving itself.
                    if (added > 0) status.RoleDieTriggeredCount++;
                }
            }
            return result;
        }
    }

    public sealed partial class FightBattleExecutor
    {
        private void RestoreRevivalDefinition(FightBattleStatusState status)
        {
            if (status.ClassId == 24083) status.ForbiddenOnApplicatorDie = true;
            if (status.ClassId != 25014 || status.RoleDieRulesSupported) return;
            var definition = statusCatalog.CreateStatus(status.ClassId, status.OwnerId,
                status.CreatorId, status.InstanceId, status.RootSkillId, status.ApplyingSkillItemId,
                status.SourceSkillRank, status.SourceSkillLevel, status.SourceSubRank,
                status.SourceProperties, replacementProperties: status.ReplacementProperties,
                blessPropertyScales: status.BlessPropertyScales);
            status.RoleDieRulesSupported = definition.RoleDieRulesSupported;
            status.RoleDieSkills = definition.RoleDieSkills;
            status.RoleDieStatuses = definition.RoleDieStatuses;
        }

        private void ExecuteRevivalSkills(List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            ref float delay, ref float duration, ref float actionDuration)
        {
            if (statusCatalog == null) return;
            // Resolve one holder at a time. Later holders must see earlier revivals.
            foreach (var owner in new List<FightBattleUnitState>(units))
            {
                var invocations = FightStatusTriggerRuntime.CollectRevivalSkills(units, random, owner);
                ExecuteConfiguredStatusSkills(invocations, casts, damageEvents, movements,
                    ref delay, out float nestedDuration, out float nestedAction);
                duration = Math.Max(duration, nestedDuration);
                actionDuration = Math.Max(actionDuration, nestedAction);
            }
        }
    }
}
