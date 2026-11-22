namespace Zjcs.Combat.Official
{
    // Per-target counts stay separate from the authored scope and its effect timing.
    public static class FightSkillHitPolicy
    {
        public static bool OncePerTarget(FightBattleSkillDefinition skill, FightBattleHitDefinition hit)
            => skill != null && hit != null && (skill.ItemId == 10047 && hit.EntityId == 10881
                || skill.ItemId == 10543 && hit.EntityId == 11151);
    }
}
