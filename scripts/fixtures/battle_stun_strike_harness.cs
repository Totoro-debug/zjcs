using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStunStrikeHarness
{
    static void Check(bool value, string text) { if (!value) throw new InvalidOperationException("Stun strike: " + text); }
    public static string Run(FightOfficialCombatCatalog c, FightDamageFormula f, FightOfficialSkillItemCatalog items)
    {
        int neutral = 0, forced = 0, resisted = 0, saved = 0;
        foreach (string mode in new[] { "neutral", "forced", "resisted" })
        for (int index = 1; index <= 64; index++)
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 10, RandomSeed = index * 104729 + 17,
                Grid = new FightBattleGrid { Width = 20, Height = 20 } };
            var actor = Unit(1, f); var target = Unit(2, f); cp.Units.AddRange(new[] { actor, target });
            if (mode == "forced") actor.Properties.Add(new FightPropertyValue { Id = f.PropertyId("EffectRateValue"), Value = 10000 });
            if (mode == "resisted") target.Properties.Add(new FightPropertyValue { Id = f.PropertyId("EffectDodgeValue"), Value = 10000 });
            var skill = Skill(c, 10620, 10010); skill.LimitedTimes = 1;
            var definition = FightBattleHitCollector.CollectAll(skill).Single();
            var application = definition.AppliedStatuses.Single();
            Check(definition.EntityId == 10621 && definition.DamageId == 10622 && definition.ActionType == 4
                && application.StatusId == 24010 && application.TargetType == 2 && application.AffectedByProp
                && Math.Abs(application.Chance - .4f) < .000001f, "official stun hit configuration differs");
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = 1 });
            var slash = Skill(c, 10600, 10008);
            target.Skills.Add(slash); target.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, Cooldown = 4, LastRound = 0, HasInitializedLastRound = true });
            var executor = FightBattleExecutor.Begin(cp, f, new FightDamageSettlement(), c, items);
            double rate = c.CalculateStatusApplicationRate(actor, target, 24010, .4, true, f);
            Check(mode == "neutral" ? Math.Abs(rate - .4) < .000001 : mode == "forced" ? rate >= 1 : rate <= 0,
                "original base chance or effect-stat boundary differs: " + mode + "=" + rate);
            var first = executor.Step(cp.Sequence, cp.Counter + 1).Event;
            var hit = first.DamageEvents.Single(x => x.SkillEntityId == 10620 && x.SourceId == 1 && x.TargetId == 2);
            Check(hit.Damage < 0, "official physical hit did not damage its target");
            var stun = target.Statuses.SingleOrDefault(x => x.ClassId == 24010);
            bool applied = stun != null;
            if (mode == "forced") { Check(applied, "guaranteed stun failed"); forced++; }
            if (mode == "resisted") { Check(!applied, "fully resisted stun applied"); resisted++; }
            if (mode == "neutral" && applied) neutral++;
            if (!applied) continue;
            Check(stun.StatusType == 3 && stun.DurationRound == 1, "original one-round abnormal stun differs");
            string json = FightBattleCheckpointSerializer.Save(cp);
            var copy = FightBattleCheckpointSerializer.Load(json);
            var resume = FightBattleExecutor.Restore(copy, f, new FightDamageSettlement(), c, items);
            long before = new FightSkillState(target.SkillState).LeftCooldown(0);
            int x = target.X, y = target.Y;
            var next = executor.Step(cp.Sequence, cp.Counter + 1).Event;
            resume.Step(copy.Sequence, copy.Counter + 1);
            Check(next.ActorId == target.Id && !next.SkillCasts.Any(xc => xc.ActorId == target.Id)
                && target.X == x && target.Y == y && new FightSkillState(target.SkillState).LeftCooldown(0) == before,
                "stun did not stop the target's movement, skill and cooldown for its turn");
            Check(!target.Statuses.Any(xc => xc.ClassId == 24010), "one-round stun did not expire after the target turn");
            Check(FightBattleCheckpointSerializer.Save(cp) == FightBattleCheckpointSerializer.Save(copy), "restored stun turn diverged");
            saved++;
        }
        Check(neutral > 8 && neutral < 56, "the base chance did not produce both successes and failures");
        return "Stun strike: neutral=" + neutral + "/64 forced=" + forced + " resisted=" + resisted + " restored=" + saved;
    }
    static FightBattleSkillDefinition Skill(FightOfficialCombatCatalog c, int entity, int item)
        => c.CreateSkill(entity, 0, item, 2, 20, 5, c.CalculateSkillProps(entity, 2, 20, 5)
            .Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
    static FightBattleUnitState Unit(int id, FightDamageFormula f)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1, Profession = 2,
            Level = 50, SubRank = 5, X = id + 4, Y = 5, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
        foreach (var p in new Dictionary<int, long> { {1, 1000}, {2, 10}, {3, 1000000}, {4, unit.Turn.Speed},
            {f.PropertyId("BaseEffectRate"),10000}, {f.PropertyId("BaseEffectDodge"),10000} })
            unit.Properties.Add(new FightPropertyValue { Id = p.Key, Value = p.Value });
        return unit;
    }
}
