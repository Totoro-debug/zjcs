using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Combat
{
    public static class OfficialEquipmentBattlePassives
    {
        public static List<FightOfficialPassiveSkillBinding> Collect(
            OfflineCharacterProgressionService progression, OfflinePlayerProgress player, int ownerSubRank)
        {
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            return progression.EquipmentBattlePassiveTerms(player).Select(term => new FightOfficialPassiveSkillBinding
            {
                ItemId = term.SkillItemId,
                Rank = term.SkillRank,
                Level = term.SkillLevel,
                SubRank = ownerSubRank,
                ReplacementProperties = term.Parameters.Select(value => new FightStatusReplacementValue
                {
                    PropertyId = value.PropertyId, RawValue = value.RawValue, IsFloat = value.IsFloat
                }).ToList(),
                BlessPropertyScales = term.PropertyScales.Select(value => new FightStatusPropertyScale
                {
                    EntityId = value.EntityId, PropertyId = value.PropertyId, Factor = value.Factor
                }).ToList()
            }).ToList();
        }
    }
}
