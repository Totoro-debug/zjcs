using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleRoundForbiddenRegression
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        Check(catalog.CreateStatus(25117, 1, 1).RoundStartRulesSupported,
            "Original 25117 remains blocked.");
        foreach (bool blocked in new[] { false, true })
        foreach (bool foreignOwner in new[] { false, true })
        foreach (bool failedAttempt in new[] { false, true })
        {
            string direct = Exercise(catalog, formula, items, false, false, blocked, foreignOwner, failedAttempt);
            Check(direct == Exercise(catalog, formula, items, true, false, blocked, foreignOwner, failedAttempt),
                "Forbidden round continuation changed after restoration.");
            Check(direct == Exercise(catalog, formula, items, true, true, blocked, foreignOwner, failedAttempt),
                "Legacy 25117 did not restore its definition without losing actual calls/quota.");
        }
        foreach (int max in new[] { 0, 1 })
            Check(Exercise(catalog, formula, items, false, false, false, false, false, max)
                == Exercise(catalog, formula, items, true, false, false, false, false, max),
                "Extra attempt bypassed the configured finite invocation limit.");
        var unit = new FightBattleUnitState { Id = 1 };
        var source = catalog.CreateStatus(25117, 1, 1);
        source.ForbiddenOnApplicatorDie = false;
        unit.Statuses.Add(source);
        Check(!FightStatusLifecycle.SetForbidden(unit, source, FightStatusLifecycle.ForbiddenApplicatorDie, true)
            && source.ForbiddenType == 0, "An exempt status acquired the death forbidden reason.");
    }

    private static string Exercise(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, bool restore, bool legacy, bool blocked,
        bool foreignOwner, bool failedAttempt, int maxCount = -1)
    {
        var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 10, RandomSeed = 23,
            Grid = new FightBattleGrid { Width = 12, Height = 8, DefaultGroundType = 2 } };
        for (int id = 1; id <= 2; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id, CharacterType = 1,
                X = id + 2, Y = 2, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000,
                Level = 1, SubRank = 1 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp });
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed });
            var skill = new FightBattleSkillDefinition { EntityId = 990200 + id, ItemId = 990200 + id,
                Slot = 0, TargetType = 1, ActionDuration = 1, Duration = 1, CanTriggerChild = true };
            skill.Range.Add(new FightGridPoint(1, 0)); skill.Range.Add(new FightGridPoint(-1, 0));
            for (int hit = 0; hit < 2; hit++)
                skill.Hits.Add(new FightBattleHitDefinition { EntityId = 990300 + id,
                    DamageId = 990300 + id, DamageType = 8, HasCustomDamage = true,
                    CustomDamage = 7, Delay = 0.1f * (hit + 1) });
            unit.Skills.Add(skill);
            unit.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            cp.Units.Add(unit);
        }
        int ownerId = foreignOwner ? 2 : 1;
        var status = catalog.CreateStatus(25117, ownerId, 1, 9925117, 25117, 0, 5, 20, 1);
        status.RoundStartMaxCount = maxCount;
        if (foreignOwner) status.RoundStartListenTarget = FightStatusLifecycle.TargetCreator;
        cp.Units.Single(u => u.Id == ownerId).Statuses.Add(status);
        if (blocked) status.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
        var wave = new FightBattleWaveState { Index = 0 };
        wave.UnitIds.Add(2); cp.Waves.Add(wave);
        var settlement = new FightDamageSettlement();
        int ownHits = 0, otherHits = 0;
        settlement.SourceHit += damage =>
        {
            var owner = cp.Units.Single(u => u.Id == ownerId);
            var current = owner.Statuses.Single(s => s.ClassId == 25117);
            int before = current.RoundStartTriggeredCount;
            bool miss = failedAttempt && cp.Units[0].SkillState.Round == 1
                && damage.SourceId == 1 && ownHits == 0;
            if (miss) current.RoundStartRate = 0;
            FightStatusLifecycle.SetForbidden(owner, current, FightStatusLifecycle.ForbiddenMutex, true);
            Check(!FightStatusLifecycle.SetForbidden(owner, current, FightStatusLifecycle.ForbiddenStatusTrigger, true),
                "Adding a second reason emitted an overall forbidden transition.");
            Check(!FightStatusLifecycle.SetForbidden(owner, current, FightStatusLifecycle.ForbiddenMutex, false)
                && current.ForbiddenType == FightStatusLifecycle.ForbiddenStatusTrigger
                && current.RoundStartTriggeredCount == before,
                "Partial release retriggered a still-forbidden status.");
            FightStatusLifecycle.SetForbidden(owner, current, FightStatusLifecycle.ForbiddenStatusTrigger, false);
            if (damage.SourceId == 1)
            {
                ownHits++;
                bool first = ownHits % 2 == 1;
                Check(current.RoundStartTriggeredCount == before
                    + (first && !miss && (maxCount < 0 || before < maxCount) ? 1 : 0),
                    "Full release lost its extra invocation or allowed a second attempt in one round.");
                Check(current.RoundStartNotForbiddenListenerId == 1
                    && current.RoundStartNotForbiddenRound == cp.Units[0].SkillState.Round,
                    "Extra attempt was not recorded before its effects.");
                if (!miss && current.RoundStartTriggeredCount > 0 && (maxCount < 0 || before < maxCount))
                {
                    var applied = owner.Statuses.Single(s => s.ClassId == 24392);
                    Check(applied.SourceSkillRank == 5 && applied.SourceSkillLevel == 20
                        && FightBattleStatusOrigin.Matches(FightBattleStatusOrigin.Resolve(applied),
                            FightBattleStatusOrigin.Resolve(current)), "Original child status lost cultivation/origin.");
                }
            }
            else
            {
                otherHits++;
                Check(current.RoundStartTriggeredCount == before, "Release during another listener's turn invoked 25117.");
            }
            current.RoundStartRate = 1;
        };
        var runner = FightBattleExecutor.Begin(cp, formula, settlement, catalog, items);
        for (int counter = 1; counter <= 4; counter++)
        {
            var receipt = runner.Step(0, counter);
            Check(receipt.Event.ActorId == (counter % 2 == 1 ? 1 : 2), "Controlled timeline changed.");
            var current = cp.Units.Single(u => u.Id == ownerId).Statuses.Single(s => s.ClassId == 25117);
            int rounds = (counter + 1) / 2;
            int expected = 2 * rounds - (blocked ? 1 : 0) - (failedAttempt ? 1 : 0);
            if (maxCount >= 0) expected = Math.Min(expected, maxCount);
            Check(current.RoundStartTriggeredCount == expected, "Normal invocation and extra attempt did not remain independent.");
            string saved = FightBattleCheckpointSerializer.Save(cp);
            Check(runner.Step(0, counter).Replayed && FightBattleCheckpointSerializer.Save(cp) == saved,
                "Receipt replay repeated a forbidden-release attempt.");
            if (restore)
            {
                if (legacy)
                {
                    current.RoundStartForbiddenRulesResolved = false;
                    current.RoundStartTryAtNotForbidden = false;
                    current.RoundStartRulesSupported = false;
                    saved = FightBattleCheckpointSerializer.Save(cp);
                }
                cp = FightBattleCheckpointSerializer.Load(saved);
                runner = FightBattleExecutor.Restore(cp, formula, settlement, catalog, items);
            }
        }
        Check(ownHits == 4 && otherHits == 4, "Fixture did not execute two actual hits in each of four turns.");
        var finalOwner = cp.Units.Single(u => u.Id == ownerId);
        var finalSource = finalOwner.Statuses.Single(s => s.ClassId == 25117);
        string final = FightBattleCheckpointSerializer.Save(cp);
        FightStatusLifecycle.SetForbidden(finalOwner, finalSource, FightStatusLifecycle.ForbiddenMutex, true);
        FightStatusLifecycle.SetForbidden(finalOwner, finalSource, FightStatusLifecycle.ForbiddenMutex, false);
        Check(FightBattleCheckpointSerializer.Save(cp) == final, "Release after the round ended invoked or consumed the next round's quota.");
        return final;
    }
}
