using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        // DamageDisperseStatusCfg excludes the target character, not the status creator.
        // Read immutable source configuration so legacy checkpoints need no new state.
        internal bool DisperseExcludesCharacter(int damageId, int characterType)
        {
            if (!entities.TryGetValue(damageId, out Dictionary<string, object> components)) return false;
            var damage = Component(components, "FightDamageComponent");
            var disperse = damage == null ? null : Component(damage, "DisperseStatus");
            if (disperse == null) return false;
            if (disperse.ContainsKey("ExcludeCharacterTypes"))
                return System.Array.IndexOf(IntArray(disperse, "ExcludeCharacterTypes"), characterType) >= 0;
            // Selected 884's only nonempty list; historical 871 omitted this field.
            return damageId == 11552 && (characterType == 7 || characterType == 3);
        }
    }
}
