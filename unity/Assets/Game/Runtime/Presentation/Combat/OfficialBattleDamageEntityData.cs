using System;
using UnityEngine;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Consumed client damage state at one actual damage-entity identity.</summary>
    public sealed class OfficialBattleDamageEntityData : ECViewEntityData
    {
        private sealed class DamageState : FightDamageComponent
        {
            public EcEntityIdentifier Target, Skill;
            public DamageResultParameter Result;
            public bool TriggerStatus;
            public override EcEntityIdentifier TargetId => Target;
            public override EcEntityIdentifier SkillId => Skill;
            public override DamageResultParameter DamageResult => Result;
            public override bool IsDamageTriggerStatus => TriggerStatus;
        }
        private sealed class TransformState : ECTransformData
        {
            public Vector3 Point, Size;
            public Quaternion Orientation;
            public override Vector3 Position => Point;
            public override Quaternion Rotation => Orientation;
            public override Vector3 Scale => Size;
        }
        private readonly EcEntityIdentifier identity;
        private readonly int classId;
        private readonly ECGridTransformData grid;
        private readonly TransformState transform;
        private readonly DamageState damage;

        public OfficialBattleDamageEntityData(EcEntityIdentifier identity, int classId,
            EcEntityIdentifier skill, EcEntityIdentifier target, bool block, bool critical,
            bool triggeredStatus, Vector3 position, Quaternion rotation, Vector3 scale,
            ECGridTransformData grid)
        {
            if (classId <= 0) throw new ArgumentOutOfRangeException(nameof(classId));
            this.identity = identity;
            this.classId = classId;
            this.grid = grid;
            transform = new TransformState { Point = position, Orientation = rotation, Size = scale };
            damage = new DamageState { Target = target, Skill = skill, TriggerStatus = triggeredStatus,
                Result = new DamageResultParameter { DamageTypeData = new DamageTypeData {
                    IsBlock = block, IsCrit = critical } } };
        }
        public override EcEntityIdentifier Id => identity;
        public override int ClassId => classId;
        public override T GetComponent<T>()
        {
            object value = typeof(T).IsInstanceOfType(damage) ? damage
                : typeof(T).IsInstanceOfType(grid) ? grid
                : typeof(T).IsInstanceOfType(transform) ? transform : null;
            return value == null ? default : (T)value;
        }
    }
}
