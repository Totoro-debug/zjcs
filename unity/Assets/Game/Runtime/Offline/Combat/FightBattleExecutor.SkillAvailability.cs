namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private bool? ConfiguredCureAvailability(FightBattleUnitState actor,
            FightBattleSkillDefinition skill)
            => FightSkillLogicRuntime.CureAvailability(actor, skill, units, statusCatalog);
    }
}
