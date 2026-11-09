using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    // Each original skill prefab owns this query context. Concurrent casts by
    // the same source must not observe another cast's consumed/later statuses.
    public sealed class OfficialBattleSkillSourceStatusSnapshot :
        IFightPlayableOverrideStatusServices, IFightHitSourceStatusServices
    {
        private sealed class StatusEntity : ECViewEntityData
        {
            public int TypeId;
            public EcEntityIdentifier Identifier;
            public override int ClassId => TypeId;
            public override EcEntityIdentifier Id => Identifier;
            public override T GetComponent<T>() => default;
        }

        private sealed class Status : FightHitSourceStatus
        {
            public StatusEntity Data;
            public int Stacks;
            public override ECViewEntityData Entity => Data;
            public override int StackedCount => Stacks;
        }

        private sealed class Agent : FightHitSourceStatusAgent
        {
            private readonly Dictionary<int, int> shared = new Dictionary<int, int>();
            private readonly Dictionary<EcEntityIdentifier, FightHitSourceStatus> statuses =
                new Dictionary<EcEntityIdentifier, FightHitSourceStatus>();
            public override IDictionary<int, int> StatusToShareStack => shared;
            public override IDictionary<EcEntityIdentifier, FightHitSourceStatus> ApplyStatusDict => statuses;

            public Agent(FightBattleSkillCastPresentation cast, EcEntityIdentifier source)
            {
                foreach (var status in cast.SourceStatuses)
                {
                    // Local status instances are owned by a unit. This private
                    // view dictionary preserves that pair and application order.
                    var id = new EcEntityIdentifier(source.EntityId, status.InstanceId);
                    statuses.Add(id, new Status { Stacks = status.StackCount,
                        Data = new StatusEntity { TypeId = status.ClassId, Identifier = id } });
                }
                foreach (var value in cast.SourceSharedStacks)
                    shared.Add(value.ShareId, value.StackCount);
            }
        }

        private readonly FightBattleSkillCastPresentation cast;
        private readonly EcEntityIdentifier source;
        private readonly FightOfficialCombatCatalog catalog;
        private Agent agent;

        public OfficialBattleSkillSourceStatusSnapshot(FightBattleSkillCastPresentation cast,
            EcEntityIdentifier source, FightOfficialCombatCatalog catalog)
        {
            this.cast = cast ?? throw new ArgumentNullException(nameof(cast));
            this.source = source;
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public FightHitSourceStatusAgent GetSourceStatusAgent(MyEntity entity) => Get(entity, false);

        public FightHitSourceStatusAgent GetSourceStatusAgent(MyFightSkill skill) =>
            skill == null || skill.Component == null ? null : Get(skill.Source, true);

        private FightHitSourceStatusAgent Get(MyEntity entity, bool requireSkillAgent)
        {
            if (entity == null || !entity.Id.Equals(source)
                || !catalog.HasComponent(entity.ClassId, "FightStatusAgentComponent")
                || requireSkillAgent && !catalog.HasComponent(entity.ClassId, "FightSkillAgentComponent"))
                return null;
            return agent ?? (agent = new Agent(cast, source));
        }
    }
}
