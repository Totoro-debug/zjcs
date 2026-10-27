using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed class FightVoodooDollDefinition
    {
        public int SyncSkillEntityId;
        public int VoodooStatusId;
        public int FollowDieStatusId;
        public int RoundType;
        public int SyncHitEntityId;
        public int SyncDamageEntityId;
        public List<FightGridPoint> Body = new List<FightGridPoint>();
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        public FightVoodooDollDefinition VoodooDollDefinition(int entityId)
        {
            var components = RequiredEntity(entityId);
            var config = Component(components, "FightRoleVoodooDollComponent");
            if (config == null || Int(config, "SyncType") != 1)
                throw new InvalidOperationException("Unconfirmed substitute sync type: " + entityId);
            var output = new FightVoodooDollDefinition {
                SyncSkillEntityId = Int(config, "SyncSkillEntityId"),
                VoodooStatusId = Int(config, "VoodooStatus"),
                FollowDieStatusId = Int(config, "FollowDieId"),
                RoundType = Int(Component(components, "FightRoundComponent"), "RoundType")
            };
            var grid = Component(components, "GridTransformComponent");
            var syncSkill = Component(RequiredEntity(output.SyncSkillEntityId), "FightSkillComponent");
            var hits = Array(Required(syncSkill, "HitList"), "substitute sync hits");
            if (hits.Count != 1) throw new InvalidOperationException("Unsupported substitute sync hit graph.");
            output.SyncHitEntityId = Int(Object(hits[0], "sync hit"), "ClassId");
            output.SyncDamageEntityId = Int(Component(RequiredEntity(output.SyncHitEntityId),
                "FightHitFixedComponent"), "DamageId");
            foreach (object raw in Array(Required(grid, "BodyRangeInfos"), "substitute body"))
            {
                var pos = Component(Object(raw, "body"), "Pos");
                output.Body.Add(new FightGridPoint(Int(pos, "X"), Int(pos, "Y")));
            }
            return output;
        }
    }
}
