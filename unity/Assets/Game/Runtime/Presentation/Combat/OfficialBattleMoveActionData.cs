using System;
using System.Collections.Generic;
using System.Globalization;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityActions;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.Combat
{
    // Consumed original action and move components for one already-settled path.
    // Position, direction and blocked-cell decisions belong to the caller.
    public sealed class OfficialBattleMoveActionData : ECViewEntityData
    {
        private sealed class ActionState : ActionComponentData
        {
            public EcEntityIdentifier Apply;
            public IReadOnlyDictionary<string, object> Configuration;
            public override EcEntityIdentifier ApplyId => Apply;
            public override object ComponentInfo => Configuration;
        }

        private readonly int classId;
        private readonly EcEntityIdentifier identity;
        private readonly ActionState action;
        public FightActionSkillMoveComponent Movement { get; }

        public OfficialBattleMoveActionData(FightOfficialCombatCatalog catalog, int classId,
            EcEntityIdentifier identity, EcEntityIdentifier applyId, Pos2d source,
            List<Pos2d> path, Direction direction, Pos2d? blockedPosition = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!catalog.TryGetComponent(classId, "FightActionSkillMoveComponent", out var move)
                || !catalog.TryGetComponent(classId, "ActionComponent", out var originalAction))
                throw new InvalidOperationException("Original movement action configuration is unavailable: " + classId);
            this.classId = classId;
            this.identity = identity;
            action = new ActionState { Apply = applyId, Configuration = originalAction };
            Movement = new FightActionSkillMoveComponent {
                ClassId = classId, Path = new List<Pos2d>(path), Direction = direction,
                BlockedPos = blockedPosition,
                Info = new FightActionSkillMoveComponentInfo {
                    UpdateFlipX = Boolean(move, "UpdateFlipX"), ReverseFlipX = Boolean(move, "ReverseFlipX"),
                    PassHeight = Integer(move, "PassHeight"), MoveDuration = Number(move, "MoveDuration"),
                    MoveSpeed = Number(move, "MoveSpeed"), BlockedLen = Number(move, "BlockedLen"),
                    BlockedSkillId = Integer(move, "BlockedSkillId"), DelayDestroyTime = Number(move, "DelayDestroyTime"),
                    ForceToTargetPos = Boolean(move, "ForceToTargetPos"), ForcePushOutSkillId = Integer(move, "ForcePushOutSkillId")
                }
            };
            Movement.InitializeMovement(source);
        }

        public override int ClassId => classId;
        public override EcEntityIdentifier Id => identity;
        public override T GetComponent<T>()
        {
            object value = typeof(T).IsInstanceOfType(Movement) ? Movement
                : typeof(T).IsInstanceOfType(action) ? action : null;
            return value == null ? default : (T)value;
        }

        private static object Required(IReadOnlyDictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value == null)
                throw new InvalidOperationException("Original movement configuration is missing " + key);
            return value;
        }
        private static bool Boolean(IReadOnlyDictionary<string, object> data, string key) =>
            Convert.ToBoolean(Required(data, key), CultureInfo.InvariantCulture);
        private static int Integer(IReadOnlyDictionary<string, object> data, string key) =>
            Convert.ToInt32(Required(data, key), CultureInfo.InvariantCulture);
        private static float Number(IReadOnlyDictionary<string, object> data, string key) =>
            Convert.ToSingle(Required(data, key), CultureInfo.InvariantCulture);
    }
}
