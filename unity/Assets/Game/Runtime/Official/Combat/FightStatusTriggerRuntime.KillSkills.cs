using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static partial class FightStatusTriggerRuntime
    {
        public static int ApplyKillSkills(FightBattleUnitState killer,
            FightBattleUnitState defeated, FightBattleSkillDefinition sourceSkill,
            IReadOnlyList<FightBattleUnitState> units, FightOfficialCombatCatalog catalog,
            FightRandomStream random,
            List<FightBattleStatusSkillInvocation> configuredSkillInvocations = null,
            FightKillDamageContext damage = null)
        {
            if (killer == null || defeated == null || killer.Statuses == null || catalog == null
                || !FightBattleTargeting.IsSelectable(killer) || defeated.Hp > 0
                || damage != null && !damage.KilledTarget) return 0;
            var statuses = new List<FightBattleStatusState>(killer.Statuses);
            int applied = 0;
            foreach (FightBattleStatusState status in statuses)
            {
                if (status == null || status.ForbiddenType != 0
                    || !FightSkillTriggerPolicy.AllowsStatus(sourceSkill, status)
                    || !status.HasKillSkill || !status.KillSkillRulesSupported
                    || status.KillSkillMaxCount >= 0
                        && status.KillSkillTriggeredCount >= status.KillSkillMaxCount
                    || status.KillSkillFilterSelf && defeated.Id == killer.Id
                    || status.KillSkillFilterClassIds.Count > 0
                        && MatchesKillClass(defeated, status.KillSkillFilterClassIds)
                    || (status.KillSkillCustomOverflowDamage || status.KillSkillUseCustomDamage) && damage == null
                    || !Roll(status.KillSkillRate, random)) continue;
                bool fired = false;
                if (status.KillSkillStatusEntityId > 0)
                {
                    int instanceId = FightStatusLifecycle.NextInstanceId(killer);
                    FightBattleStatusState template = catalog.CreateStatus(
                        status.KillSkillStatusEntityId, killer.Id, status.CreatorId,
                        instanceId, status.RootSkillId, status.ApplyingSkillItemId,
                        status.SourceSkillRank, status.SourceSkillLevel, status.SourceSubRank,
                        status.SourceProperties, 0, status.ReplacementProperties,
                        status.BlessPropertyScales);
                    template.ReportSource = FightBattleStatusReportSource.Resolve(status).Copy();
                    template.Origin = FightBattleStatusOrigin.Resolve(status).Copy();
                    FightStatusLifecycle.Apply(killer, template, -1, instanceId, random);
                    fired = true;
                }
                else if (status.KillSkillEntityId > 0 && configuredSkillInvocations != null)
                {
                    if (status.KillSkillTargetDiePos && damage != null && !damage.HasDamagePosition)
                        throw new System.InvalidOperationException("A death-position skill requires the captured damage cell.");
                    int targetId = status.KillSkillTargetDiePos ? defeated.Id : killer.Id;
                    bool custom = status.KillSkillCustomOverflowDamage || status.KillSkillUseCustomDamage;
                    configuredSkillInvocations.Add(new FightBattleStatusSkillInvocation
                    {
                        Trigger = new FightBattleStatusStateTrigger
                        {
                            EntityId = status.KillSkillEntityId,
                            Origin = FightBattleStatusOrigin.Resolve(status).Copy(),
                            SourceSkillItemId = FightBattleStatusReportSource.Resolve(status).SkillItemId,
                            SourceSkillRank = Math.Max(1, status.SourceSkillRank),
                            SourceSkillLevel = Math.Max(1, status.SourceSkillLevel),
                            SourceSubRank = Math.Max(0, status.SourceSubRank),
                            BlessPropertyScales = FightStatusPropertyModifiers.Copy(
                                status.BlessPropertyScales)
                        },
                        SourceUnitId = killer.Id,
                        TargetUnitId = targetId,
                        TargetPositionUnitId = targetId,
                        AllowDeadTarget = status.KillSkillTargetDiePos,
                        Delay = damage?.Delay ?? 0f,
                        HasCustomDamage = custom,
                        CustomDamage = custom ? damage.Resolve(status, defeated) : 0L,
                        HitOrigin = status.KillSkillTargetDiePos && damage != null
                            ? damage.DamagePosition : (FightGridPoint?)null,
                        HitFacing = damage?.FacingIndex ?? killer.Direction,
                        HitFlipX = damage?.FlipX ?? killer.FlipX
                    });
                    fired = true;
                }
                if (fired)
                {
                    status.KillSkillTriggeredCount++;
                    applied++;
                }
            }
            return applied;
        }

        private static bool MatchesKillClass(FightBattleUnitState unit, List<int> classIds)
        {
            if (unit == null || classIds == null) return false;
            foreach (int classId in classIds)
                if (classId > 0 && (unit.MonsterId == classId
                    || unit.MonsterTemplateId == classId || unit.NpcClassId == classId))
                    return true;
            return false;
        }

    }
}
