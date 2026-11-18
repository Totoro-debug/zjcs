using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zjcs.Combat.Official;

public static class BattleImpactGateHarness
{
    private sealed class Pending
    {
        public FightBattlePendingImpact Impact;
        public ManualResetEventSlim Released = new ManualResetEventSlim(false);
    }
    private sealed class Gate : IFightBattleImpactGate, IFightBattleCastPreparationObserver
    {
        public readonly BlockingCollection<Pending> Items = new BlockingCollection<Pending>();
        public readonly ConcurrentQueue<FightBattleSkillCastEvent> Prepared = new ConcurrentQueue<FightBattleSkillCastEvent>();
        public void CastPrepared(FightBattleSkillCastEvent cast) { Prepared.Enqueue(cast); }
        public void AwaitImpact(FightBattlePendingImpact impact)
        {
            var pending = new Pending { Impact = impact };
            Items.Add(pending);
            if (!pending.Released.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("The impact was never released by the presentation.");
        }
        public Pending Next()
        {
            if (!Items.TryTake(out var pending, TimeSpan.FromSeconds(20)))
                throw new TimeoutException("The expected impact was not emitted.");
            return pending;
        }
    }
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Impact gate: " + message); }
    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = 1000000, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var value in new Dictionary<int, long> { {1, 1000}, {2, 10}, {3, unit.MaxHp}, {4, unit.Turn.Speed} })
            unit.Properties.Add(new FightPropertyValue { Id = value.Key, Value = value.Value });
        foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
        return unit;
    }
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int impacts = 0;
        foreach (var setup in new[] { new[] {10011,10630,1,3}, new[] {10015,10670,2,1}, new[] {10028,10800,3,1} })
        {
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); actor.Direction = 3;
            var target = Unit(2, 2, 7 + setup[2], 7, formula);
            var skill = catalog.CreateSkill(setup[1], 0, setup[0], 2, 20, 1,
                catalog.CalculateSkillProps(setup[1], 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            checkpoint.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); checkpoint.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var baseline = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var ordinary = FightBattleExecutor.Restore(baseline, formula, new FightDamageSettlement(), catalog, items);
            ordinary.Step(0, 1);
            var gate = new Gate(); executor.ImpactGate = gate;
            var step = Task.Run(() => executor.Step(0, 1));
            long previousHp = target.Hp;
            for (int index = 0; index < setup[3]; index++)
            {
                var pending = gate.Next();
                try
                {
                    var impact = pending.Impact;
                    Check(impact.SourceId == 1 && impact.TargetId == 2 && impact.SkillItemId == setup[0]
                        && impact.SkillEntityId == setup[1] && impact.HitEntityId > 0 && impact.DamageEntityId > 0
                        && impact.Cast != null && impact.HitSelection != null,
                        "The native hit lacks its original cast, hit, target or location");
                    Check(gate.Prepared.Any(cast => ReferenceEquals(cast, impact.Cast)),
                        "The impact was requested before its cast preparation notification");
                    Check(target.Hp == previousHp, "HP changed before the presentation released the pending impact");
                    Check(!step.Wait(50) && target.Hp == previousHp, "A paused impact settled without its presentation event");
                }
                finally { pending.Released.Set(); }
                if (index + 1 < setup[3])
                {
                    var next = gate.Next();
                    Check(target.Hp < previousHp, "A released hit did not settle before the next impact");
                    previousHp = target.Hp;
                    gate.Items.Add(next);
                }
                impacts++;
            }
            Check(step.Wait(TimeSpan.FromSeconds(20)), "The final impact did not finish the action");
            var actual = step.Result;
            Check(actual.Event.DamageEvents.Count(d => d.SkillItemId == setup[0]) == setup[3], "A hit was lost or duplicated");
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(baseline),
                "Gating changed the original deterministic result");
        }
        foreach (bool removed in new[] { false, true })
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); var target = Unit(2, 2, 8, 7, formula);
            var skill = catalog.CreateSkill(10600, 0, 10008, 2, 20, 1,
                catalog.CalculateSkillProps(10600, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.CostType = 1; skill.SkillCost = 100;
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var gate = new Gate(); executor.ImpactGate = gate; var task = Task.Run(() => executor.Step(0, 1));
            var pending = gate.Next();
            Check(gate.Prepared.TryPeek(out var started) && ReferenceEquals(started, pending.Impact.Cast)
                && started.HasSkillCostHp && started.SkillCostBeforeHp == 1000000
                && started.SkillCostAfterHp == 999900 && actor.Hp == 999900,
                "Cast preparation must follow original HP cost and precede the pending hit");
            target.Hp = target.Turn.Hp = removed ? 0 : 500000;
            pending.Released.Set();
            Check(task.Wait(TimeSpan.FromSeconds(20)), "Changed target did not finish the action");
            var hits = task.Result.Event.DamageEvents.Where(d => d.SkillItemId == 10008 && !d.IsSkillCost).ToArray();
            Check(removed ? hits.Length == 0 : hits.Length == 1 && hits[0].BeforeHp == 500000 && hits[0].AfterHp < 500000,
                "The impact did not use the target's live state when the presentation reached it");
        }
        return "Impact gate passed: " + impacts + " held impacts across three-hit combo, jump slash and fireball; exact ungated results preserved.";
    }
}
