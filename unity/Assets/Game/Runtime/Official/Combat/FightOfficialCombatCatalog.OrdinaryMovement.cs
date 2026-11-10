using System;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        // Empty hit graphs are valid only through this checked movement capability.
        public FightBattleSkillDefinition CreateOrdinaryMovementSkill(int entityId)
        {
            if (!FightOrdinaryMovementTiming.Supports(entityId))
                throw new ArgumentException("Unknown ordinary movement skill.", nameof(entityId));
            var skill = CreateSkillCore(entityId, -1, 0, 1, 1, 0, null, true);
            if (!FightOrdinaryMovementTiming.Supports(skill))
                throw new InvalidOperationException("Review changed ordinary movement configuration: " + entityId);
            return skill;
        }
    }
}
