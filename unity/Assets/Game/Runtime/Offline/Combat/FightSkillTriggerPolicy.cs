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
            // Confirmed offline conversion HP loss can notify hurt passives before its shield exists.
            return status != null && (skill == null || skill.ShieldConversionCost || skill.CanTriggerChild
                || skill.CanTriggerStatusEntityIds != null
                    && skill.CanTriggerStatusEntityIds.Contains(status.ClassId));
        }
    }
}
