using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private FightBattleUnitState CreatePetSummon(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, FightBattleSummonPool pool,
            FightBattleHitDefinition hit, FightGridPoint position, FightBattleUnitState reference)
        {
            // FightHitSummonPetCfg uses the owner's pet assembly, not monster_group.
            // The transfer caster has its owner's id but is intentionally transient.
            int masterId = ResolveSummonMaster(actor, hit.SummonReferenceType);
            FightBattleUnitState owner;
            if (!unitById.TryGetValue(masterId, out owner))
                throw new InvalidOperationException("Pet summon owner is not registered: " + masterId);
            FightBattlePetEnergyState pet = owner.PetEnergy;
            if (pet == null || !pet.Enabled || pet.PetEntityClassId != pool.MonsterId
                || pet.EntityProperties == null || pet.EntityProperties.Count == 0)
                throw new InvalidOperationException("Pet summon assembly is missing: " + pool.MonsterId);
            if (!hit.SummonInheritProps)
                throw new InvalidOperationException("Pet summon without inherited assembly is not migrated.");
            var properties = new Dictionary<int, long>();
            foreach (FightPropertyValue property in pet.EntityProperties)
                properties[property.Id] = property.Value;
            var body = statusCatalog.MonsterBodyOffsets(pool.MonsterId);
            if (body.Count == 0)
                throw new InvalidOperationException("Pet summon body is unbound: " + pool.MonsterId);
            var summon = new FightBattleUnitState {
                Id = NextSummonUnitId(), MasterId = masterId, Side = owner.Side,
                CharacterType = statusCatalog.MonsterCharacterType(pool.MonsterId),
                AiType = statusCatalog.MonsterAiType(pool.MonsterId),
                MonsterId = pool.MonsterId,
                MonsterTemplateId = statusCatalog.MonsterTemplateId(pool.MonsterId),
                MonsterType = statusCatalog.MonsterType(pool.MonsterId),
                Level = Math.Max(1, pet.SkillLevel), SubRank = pet.SkillSubRank,
                Profession = owner.Profession, Wave = owner.Wave,
                Direction = reference.Direction, MoveMask = owner.MoveMask,
                X = position.X, Y = position.Y,
                Hp = Property(properties, formula.PropertyId("MaxHp")),
                MaxHp = Property(properties, formula.PropertyId("MaxHp")),
                LastMaxHp = Property(properties, formula.PropertyId("MaxHp")),
                BaseProperties = ToPropertyList(properties), Properties = ToPropertyList(properties),
                BodyOffsets = body
            };
            if (summon.MaxHp <= 0)
                throw new InvalidOperationException("Pet summon has no assembled MaxHp: " + pool.MonsterId);
            summon.MoveDist = formula.CalculateMoveDist(properties);
            summon.Turn.Id = summon.Id;
            summon.Turn.MasterId = masterId;
            summon.Turn.Hp = summon.Hp;
            summon.Turn.Speed = (int)Math.Max(1L, Property(properties, formula.PropertyId("Speed")));
            foreach (FightBattleSkillDefinition active in pet.EntityActiveSkills)
            {
                var definition = statusCatalog.CreateSkill(active.EntityId, active.Slot,
                    active.ItemId, active.Rank, active.Level, active.SubRank, active.Properties);
                summon.Skills.Add(definition);
                long cooldown = statusCatalog.CalculateSkillCooldown(active.EntityId, active.Rank,
                    active.Level, active.SubRank, properties);
                summon.SkillState.Slots.Add(statusCatalog.CreateSkillSlot(definition, cooldown));
            }
            new FightSkillState(summon.SkillState).Initialize(summon.SkillCooldownChangeAtStart);
            foreach (int statusId in statusCatalog.MonsterInitialStatusIds(pool.MonsterId))
                ApplySummonStatus(summon, actor, skill, statusId);
            foreach (int statusId in pet.EntityPassiveStatusIds)
                ApplySummonStatus(summon, actor, skill, statusId);
            return summon;
        }
    }
}
