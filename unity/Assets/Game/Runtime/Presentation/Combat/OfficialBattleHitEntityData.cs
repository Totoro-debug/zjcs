using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Original hit-view data backed by one actual hit identity and shared grid.</summary>
    public sealed class OfficialBattleHitEntityData : ECViewEntityData
    {
        private readonly EcEntityIdentifier identity, skill;
        private readonly int classId;
        private readonly FightHitGridTransformData grid;
        private readonly Pos2d chainStart;
        private readonly FightHitBaseComponentInfo info;
        private readonly List<HitScopCfg> scopes = new List<HitScopCfg>();
        public FightHitBaseComponent Hit { get; }

        public OfficialBattleHitEntityData(EcEntityIdentifier identity, EcEntityIdentifier skill,
            FightBattleHitEntity definition, FightHitGridTransformData grid, Pos2d chainStart)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (definition.Trajectory == null || definition.ScopeEntries == null)
                throw new InvalidOperationException("Restore original hit configuration before creating its view.");
            this.identity = identity;
            this.skill = skill;
            this.chainStart = chainStart;
            classId = definition.EntityId;
            switch (definition.HitComponentType)
            {
                case "FightHitFixedComponent":
                    info = new FightHitFixedComponentInfo(); Hit = new Fixed(this); break;
                case "FightHitChainedComponent":
                    info = new FightHitChainedComponentInfo(); Hit = new Chained(this); break;
                case "FightHitSummonComponent":
                    info = new FightHitSummonComponentInfo(); Hit = new Summon(this); break;
                case "FightHitRandomTargetComponent":
                    info = new FightHitRandomTargetComponentInfo(); Hit = new RandomTarget(this); break;
                case "FightHitCustomTargetComponent":
                    info = new FightHitCustomTargetComponentInfo(); Hit = new CustomTarget(this); break;
                case "FightHitTriggerSkillComponent":
                    info = new FightHitTriggerSkillComponentInfo(); Hit = new TriggerSkill(this); break;
                default: throw new NotSupportedException("Unbound original hit component: " + definition.HitComponentType);
            }
            var trajectory = definition.Trajectory;
            info.Offset = new Vector3(trajectory.OffsetX, trajectory.OffsetY, trajectory.OffsetZ);
            info.TargetOffset = new Vector3(trajectory.TargetOffsetX, trajectory.TargetOffsetY, trajectory.TargetOffsetZ);
            info.RotateCenterOffset = new Vector3(trajectory.RotateCenterOffsetX,
                trajectory.RotateCenterOffsetY, trajectory.RotateCenterOffsetZ);
            info.IsUseCenterRotation = trajectory.IsUseCenterRotation;
            info.DelayDistFactor = definition.DelayDistFactor;
            foreach (var entry in definition.ScopeEntries)
            {
                if (entry == null) throw new InvalidOperationException("Original hit scope contains a null entry.");
                scopes.Add(new HitScopCfg { Pos = new Pos2d(entry.Position.X, entry.Position.Y), Delay = entry.Delay });
            }
        }

        public override int ClassId => classId;
        public override EcEntityIdentifier Id => identity;
        public override T GetComponent<T>()
        {
            object value = typeof(T).IsInstanceOfType(grid) ? grid
                : typeof(T).IsInstanceOfType(Hit) ? Hit : null;
            return value == null ? default : (T)value;
        }

        // The shared hit/grid transition must be applied before its original notification.
        public void PublishPositionChanged() => Hit.OnHitPosChange.SafeInvoke(grid.Pos);

        private sealed class Fixed : FightHitFixedComponent
        {
            private readonly OfficialBattleHitEntityData owner;
            public Fixed(OfficialBattleHitEntityData owner) { this.owner = owner; }
            public override EcEntityIdentifier SkillId => owner.skill;
            public override ECViewEntityData Entity => owner;
            public override FightHitGridTransformData GridTransform => owner.grid;
            public override FightHitBaseComponentInfo Info => owner.info;
            public override List<HitScopCfg> GetScopes() => owner.scopes;
        }
        private sealed class Chained : FightHitChainedComponent
        {
            private readonly OfficialBattleHitEntityData owner;
            public Chained(OfficialBattleHitEntityData owner) { this.owner = owner; }
            public override EcEntityIdentifier SkillId => owner.skill;
            public override ECViewEntityData Entity => owner;
            public override FightHitGridTransformData GridTransform => owner.grid;
            public override FightHitBaseComponentInfo Info => owner.info;
            public override Pos2d StartGridPos => owner.chainStart;
            public override List<HitScopCfg> GetScopes() => owner.scopes;
        }
        private sealed class Summon : FightHitSummonComponent
        {
            private readonly OfficialBattleHitEntityData owner;
            public Summon(OfficialBattleHitEntityData owner) { this.owner = owner; }
            public override EcEntityIdentifier SkillId => owner.skill;
            public override ECViewEntityData Entity => owner;
            public override FightHitGridTransformData GridTransform => owner.grid;
            public override FightHitBaseComponentInfo Info => owner.info;
            public override List<HitScopCfg> GetScopes() => owner.scopes;
        }
        private sealed class RandomTarget : FightHitRandomTargetComponent
        {
            private readonly OfficialBattleHitEntityData owner;
            public RandomTarget(OfficialBattleHitEntityData owner) { this.owner = owner; }
            public override EcEntityIdentifier SkillId => owner.skill;
            public override ECViewEntityData Entity => owner;
            public override FightHitGridTransformData GridTransform => owner.grid;
            public override FightHitBaseComponentInfo Info => owner.info;
            public override List<HitScopCfg> GetScopes() => owner.scopes;
        }
        private sealed class CustomTarget : FightHitCustomTargetComponent
        {
            private readonly OfficialBattleHitEntityData owner;
            public CustomTarget(OfficialBattleHitEntityData owner) { this.owner = owner; }
            public override EcEntityIdentifier SkillId => owner.skill;
            public override ECViewEntityData Entity => owner;
            public override FightHitGridTransformData GridTransform => owner.grid;
            public override FightHitBaseComponentInfo Info => owner.info;
            public override List<HitScopCfg> GetScopes() => owner.scopes;
        }
        private sealed class TriggerSkill : FightHitTriggerSkillComponent
        {
            private readonly OfficialBattleHitEntityData owner;
            public TriggerSkill(OfficialBattleHitEntityData owner) { this.owner = owner; }
            public override EcEntityIdentifier SkillId => owner.skill;
            public override ECViewEntityData Entity => owner;
            public override FightHitGridTransformData GridTransform => owner.grid;
            public override FightHitBaseComponentInfo Info => owner.info;
            public override List<HitScopCfg> GetScopes() => owner.scopes;
        }
    }
}
