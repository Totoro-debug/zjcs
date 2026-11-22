namespace Zjcs.Combat.Official
{
    public static partial class FightStatusTriggerRuntime
    {
        // Confirmed offline rule: an applied creator-sourced effect keeps
        // settling from its retained creator after death or departure.
        // Missing creators and owner-sourced effects do not receive this exception.
        private static bool CanUseRetainedCreator(int sourceType, FightBattleUnitState creator)
        {
            return sourceType == 3 && creator != null && (creator.Hp <= 0 || creator.Ended);
        }
    }
}
