using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Zjcs.Combat.Official;

public static class BattleHitTriggerOriginHarness
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, string output)
    {
        Directory.CreateDirectory(output);
        int cases = 0;
        foreach (int facing in new[] { 0, 1, 2, 3 })
        foreach (bool statusOnly in new[] { false, true })
        {
            var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = 11,
                Grid = new FightBattleGrid { Width = 20, Height = 20 } };
            var source = Unit(1, 8, 8, facing);
            var offset = new[] { new FightGridPoint(0, -3), new FightGridPoint(-3, 0),
                new FightGridPoint(0, 3), new FightGridPoint(3, 0) }[facing];
            var target = Unit(2, 8 + offset.X, 8 + offset.Y, 0);
            target.BodyOffsets.AddRange(new[] { new FightGridPoint(1, 0), new FightGridPoint(-1, 0),
                new FightGridPoint(0, 1), new FightGridPoint(0, -1) });
            var skill = catalog.CreateSkill(13520, 0, 0, 5, 20, 1, null);
            if (statusOnly) skill.HitGraphEntities.Single(h => h.EntityId == 13522).ActionType = 0;
            source.Skills.Add(skill); source.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            cp.Units.AddRange(new[] { source, target });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var step = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items).Step(0, 1).Event;
            var parent = step.SkillCasts.Single(c => c.SkillEntityId == 13520);
            var children = step.SkillCasts.Where(c => c.SkillEntityId == 13523).ToArray();
            var triggers = step.DamageEvents.Where(d => d.HitEntityId == 13522).ToArray();
            Check(children.Length > 0 && children.Length == triggers.Length, "One child per actual original trigger.");
            Check(triggers.All(d => d.DamageEntityId == 0 && d.HasDamagePosition), "Trigger has no damage view entity.");
            Check(triggers.All(d => d.HasAbsoluteDamage != statusOnly), "Damage and None branches both trigger.");
            var positions = new List<FightGridPoint>();
            foreach (var selection in parent.HitSelections.Where(s => s.HitEntityId == 13522))
                foreach (var occurrence in selection.Targets)
                    positions.Add(selection.EffectCenters[occurrence.EffectCenterIndex]);
            Check(positions.Any(p => p.X != target.X || p.Y != target.Y), "Real random draw includes a body cell away from the centre.");
            Check(positions.Count == children.Length, "Every actual hit centre has a child cast.");
            foreach (var child in children)
            {
                var pose = child.Presentation;
                int index = positions.FindIndex(p => p.X == pose.EntityX && p.Y == pose.EntityY);
                Check(pose.HasEntityPosition && index >= 0, "Child stays at its actual hit entity, including body cells.");
                positions.RemoveAt(index);
                Check(pose.TargetX == pose.EntityX && pose.TargetY == pose.EntityY, "Child target anchor uses the same frozen origin.");
                Check(pose.SourceX == parent.Presentation.SourceX && pose.SourceY == parent.Presentation.SourceY,
                    "The source role is not moved to its child's origin.");
                Check(pose.FacingIndex == parent.Presentation.FacingIndex && pose.FlipX == parent.Presentation.FlipX,
                    "Hit direction and flip survive child creation.");
                Check(child.HitSelections.All(s => s.FacingIndex == pose.FacingIndex && s.FlipX == pose.FlipX),
                    "The rule executor uses the same orientation as the original view.");
                Check(step.DamageEvents.Where(d => child.DamageEventIds.Contains(d.DamageId)).All(d => d.Delay >= child.Delay),
                    "Child damage cannot precede its trigger.");
            }
            Check(children.Select(c => c.Delay).OrderBy(x => x).SequenceEqual(triggers.Select(d => d.Delay).OrderBy(x => x)),
                "Child creation time is the actual parent trigger time.");
            Check(step.DamageEvents.Any(d => d.DamageEntityId == 13526), "The child's original area actually hits the large target.");
            string json = FightBattleCheckpointSerializer.Save(cp);
            Check(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(json)) == json, "Full checkpoint round trip.");
            string old = Regex.Replace(json, "\"HasEntityPosition\":(?:true|false),", "");
            old = Regex.Replace(old, "\"Entity[XY]\":-?[0-9]+,", "");
            Check(FightBattleCheckpointSerializer.Load(old).Receipts.SelectMany(e => e.Event.SkillCasts)
                .All(c => c.Presentation == null || !c.Presentation.HasEntityPosition), "Old receipts keep their source-origin default.");
            File.WriteAllText(Path.Combine(output, facing + (statusOnly ? "-none" : "-damage") + ".json"), json);
            cases++;
        }
        Check(cases == 8, "Eight direction/result cases.");
    }
    private static FightBattleUnitState Unit(int id, int x, int y, int facing)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = id == 1 ? 1 : 2,
            X = x, Y = y, Hp = 10000000, MaxHp = 10000000, Level = 150, SubRank = 1, MoveMask = 5,
            Direction = facing, HasFlipX = true, FlipX = facing == 1,
            Turn = new FightTurnUnit { Id = id, Hp = 10000000, Speed = 100 - id } };
        foreach (var pair in new[] { new[] { 1, 10001 }, new[] { 2, 10003 }, new[] { 3, 10000000 }, new[] { 4, 100 - id } })
            unit.Properties.Add(new FightPropertyValue { Id = pair[0], Value = pair[1] });
        return unit;
    }
}
