namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Confirmed offline rule for FightSkillComponentInfo.CanTriggerChild and its status whitelist.
    /// Apply before a responding status changes counters or consumes random values. Directly
    /// configured skill effects do not pass through this gate; events without a skill remain eligible.
    /// </summary>
    public static class FightSkillTriggerPolicy
    {
        public static bool AllowsStatus(FightBattleSkillDefinition skill, FightBattleStatusState status)
        {
            return status != null && (skill == null || skill.CanTriggerChild
                || skill.CanTriggerStatusEntityIds != null
                    && skill.CanTriggerStatusEntityIds.Contains(status.ClassId));
        }
    }
}
