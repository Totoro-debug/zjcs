using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Zjcs.Combat.Official;

public static class BattleDamagePoseReceiptsHarness
{
    private static readonly FieldInfo Known = typeof(FightBattleDamageEvent).GetField("HasDamagePosition");
    private static readonly FieldInfo X = typeof(FightBattleDamageEvent).GetField("DamageX");
    private static readonly FieldInfo Y = typeof(FightBattleDamageEvent).GetField("DamageY");
    private static readonly Regex NewFields = new Regex("\\\"(?:HasDamagePosition|DamageX|DamageY)\\\":(?:true|false|-?[0-9]+),");
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, string output, bool baseline)
    {
        Check((Known == null) == baseline, "Compile actual pre-change sources for comparison.");
        Directory.CreateDirectory(output);
        foreach (string name in new[] { "point", "point-displacement", "body-10910", "body-10920", "zero", "cure-full", "cure-effective" })
        {
            var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = 11,
                Grid = new FightBattleGrid { Width = 16, Height = 16 } };
            var source = Unit(1, 5); var target = Unit(2, name.StartsWith("body") ? 8 : 6);
            var other = Unit(3, 12);
            FightBattleSkillDefinition skill;
            if (name.StartsWith("body"))
            {
                skill = catalog.CreateSkill(int.Parse(name.Substring(5)), 0, 0, 5, 20, 1, null);
                target.BodyOffsets.AddRange(new[] { new FightGridPoint(1, 0), new FightGridPoint(0, 1), new FightGridPoint(1, 1) });
            }
            else
            {
                skill = new FightBattleSkillDefinition { EntityId = 930, Slot = 0, TargetType = 1,
                    Duration = 1f, ActionDuration = 1f };
                skill.Range.Add(new FightGridPoint(1, 0));
                var hit = new FightBattleHitDefinition { EntityId = 931, DamageId = 932, DamageType = 8,
                    HasCustomDamage = true, CustomDamage = name == "zero" ? 0 : 5, TargetMode = 0 };
                if (name == "point-displacement") hit.HitMove = new FightBattleSkillMove {
                    Distance = 1, Direction = 1, BasePercent = 1f, AffectedByProp = false };
                if (name.StartsWith("cure"))
                {
                    hit.ActionType = 5; skill.HasCustomCure = true; skill.CustomCure = 10;
                    if (name == "cure-effective") { target.Hp -= 100; target.Turn.Hp = target.Hp; }
                    foreach (var unit in new[] { source, target })
                        foreach (string prop in new[] { "BaseCureAdd", "BaseBeCureAdd" })
                            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(prop), Value = 10000 });
                }
                skill.Hits.Add(hit);
            }
            source.Skills.Add(skill); source.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            cp.Units.AddRange(new[] { source, target, other });
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3 }); cp.Waves.Add(wave);
            var step = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items).Step(0, 1).Event;
            var damage = step.DamageEvents.Where(d => !d.IsSkillCost).ToArray();
            Check(damage.Length > 0, name + ": no actual settled occurrence; casts=" + step.SkillCasts.Count + ".");
            if (!baseline)
            {
                foreach (var receipt in damage)
                {
                    Check((bool)Known.GetValue(receipt), name + ": position not captured.");
                    int x = (int)X.GetValue(receipt), y = (int)Y.GetValue(receipt);
                    if (receipt.HasHitGridPosition) Check(x == receipt.HitGridX && y == receipt.HitGridY, "Body cell was replaced by entity center.");
                    else Check(x == (name.StartsWith("body") ? (receipt.TargetId == 2 ? 8 : 12) : 6) && y == 5,
                        name + ": target=" + receipt.TargetId + " position=" + x + "," + y + "; point position was replaced by later state.");
                    if (name.StartsWith("cure")) Check(receipt.HasAbsoluteDamage && receipt.AbsoluteDamage == 10
                        && receipt.DamageValue == 10 && receipt.Damage == (name == "cure-full" ? 0 : 10),
                        "Original absolute cure must survive overheal clamping without recalculation.");
                    if (name == "zero") Check(receipt.HasAbsoluteDamage && !receipt.IsCure && receipt.DamageValue == 0,
                        "A real zero-damage settlement must remain distinct from a status-only occurrence.");
                }
                if (name == "point-displacement") Check(target.X != 6 || target.Y != 5, "Displacement fixture did not move target.");
                if (name == "body-10910") Check(damage.Any(d => d.HasHitGridPosition), "Original 10910 body-scope fixture did not strike a body cell.");
            }
            string json = FightBattleCheckpointSerializer.Save(cp);
            var restored = FightBattleCheckpointSerializer.Load(json);
            Check(FightBattleCheckpointSerializer.Save(restored) == json, name + ": checkpoint round trip changed receipts.");
            if (!baseline)
            {
                var legacy = FightBattleCheckpointSerializer.Load(NewFields.Replace(json, ""));
                Check(legacy.Receipts.SelectMany(r => r.Event.DamageEvents).All(d => !(bool)Known.GetValue(d)),
                    "Legacy position must remain explicitly unknown.");
            }
            // The only existing-field change is recording the already-computed
            // absolute cure. Remove it from both versions solely for comparison.
            foreach (var receipt in restored.Receipts.SelectMany(r => r.Event.DamageEvents).Where(d => d.IsCure))
            { receipt.HasAbsoluteDamage = false; receipt.AbsoluteDamage = 0; }
            File.WriteAllText(Path.Combine(output, name + ".json"), NewFields.Replace(FightBattleCheckpointSerializer.Save(restored), ""));
        }
    }

    private static FightBattleUnitState Unit(int id, int x)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id == 1 ? 1 : 2, CharacterType = id == 1 ? 1 : 2,
            X = x, Y = 5, Hp = 1000000, MaxHp = 1000000, Level = 150, SubRank = 1, MoveMask = 5,
            Direction = 3, HasFlipX = true, Turn = new FightTurnUnit { Id = id, Hp = 1000000, Speed = 100 - id } };
        foreach (var pair in new[] { new[] { 1, 10001 }, new[] { 2, 10003 }, new[] { 3, 1000000 }, new[] { 4, 100 - id } })
            unit.Properties.Add(new FightPropertyValue { Id = pair[0], Value = pair[1] });
        return unit;
    }
}
