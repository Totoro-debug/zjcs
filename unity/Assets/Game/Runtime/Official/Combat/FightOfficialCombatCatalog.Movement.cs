using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        // NpcInfo.FightEntityId selects the role prefab; the NPC class ID remains
        // the social/progression identity. Preserve the role's configured type.
        public void BindNpcRoleEntity(FightBattleUnitState unit, int entityId)
        {
            if (unit == null || unit.NpcClassId <= 0 || !entities.TryGetValue(entityId, out var entity))
                throw new System.ArgumentException("A configured NPC role entity is required.");
            var role = Component(entity, "FightRoleCharacterComponent")
                ?? throw new System.InvalidOperationException("Original NPC role character component is missing: " + entityId);
            unit.RoleEntityId = entityId;
            unit.CharacterType = Int(role, "CharacterType");
        }
        // FightActionSkillMoveComponent.StartImpl commits facing before action-start listeners.
        public void ApplyMovementFacing(FightBattleUnitState unit, FightBattleMovementEvent movement)
        {
            if (unit == null || movement?.Path == null || movement.Path.Count < 2
                || !entities.TryGetValue(movement.MoveBehaviourId, out var entity)) return;
            var action = Component(entity, "FightActionSkillMoveComponent");
            if (action == null) return;
            bool flip = unit.HasFlipX ? unit.FlipX : unit.Direction == 1;
            if (Bool(action, "UpdateFlipX"))
            {
                var source = movement.Path[movement.Path.Count - 2];
                var target = movement.Path[movement.Path.Count - 1];
                int deltaX = target.X.CompareTo(source.X);
                if (Bool(action, "ReverseFlipX")) deltaX = -deltaX;
                if (deltaX != 0) flip = deltaX < 0;
            }
            unit.HasFlipX = true;
            unit.FlipX = flip;
            movement.HasFacing = true;
            movement.EndFlipX = flip;
        }

        // FightRoleCharacterComponentInfo selects separate forward/back movement skills.
        // Keep that identity in the event instead of substituting one player animation.
        public FightBattleMovementEvent CreateRoleMovement(FightBattleUnitState unit, bool retreat)
        {
            var result = new FightBattleMovementEvent { UnitId = unit.Id, IsRetreat = retreat,
                HasDirection = true, Direction = unit.Direction };
            int entityId = unit.RoleEntityId > 0 ? unit.RoleEntityId
                : unit.NpcClassId > 0 ? unit.NpcClassId
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
