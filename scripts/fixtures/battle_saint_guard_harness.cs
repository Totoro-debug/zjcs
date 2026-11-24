using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSaintGuardHarness
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Saint guard: " + message);
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        CheckPropertyDefinition(catalog, formula);
        long ordinary = CheckShield(catalog, formula, items, false, 1, 1, 1, 10032, 10032);
        foreach (var source in new[] { new[] { 10032, 10032 }, new[] { 10575, 10575 }, new[] { 0, 0 } })
        {
            long boosted = CheckShield(catalog, formula, items, true, 1, 1, 1, source[0], source[1]);
            Check(boosted > ordinary, "self shield from a configured source did not gain the 10593 bonus");
            Check(CheckShield(catalog, formula, items, true, 2, 1, 1, source[0], source[1]) == boosted,
                "the player's defence shield cast on an ally lost the 10593 bonus");
        }
        CheckForeignShield(catalog, formula, items, ordinary);
        Check(CheckShield(catalog, formula, items, true, 1, 1, 5, 10032, 10032) == ordinary,
            "a pet shield with a shared owner id received the 10593 bonus");
        Check(CheckShield(catalog, formula, items, true, 1, 1, 0, 10032, 10032) == ordinary,
            "an unknown shield creator role received the 10593 bonus");
        CheckFixedShield(catalog, formula, items);
        long ordinaryCast = CastAllyShield(catalog, formula, items, false, 1, 1);
        foreach (int rank in new[] { 1, 3, 5 })
        foreach (int level in new[] { 1, 2, 20 })
            Check(CastAllyShield(catalog, formula, items, true, rank, level) > ordinaryCast,
                "an actual cultivated allied shield cast did not receive the defence-shield bonus");
        return "Saint guard defence-shield formula, self/ally recipients, active/passive/weapon sources, foreign/pet exclusions, fixed-shield filter and checkpoint cases passed.";
    }

    private static void CheckPropertyDefinition(FightOfficialCombatCatalog catalog, FightDamageFormula formula)
    {
        var state = catalog.CreateStatus(25109, 1, 1, 1, 25109, 10593, 1, 1, 8,
            new List<FightPropertyValue>());
        Check(state.Components.ShieldByStatusProperty != null
            && state.Components.ShieldByStatusProperty.IsShieldStatus
            && state.Components.ShieldByStatusProperty.IncludePropertyTypes.Contains(
                formula.PropertyId("ShieldByDefence")), "official 25109 shield filter was not preserved");
    }

    private static long CheckShield(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, bool equipPassive, int ownerId, int creatorId,
        int creatorType, int rootSkillId, int skillItemId)
    {
        var checkpoint = BaseCheckpoint(formula);
        var creator = Unit(creatorId, 1, 1, formula);
        var owner = ownerId == creatorId ? creator : Unit(ownerId, 1, 1, formula);
        FightBattleStatusState passive = null;
        if (equipPassive)
        {
            passive = catalog.CreateStatus(25109, creator.Id, creator.Id, 1, 25109, 10593, 1, 1, 8,
                creator.Properties);
            creator.Statuses.Add(passive);
            var participants = new List<FightBattleUnitState> { creator };
            if (owner != creator) participants.Add(owner);
            FightStatusPropertyRuntime.RebuildAll(participants, catalog, formula);
        }
        var shield = DefenceShield(formula, owner.Id, creator.Id, creatorType,
            rootSkillId, skillItemId, creator.Properties);
        owner.Statuses.Add(shield);
        if (equipPassive && skillItemId == 10575 && owner == creator)
        {
            creator.Statuses.Remove(passive);
            creator.Statuses.Add(passive);
        }
        checkpoint.Units.Add(creator);
        if (owner != creator) checkpoint.Units.Add(owner);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        long before = shield.ShieldRemainValue;
        long expected = CalculateExpectedShield(shield, creator, formula);
        Check(before == expected, "resolved shield did not follow fixed + defence-ratio formula: expected="
            + expected + ", actual=" + before + ", defence=" + creator.Properties.Single(value =>
                value.Id == formula.PropertyId("Defence")).Value + ", fixed=" + Property(shield, formula, "SkillFixedShield")
            + ", ratio=" + Property(shield, formula, "ShieldByDefence"));
        var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
        long restored = copy.Units.Single(unit => unit.Id == owner.Id).Statuses
            .Single(status => status.InstanceId == shield.InstanceId).ShieldRemainValue;
        Check(before == restored, "shield source scope changed after checkpoint restore");
        return before;
    }

    private static long CalculateExpectedShield(FightBattleStatusState shield,
        FightBattleUnitState creator, FightDamageFormula formula)
    {
        var values = Values(shield.StatusProperties);
        var source = Values(creator.Properties);
        bool playerCreatedShield = creator.CharacterType == 1 && shield.CreatorCharacterType == 1;
        if (!playerCreatedShield)
        {
            int[] bonusIds = { formula.PropertyId("StatusShieldAddPercent"),
                formula.PropertyId("StatusFixedShieldAdd") };
            foreach (FightBattleStatusState passive in creator.Statuses.Where(value =>
                value?.Components?.ShieldByStatusProperty?.IsShieldStatus == true))
                foreach (FightPropertyValue property in passive.ResolvedProperties ?? new List<FightPropertyValue>())
                    if (property != null && bonusIds.Contains(property.Id)
                        && source.TryGetValue(property.Id, out long current))
                        source[property.Id] = current - Math.Min(Math.Max(0L, current), Math.Max(0L, property.Value));
        }
        int defenceShield = formula.PropertyId("ShieldByDefence");
        int fixedShield = formula.PropertyId("SkillFixedShield");
        long shieldValue = (long)(formula.ReadProperty(source, formula.PropertyId("Defence"))
            * formula.ReadProperty(values, defenceShield))
            + (long)formula.ReadProperty(values, fixedShield);
        double percentage = formula.ReadProperty(source, formula.PropertyId("StatusShieldAddPercent"));
        double fixedValue = formula.ReadProperty(source, formula.PropertyId("StatusFixedShieldAdd"));
        return (long)Math.Max(0.0, shieldValue * (1.0 + percentage) + fixedValue);
    }

    private static long Property(FightBattleStatusState status, FightDamageFormula formula, string name)
    {
        int id = formula.PropertyId(name);
        return (status.StatusProperties ?? new List<FightPropertyValue>())
            .Where(value => value.Id == id).Select(value => value.Value).FirstOrDefault();
    }

    private static FightBattleStatusState DefenceShield(FightDamageFormula formula, int ownerId,
        int creatorId, int creatorCharacterType, int rootSkillId, int itemId,
        IList<FightPropertyValue> sourceProperties)
    {
        return Shield(formula, ownerId, creatorId, creatorCharacterType, rootSkillId, itemId,
            sourceProperties, new List<FightPropertyValue> {
                new FightPropertyValue { Id = formula.PropertyId("ShieldByDefence"), Value = 5000 },
                new FightPropertyValue { Id = formula.PropertyId("SkillFixedShield"), Value = 100 }
            });
    }

    private static FightBattleStatusState FixedShield(FightDamageFormula formula, int ownerId,
        int creatorId, int creatorCharacterType, IList<FightPropertyValue> sourceProperties, long value)
    {
        return Shield(formula, ownerId, creatorId, creatorCharacterType, 90001, 90002,
            sourceProperties, new List<FightPropertyValue> {
                new FightPropertyValue { Id = formula.PropertyId("SkillFixedShield"), Value = value }
            });
    }

    private static FightBattleStatusState Shield(FightDamageFormula formula, int ownerId, int creatorId,
        int creatorCharacterType, int rootSkillId, int itemId, IList<FightPropertyValue> sourceProperties,
        List<FightPropertyValue> properties)
    {
        var storedProperties = properties.Select(value => new FightPropertyValue
            { Id = value.Id, Value = value.Value }).ToList();
        return new FightBattleStatusState
        {
            InstanceId = 2,
            ClassId = 90000 + rootSkillId,
            OwnerId = ownerId,
            CreatorId = creatorId,
            CreatorCharacterType = creatorCharacterType,
            RootSkillId = rootSkillId,
            ApplyingSkillItemId = itemId,
            ShieldKind = FightBattleShieldKind.Persistent,
            HasPropertyComponent = true,
            PropertyDefinitionResolved = true,
            SourceProperties = sourceProperties.Select(value => new FightPropertyValue
                { Id = value.Id, Value = value.Value }).ToList(),
            StatusProperties = storedProperties,
            StatusConfigProperties = properties.Select(value => new FightPropertyValue
                { Id = value.Id, Value = value.Value }).ToList(),
            ShieldInitialCount = -1,
            ShieldRemainCount = -1
        };
    }

    private static void CheckForeignShield(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, long ordinary)
    {
        var checkpoint = BaseCheckpoint(formula);
        var owner = Unit(1, 1, 1, formula);
        var creator = Unit(2, 1, 1, formula);
        owner.Statuses.Add(catalog.CreateStatus(25109, 1, 1, 1, 25109, 10593, 1, 1, 8, owner.Properties));
        var shield = DefenceShield(formula, owner.Id, creator.Id, 1, 10032, 10032, creator.Properties);
        owner.Statuses.Add(shield);
        checkpoint.Units.AddRange(new[] { owner, creator });
        FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        Check(shield.ShieldRemainValue == ordinary, "a foreign shield used its recipient's 10593 bonus");
        var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        Check(restored.Units.Single(unit => unit.Id == 1).Statuses.Single(status => status.ClassId == shield.ClassId)
            .ShieldRemainValue == ordinary, "a foreign shield gained the recipient's bonus after restore");
    }

    private static long CastAllyShield(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, bool equipPassive, int rank, int level)
    {
        var checkpoint = BaseCheckpoint(formula);
        var caster = Unit(1, 1, 1, formula);
        var ally = Unit(2, 1, 1, formula);
        var enemy = Unit(3, 2, 1, formula);
        caster.Hp = caster.Turn.Hp = 1000;
        ally.Hp = ally.Turn.Hp = 100;
        enemy.X = 18;
        if (equipPassive)
            caster.Statuses.Add(catalog.CreateStatus(25109, 1, 1, 1, 25109, 10593, rank, level, 8, caster.Properties));
        var skill = catalog.CreateSkill(11170, 0, 10032, 2, 20, 8,
            catalog.CalculateSkillProps(11170, 2, 20, 8).Select(pair =>
                new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        skill.ResetCooldownAtStart = true;
        caster.Skills.Add(skill);
        caster.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
        checkpoint.Units.AddRange(new[] { caster, ally, enemy });
        var wave = new FightBattleWaveState();
        wave.UnitIds.Add(enemy.Id);
        checkpoint.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        FightBattleStatusState shield = null;
        for (int i = 0; i < 6 && shield == null; i++)
        {
            executor.Step(checkpoint.Sequence, checkpoint.Counter + 1);
            shield = ally.Statuses.Find(status => status.ClassId == 24180);
        }
        Check(shield != null && shield.CreatorId == caster.Id && shield.CreatorCharacterType == 1,
            "the original ally-targeting skill did not preserve the shield's player origin");
        long value = shield.ShieldRemainValue;
        Check(value > 0 && caster.Statuses.All(status => status.ClassId != 24180),
            "the original shield missed the ally with lowest HP percentage");
        var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
        Check(copy.Units.Single(unit => unit.Id == ally.Id).Statuses.Single(status => status.ClassId == 24180)
            .ShieldRemainValue == value, "the actual allied shield changed after restore");
        return value;
    }

    private static void CheckFixedShield(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        var checkpoint = BaseCheckpoint(formula);
        var owner = Unit(1, 1, 1, formula);
        var passive = catalog.CreateStatus(25109, 1, 1, 1, 25109, 10593, 1, 1, 8, owner.Properties);
        owner.Statuses.Add(passive);
        FightStatusPropertyRuntime.RebuildAll(new List<FightBattleUnitState> { owner }, catalog, formula);
        var fixedShield = FixedShield(formula, 1, 1, 1, owner.Properties, 500);
        owner.Statuses.Add(fixedShield);
        checkpoint.Units.Add(owner);
        FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        Check(fixedShield.ShieldRemainValue == 500, "fixed-value shield received the defence shield bonus");
    }

    private static Dictionary<int, long> Values(IList<FightPropertyValue> properties)
    {
        return (properties ?? new List<FightPropertyValue>()).ToDictionary(value => value.Id, value => value.Value);
    }

    private static FightBattleCheckpoint BaseCheckpoint(FightDamageFormula formula)
    {
        return new FightBattleCheckpoint { StageId = 1, MaxRound = 8, RandomSeed = 71,
            Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
    }

    private static FightBattleUnitState Unit(int id, int side, int characterType, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = characterType,
            Profession = 4, Level = 70, SubRank = 8, X = id * 2, Y = 4, Hp = 800, MaxHp = 1000, LastMaxHp = 1000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = 1000 - id;
        foreach (var pair in new Dictionary<string, long> { { "Attack", 1000 }, { "Defence", 200 },
            { "MaxHp", 1000 }, { "Speed", unit.Turn.Speed }, { "BaseCureAdd", 10000 }, { "BaseBeCureAdd", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
