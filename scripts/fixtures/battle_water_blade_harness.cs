using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleWaterBladeHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Water blade: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int item in new[] { 10082, 10184 })
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        foreach (long attack in new[] { 1000L, 100000L })
        foreach (string mode in new[] { "normal", "overkill", "partial-shield", "full-shield" })
        {
            try
            {
                int entity = item == 10082 ? 11980 : 13580;
                int shieldId = item == 10082 ? 24377 : 24659;
                var actor = Unit(1, 1, 6, 6, 1000, attack, formula);
                var target = Unit(2, 2, 6 + direction[0], 6 + direction[1], 1000000, 100, formula);
                if (mode == "overkill") target.Hp = target.Turn.Hp = 100;
                if (mode.EndsWith("shield", StringComparison.Ordinal))
                    target.Statuses.Add(catalog.CreateStatus(24377, target.Id, target.Id, 9024377,
                        customShieldValue: mode == "full-shield" ? 10000000 : 100));
                var checkpoint = new FightBattleCheckpoint {
                    StageId = 1, MaxRound = 8, RandomSeed = 91,
                    Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 }
                };
                checkpoint.Units.AddRange(new[] { actor, target });
                var wave = new FightBattleWaveState(); wave.UnitIds.Add(target.Id); checkpoint.Waves.Add(wave);
                var skill = catalog.CreateSkill(entity, 0, item, 5, 20, 8,
                    catalog.CalculateSkillProps(entity, 5, 20, 8)
                        .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
                var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
                var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
                FightBattleStepEvent step = null;
                FightBattleSkillCastEvent cast = null;
                while (cast == null && !checkpoint.Finished && checkpoint.Counter < 8)
                {
                    step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
                    resumed.Step(copy.Sequence, copy.Counter + 1);
                    cast = step.SkillCasts.SingleOrDefault(value => value.SkillItemId == item);
                }
                Check(cast != null, "original skill was not selected");
                var hit = step.DamageEvents.Single(value => value.SkillEntityId == cast.SkillEntityId);
                float delay = cast.SkillEntityId == 11983 ? .57f : cast.SkillEntityId == 11984 ? .72f
                    : item == 10082 ? .55f : .5833f;
                Check(hit.SourceId == actor.Id && hit.TargetId == target.Id && hit.HitEntityId == entity + 1
                    && hit.DamageEntityId == entity + 2 && hit.ElementType == 101 && hit.DamageType == 1
                    && !hit.TrueDamage && !hit.IsCure
                    && Math.Abs(hit.Delay - cast.Delay - delay) < .0001f,
                    "water hit identity or authored timing differs");
                long actualLoss = hit.BeforeHp - hit.AfterHp;
                long expected = Math.Min((long)(actualLoss * (double).2f), actor.MaxHp / 2);
                var shields = actor.Statuses.Where(value => value.ClassId == shieldId).ToArray();
                Check(shields.Sum(value => value.ShieldRemainValue) == expected
                    && shields.All(value => value.CreatorId == actor.Id && value.DurationRound == 2
                        && value.CustomShieldValue <= actor.MaxHp / 2),
                    "shield must use actual HP loss, exclude overkill/absorbed damage and cap at half caster max HP");
                Check(mode != "full-shield" || actualLoss == 0 && expected == 0,
                    "a fully absorbed hit granted a shield");
                Check(mode != "overkill" || actualLoss == 100 && expected == 20,
                    "lethal overflow was included in shield conversion");
                Check(mode != "normal" || attack != 100000 || actualLoss > actor.MaxHp / 2 && expected == actor.MaxHp / 2,
                    "the shield limit incorrectly truncated the original damage");
                Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(copy),
                    "restoring before the cast changed damage or shield conversion");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(saved);
                Check(FightBattleCheckpointSerializer.Save(restored) == saved,
                    "shield amount, duration or custom value limit was lost on disk");
                Check(executor.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(checkpoint) == saved, "receipt replay converted the damage twice");
                cases++;
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Water blade case " + item + "/" + attack + "/" + mode
                    + "/" + string.Join(",", direction) + ": " + error);
            }
        }
        return "Water blade passed: " + cases + " direction, variant, HP-only conversion, cap, timing and restore cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, long hp, long attack,
        FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Profession = 4, Level = 70, SubRank = 8,
            X = x, Y = y, Hp = hp, MaxHp = hp, LastMaxHp = hp
        };
        unit.Turn.Id = id; unit.Turn.Hp = hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = attack });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = hp });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed });
        return unit;
    }
}
