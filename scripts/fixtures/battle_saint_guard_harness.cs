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
        }
        Check(CheckShield(catalog, formula, items, true, 2, 1, 1, 10032, 10032) == ordinary,
            "a teammate-applied defence shield received the owner's 10593 bonus");
        Check(CheckShield(catalog, formula, items, true, 1, 1, 5, 10032, 10032) == ordinary,
            "a pet shield with a shared owner id received the 10593 bonus");
        Check(CheckShield(catalog, formula, items, true, 1, 1, 0, 10032, 10032) == ordinary,
            "an unknown shield creator role received the 10593 bonus");
        CheckFixedShield(catalog, formula, items);
        return "Saint guard defence-shield formula, self active/passive/weapon sources, teammate/pet exclusions, fixed-shield filter and checkpoint cases passed.";
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
        bool selfPlayerShield = shield.OwnerId == shield.CreatorId
            && creator.CharacterType == 1 && shield.CreatorCharacterType == 1;
        if (!selfPlayerShield)
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
