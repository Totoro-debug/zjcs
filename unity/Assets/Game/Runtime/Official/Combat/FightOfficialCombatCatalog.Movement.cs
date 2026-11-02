using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        // FightRoleCharacterComponentInfo selects separate forward/back movement skills.
        // Keep that identity in the event instead of substituting one player animation.
        public FightBattleMovementEvent CreateRoleMovement(FightBattleUnitState unit, bool retreat)
        {
            var result = new FightBattleMovementEvent { UnitId = unit.Id, IsRetreat = retreat };
            int entityId = unit.NpcClassId > 0 ? unit.NpcClassId
                : unit.CharacterType == 1 ? 3100
                : unit.MonsterTemplateId > 0 ? unit.MonsterTemplateId
                : unit.MonsterId > 0 ? MonsterTemplateId(unit.MonsterId)
                : 0;
            if (!entities.TryGetValue(entityId, out Dictionary<string, object> entity)) return result;
            Dictionary<string, object> role = Component(entity, "FightRoleCharacterComponent");
            if (role == null) return result;
            int skillId = Int(role, retreat ? "BackMoveSkillId" : "MoveSkillId");
            if (!entities.TryGetValue(skillId, out Dictionary<string, object> skillEntity)) return result;
            Dictionary<string, object> skill = Component(skillEntity, "FightSkillComponent");
            if (skill == null || !skill.TryGetValue("SourceMoveList", out object raw) || raw == null) return result;
            List<object> moves = Array(raw, "role movement SourceMoveList");
            if (moves.Count != 1) return result;
            FightBattleSkillMove move = ParseMove(Object(moves[0], "role movement"));
            result.MoveSkillEntityId = skillId;
            result.MoveBehaviourId = move.MoveBehaviourId;
            result.Delay = move.Delay;
            result.MoveDuration = move.MoveDuration;
            result.MoveSpeed = move.MoveSpeed;
            return result;
        }
    }
}
