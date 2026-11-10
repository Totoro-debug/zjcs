namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private FightSkillCastModule skillCastModule;
        private FightSkillCastModule SkillCasts => skillCastModule
            ?? (skillCastModule = new FightSkillCastModule(statusCatalog, skillItems));
    }
}
