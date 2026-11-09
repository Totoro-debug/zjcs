using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Official.BattleSkillPlayables
{
    // Empty official client roles consume no shared fields or notifications.
    public interface IFightHitPassiveViewData { }

    public interface IFightHitViewBinding
    {
        IFightHitViewServices ViewServices { get; set; }
        IMyPlayableManager mMyPlayableManager { get; set; }
    }
    // Consumed original state only. The host supplies actual shared hit entities,
    // their original ordered scopes and their notifications at each transition.
    public abstract class FightHitBaseComponent : IEntityComponentInfoSource
    {
        public readonly GAction<Pos2d> OnHitPosChange = new GAction<Pos2d>();
        public abstract EcEntityIdentifier SkillId { get; }
        public abstract ECViewEntityData Entity { get; }
        public abstract FightHitGridTransformData GridTransform { get; }
        public abstract FightHitBaseComponentInfo Info { get; }
        public abstract List<HitScopCfg> GetScopes();
        object IEntityComponentInfoSource.ComponentInfo => Info;
    }

    public abstract class FightHitGridTransformData : ECGridTransformData
    {
        public abstract Pos2d LocalToWorldPos(Pos2d position);
    }

    public class FightHitBaseComponentInfo
    {
        public Vector3 Offset, TargetOffset, RotateCenterOffset;
        public float DelayDistFactor;
        public bool IsUseCenterRotation;
    }

    public sealed class HitScopCfg
    {
        public Pos2d Pos;
        public float Delay;
    }

    public abstract class FightHitFixedComponent : FightHitBaseComponent { }
    public abstract class FightHitSummonComponent : FightHitBaseComponent { }
    public abstract class FightHitRandomTargetComponent : FightHitBaseComponent { }
    public abstract class FightHitCustomTargetComponent : FightHitBaseComponent { }
    public abstract class FightHitTriggerSkillComponent : FightHitBaseComponent { }
    public abstract class FightHitChainedComponent : FightHitBaseComponent
    {
        public abstract Pos2d StartGridPos { get; }
    }
    public class FightHitFixedComponentInfo : FightHitBaseComponentInfo { }
    public class FightHitSummonComponentInfo : FightHitBaseComponentInfo { }
    public class FightHitRandomTargetComponentInfo : FightHitBaseComponentInfo { }
    public class FightHitCustomTargetComponentInfo : FightHitBaseComponentInfo { }
    public class FightHitTriggerSkillComponentInfo : FightHitBaseComponentInfo { }
    public class FightHitChainedComponentInfo : FightHitBaseComponentInfo { }

    public interface IFightHitViewServices : IFightSkillViewServices
    {
        Vector3 ToWorldPos(Vector3 position);
    }

    public interface IFightHitSourceStatusServices
    {
        // Original SourceEnt -> FightSkillAgent -> FightStatusAgent query.
        // Return null only when that original query has no matching entity/agent.
        FightHitSourceStatusAgent GetSourceStatusAgent(MyFightSkill skill);
    }

    public abstract class FightHitSourceStatusAgent
    {
        public abstract IDictionary<int, int> StatusToShareStack { get; }
        public abstract IDictionary<EcEntityIdentifier, FightHitSourceStatus> ApplyStatusDict { get; }
    }

    public abstract class FightHitSourceStatus
    {
        public abstract ECViewEntityData Entity { get; }
        public abstract int StackedCount { get; }
    }

    public sealed class FightHitExtDelayInfoOverriderByStatusStackComponentInfo
    {
        public int StatusId;
        public List<float> DelayDistFactors;
        public List<Vector3> Offsets, TargetOffsets, RotationCenterOffsets;
        public List<bool> IsUseCenterRotations;
    }

    public abstract class FightHitExtDelayInfoOverriderByStatusStackComponent : IEntityComponentInfoSource
    {
        public abstract FightHitExtDelayInfoOverriderByStatusStackComponentInfo Info { get; }
        object IEntityComponentInfoSource.ComponentInfo => Info;

        // Original shared component SetValue: out-of-range entries retain the base value.
        public static void SetValue<T>(ref T target, List<T> data, int index)
        {
            if (index >= 0 && index < data.Count) target = data[index];
        }
    }
}
