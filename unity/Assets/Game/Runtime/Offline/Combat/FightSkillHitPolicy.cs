namespace Zjcs.Combat.Official
{
    // Explicit solo decisions stay separate from the unmodified official scope data.
    public static class FightSkillHitPolicy
    {
        public static bool OncePerTarget(FightBattleSkillDefinition skill, FightBattleHitDefinition hit)
            => skill != null && hit != null && skill.ItemId == 10047 && hit.EntityId == 10881;
    }
}
