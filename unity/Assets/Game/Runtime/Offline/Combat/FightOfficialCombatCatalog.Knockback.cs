using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        internal bool UsesStandardKnockbackCollision(int actionEntityId)
        {
            if (!entities.TryGetValue(actionEntityId, out Dictionary<string, object> entity)) return false;
            var action = Component(entity, "ActionComponent");
            var move = Component(entity, "FightActionSkillMoveComponent");
            // Original knockback action and its authored shared collision skill.
            // Read the immutable action definition so old checkpoints also recover it.
            return action != null && move != null && Int(action, "ActionType") == 7
                && Int(move, "BlockedSkillId") == 10100;
        }
    }
}
