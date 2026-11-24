using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static class FightSkillJumpPolicy
    {
        // The client receives server paths. Keep confirmed offline path rules here;
        // source actions still supply their original delay, duration and pass height.
        internal static bool LandsBesideTarget(FightBattleSkillDefinition skill)
            => skill != null && skill.EntityId == 12100 && skill.ItemId == 10101;

        internal static bool UsesDirectPath(FightBattleSkillDefinition skill)
            => skill != null && (skill.MoveByTeleport
                || skill.EntityId == 10670 && skill.ItemId == 10015 || LandsBesideTarget(skill));

        internal static void RestoreTargetRange(FightBattleSkillDefinition skill)
        {
            if (!LandsBesideTarget(skill)) return;
            skill.Range = new List<FightGridPoint>(ConfirmedTargetRange(skill.ItemId));
        }

        public static IReadOnlyList<FightGridPoint> ConfirmedTargetRange(int skillItemId)
        {
            if (skillItemId != 10101) return null;
            // User-confirmed: choose an enemy within two cells, then an empty cell
            // beside that enemy; the serialized server range is not the local selector.
            var range = new List<FightGridPoint>();
            for (int distance = 1; distance <= 2; distance++)
                for (int x = -distance; x <= distance; x++)
                {
                    int y = distance - Math.Abs(x);
                    range.Add(new FightGridPoint(x, y));
                    if (y != 0) range.Add(new FightGridPoint(x, -y));
                }
            return range;
        }
    }
}
