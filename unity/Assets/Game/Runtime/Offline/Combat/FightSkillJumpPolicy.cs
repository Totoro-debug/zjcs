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

        internal static bool UsesStandableLanding(FightBattleSkillDefinition skill, FightOfficialCombatCatalog catalog)
        {
            if (skill != null && skill.EntityId == 10670 && skill.ItemId == 10015) return true;
            if (skill == null || catalog == null || skill.SkillTargetType != 2 || skill.SourceMoves == null
                || skill.SourceMoves.Count != 1) return false;
            var move = skill.SourceMoves[0];
            // A current PosCanStand action with unrestricted pass height chooses an empty
            // landing first; the action retains its authored timing and movement data.
            return move != null && move.AiMustMoveTo && !move.AIIgnore && !move.CanExpandOffset
                && move.TowardsTargetPos && move.Direction == 1 && move.Distance == -1
                && move.OffsetX == 0 && move.OffsetY == 0 && move.MoveBehaviourId > 0
                && catalog.SkillMovePassHeight(move.MoveBehaviourId) == 999;
        }

        internal static bool UsesDirectPath(FightBattleSkillDefinition skill, FightOfficialCombatCatalog catalog)
            => skill != null && (skill.MoveByTeleport
                || UsesStandableLanding(skill, catalog) || LandsBesideTarget(skill));

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
