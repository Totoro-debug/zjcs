using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Zjcs.Combat.Official;

public static class BattlePetEntityHarness
{
    private static void Require(bool value, string message)
    { if (!value) throw new Exception(message); }

    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        var properties = new List<FightPropertyValue> {
            new FightPropertyValue { Id = 1, Value = 1000 },
            new FightPropertyValue { Id = 2, Value = 100 },
            new FightPropertyValue { Id = 3, Value = 100000 },
            new FightPropertyValue { Id = 4, Value = 100 } };
        var owner = new FightBattleUnitState { Id = 1, Side = 1, CharacterType = 1,
            Level = 20, SubRank = 1, X = 5, Y = 5, Hp = 100000, MaxHp = 100000,
            Properties = properties,
            Turn = new FightTurnUnit { Id = 1, Hp = 100000, Speed = 100 } };
        var item = items.Required(16100);
        owner.PetEnergy = new FightBattlePetEnergyState {
            Enabled = true, OwnerUnitId = 1, PetUnitId = 1, PetClassId = 1600,
            PetEntityClassId = 9301, PetEvolutionPhase = 2,
            SkillEntityId = item.EntityId, SkillItemId = 16100,
            SkillRank = 4, SkillLevel = 20, SkillSubRank = 1,
            InitialEnergy = 500, NeedEnergy = 500,
            EntityProperties = properties.Select(p => new FightPropertyValue { Id = p.Id, Value = p.Value }).ToList() };
        owner.PetEnergy.EntityProperties.Single(p => p.Id == 3).Value = 43210;
        owner.PetEnergy.EntityProperties.Single(p => p.Id == 1).Value = 1234;
        foreach (int id in new[] {16400, 16401})
        {
            var active = items.Required(id);
            owner.PetEnergy.EntityActiveSkills.Add(catalog.CreateSkill(active.EntityId,
                owner.PetEnergy.EntityActiveSkills.Count, id, 3, 20, 1,
                catalog.CalculateSkillProps(active.EntityId, 3, 20, 1)
                    .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList()));
        }
        var enemy = new FightBattleUnitState { Id = 2, Side = 2, CharacterType = 2,
            X = 8, Y = 8, Hp = 100000, MaxHp = 100000,
            Turn = new FightTurnUnit { Id = 2, Hp = 100000, Speed = 1 } };
        var state = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 13 };
        state.Units.Add(owner); state.Units.Add(enemy);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        var result = executor.Step(0, 1);
        var pet = state.Units.SingleOrDefault(p => p.MonsterId == 9301);
        Require(pet != null && pet.MasterId == 1 && pet.CharacterType == 7 && pet.X == 5 && pet.Y == 3,
            "Actual energy skill did not register its configured pet entity.");
        Require(pet.MaxHp == 43210 && pet.Hp == 43210
            && pet.Properties.Single(p => p.Id == 1).Value == 1234
            && owner.MaxHp == 100000 && owner.Properties.Single(p => p.Id == 1).Value == 1000 && pet.Level == 20
            && pet.Skills.Count == 2 && pet.Skills.All(s => s.Rank == 3 && s.Level == 20),
            "Pet entity lost its assembled properties or saved skill ranks/levels.");
        Require(pet.Statuses.Any(s => s.ClassId == 51002) && result.Event.SpawnedUnitIds.Contains(pet.Id),
            "Pet summon lost its original status or spawn event.");
        var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
        var copy = restored.Units.Single(p => p.MonsterId == 9301);
        Require(copy.MasterId == pet.MasterId && copy.Skills[0].Rank == 3 && copy.MaxHp == pet.MaxHp,
            "Pet entity checkpoint changed properties or skill rank.");
        FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);

        VerifyPetEnergyHpSynchronization(executor, owner);
    }

    private static void VerifyPetEnergyHpSynchronization(FightBattleExecutor executor,
        FightBattleUnitState owner)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type executorType = typeof(FightBattleExecutor);
        FieldInfo ownerField = executorType.GetField("petEnergyHpOwner", flags);
        FieldInfo casterField = executorType.GetField("petEnergyHpCaster", flags);
        MethodInfo synchronize = executorType.GetMethod("SynchronizePetEnergyHp", flags);
        MethodInfo executeSkill = executorType.GetMethods(flags).Single(method =>
            method.Name == "ExecuteSkill" && method.GetParameters().Length == 10);
        Require(ownerField != null && casterField != null && synchronize != null,
            "Pet energy HP synchronization context is missing.");

        long originalHp = owner.Hp;
        long originalTurnHp = owner.Turn.Hp;
        var costSkill = new FightBattleSkillDefinition
        {
            EntityId = 16100,
            CostType = 1,
            SkillCost = 1000
        };
        var caster = new FightBattleUnitState
        {
            Id = owner.Id,
            Hp = owner.Hp,
            MaxHp = owner.MaxHp,
            Turn = new FightTurnUnit { Id = owner.Id, Hp = owner.Hp }
        };
        ownerField.SetValue(executor, owner);
        casterField.SetValue(executor, caster);
        try
        {
            caster.Hp = Math.Max(0L, owner.Hp - 1000L);
            MethodInfo applyHpTriggers = executorType.GetMethod("ApplyHpChangeTriggers", flags);
            applyHpTriggers.Invoke(executor, new object[] { caster, costSkill });
            Require(owner.Hp == caster.Hp && owner.Turn.Hp == caster.Hp,
                "Pet caster HP changes were not mirrored to its owner.");

            long lowHp = Math.Max(2000L, owner.MaxHp / 4L);
            owner.Hp = lowHp;
            owner.Turn.Hp = lowHp;
            caster.Hp = lowHp;
            object[] arguments =
            {
                owner, costSkill, new List<FightBattleUnitState> { owner },
                new List<FightBattleSkillCastEvent>(), new List<FightBattleDamageEvent>(),
                new List<FightBattleMovementEvent>(), 0f, null, false, 1.0
            };
            executeSkill.Invoke(executor, arguments);
            Require(owner.Hp == lowHp - costSkill.SkillCost && caster.Hp == owner.Hp
                && caster.Turn.Hp == owner.Hp,
                "Owner skill HP changes were not retained by the pet caster HP mirror: low="
                + lowHp + ", owner=" + owner.Hp + ", caster=" + caster.Hp
                + ", turn=" + caster.Turn.Hp + ".");
        }
        finally
        {
            ownerField.SetValue(executor, null);
            casterField.SetValue(executor, null);
            owner.Hp = originalHp;
            owner.Turn.Hp = originalTurnHp;
        }
    }
}
