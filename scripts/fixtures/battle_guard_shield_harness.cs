using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleGuardShieldHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Guard shield: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int[] growth in new[] { new[] { 1, 1, 1 }, new[] { 2, 20, 5 }, new[] { 7, 30, 8 } })
        foreach (long defence in new[] { 10L, 1000L, 5000L })
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 20, RandomSeed = 17,
                Grid = new FightBattleGrid { Width = 20, Height = 20 } };
            var player = Unit(1, defence); var enemy = Unit(2, 10);
            cp.Units.AddRange(new[] { player, enemy });
            var guard = catalog.CreateSkill(11160, 0, 10046, growth[0], growth[1], growth[2],
                catalog.CalculateSkillProps(11160, growth[0], growth[1], growth[2])
                    .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            guard.LimitedTimes = 1;
            player.Skills.Add(guard); player.SkillState.Slots.Add(catalog.CreateSkillSlot(guard, 0));
            var slash = catalog.CreateSkill(10600, 0, 10008, 2, 2, enemy.SubRank,
                catalog.CalculateSkillProps(10600, 2, 2, enemy.SubRank)
                    .Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            slash.LimitedTimes = 1;
            enemy.Skills.Add(slash); enemy.SkillState.Slots.Add(catalog.CreateSkillSlot(slash, 0));
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var cast = executor.Step(cp.Sequence, cp.Counter + 1).Event;
            Check(cast.SkillCasts.Count(x => x.SkillEntityId == 11160 && x.ActorId == player.Id) == 1,
                "initial cooldown did not release the official self shield");
            var shield = player.Statuses.Single(x => x.ClassId == 24170);
            var raw = catalog.CalculateSkillProps(24170, growth[0], growth[1], growth[2])
                .ToDictionary(p => p.Key, p => p.Value);
            long expected = (long)formula.ReadProperty(raw, formula.PropertyId("SkillFixedShield"))
                + (long)(defence * formula.ReadProperty(raw, formula.PropertyId("ShieldByDefence")));
            Check(expected > 0 && shield.ShieldValue == expected && shield.ShieldRemainValue == expected,
                "shield differs from original fixed plus defence formula");
            Check(shield.OwnerId == player.Id && shield.CreatorId == player.Id && shield.DurationRound == 2
                && shield.SourceSkillRank == growth[0] && shield.SourceSkillLevel == growth[1]
                && shield.SourceSubRank == growth[2], "shield lost its owner, duration or cultivation");
            string initial = FightBattleCheckpointSerializer.Save(cp);
            var copy = FightBattleCheckpointSerializer.Load(initial);
            var restored = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            var control = FightBattleCheckpointSerializer.Load(initial);
            control.Units.Single(x => x.Id == player.Id).Statuses.RemoveAll(x => x.ClassId == 24170);
            var plain = FightBattleExecutor.Restore(control, formula, new FightDamageSettlement(), catalog, items);
            var blocked = executor.Step(cp.Sequence, cp.Counter + 1).Event;
            restored.Step(copy.Sequence, copy.Counter + 1);
            var unshielded = plain.Step(control.Sequence, control.Counter + 1).Event;
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(copy),
                "restored shield or its damage absorption diverged");
            var hit = blocked.DamageEvents.Single(x => x.SourceId == enemy.Id && x.TargetId == player.Id && x.SkillEntityId == 10600);
            var originalHit = unshielded.DamageEvents.Single(x => x.SourceId == enemy.Id && x.TargetId == player.Id && x.SkillEntityId == 10600);
            Check(originalHit.Damage < 0 && hit.ShieldDamage == Math.Min(expected, -originalHit.Damage)
                && -hit.Damage + hit.ShieldDamage == -originalHit.Damage,
                "actual incoming damage was not absorbed by the original shield amount");
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(cp.Sequence, cp.Counter).Replayed && saved == FightBattleCheckpointSerializer.Save(cp),
                "replaying a receipt changed the shield");
            cases++;
        }
        return "Guard shield passed: " + cases + " original defence/cultivation, absorption, restoration and replay cases.";
    }

    private static FightBattleUnitState Unit(int id, long defence)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1, Profession = 2,
            Level = 50, SubRank = 5, X = id + 4, Y = 5, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
        foreach (var p in new Dictionary<int, long> { { 1, 1000 }, { 2, defence }, { 3, 1000000 }, { 4, unit.Turn.Speed } })
            unit.Properties.Add(new FightPropertyValue { Id = p.Key, Value = p.Value });
        return unit;
    }
}
