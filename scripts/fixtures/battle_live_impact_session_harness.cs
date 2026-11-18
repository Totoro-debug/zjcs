using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Zjcs.Combat.Official;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.World.NativeTerrain.Common
{
    public struct Pos2d { public int X, Y; public Pos2d(int x, int y) { X = x; Y = y; } }
}
namespace Zjcs.Official.StoryPlayables
{
    public struct EcEntityIdentifier { public int Value; public EcEntityIdentifier(int value) { Value = value; } }
}
namespace Zjcs.Presentation.Combat
{
    // Drives the production session with real battle rules and a controlled native-view boundary.
    public sealed class OfficialBattleSkillEntityPlayback
    {
        public event Action<FightBattleSkillCastEvent, FightBattleHitSelectionSnapshot, int, Pos2d, int> HitSelectionViewArrived;
        public FightBattleSkillCastEvent Cast;
        public FightBattleHitSelectionSnapshot Selection;
        public float SelectionElapsed, CompletionElapsed;
        public int Damages, Completions;
        public bool ArriveImmediately;
        public void QueueSelectedHit(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            FightBattleHitSelectionSnapshot selection, float elapsed)
        {
            Cast = cast; Selection = selection; SelectionElapsed = elapsed;
            if (ArriveImmediately) {
                var target = selection.Targets.Single(t => t.TargetId == 2);
                Arrive(target.HasScopeIndex ? target.ScopeIndex : -1);
            }
        }
        public bool QueueSettledDamage(FightBattleSkillCastEvent cast, EcEntityIdentifier id, FightBattleDamageEvent damage)
        { if (!damage.HasDamageTriggerStatus) return false; Damages++; return ReferenceEquals(cast, Cast); }
        public void CompleteLiveCast(FightBattleSkillCastEvent cast, EcEntityIdentifier id, float elapsed)
        { if (!ReferenceEquals(cast, Cast)) throw new InvalidOperationException("Wrong completed cast"); Completions++; CompletionElapsed = elapsed; }
        public void Arrive(int scope) => HitSelectionViewArrived?.Invoke(Cast, Selection, 0, new Pos2d(10, 7), scope);
    }

    public static class BattleLiveImpactSessionHarness
    {
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("Live impact session: " + message); }
        private static FightBattleUnitState Unit(int id, int side, int x, FightDamageFormula formula)
        {
            var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
                Level = 50, SubRank = 5, X = x, Y = 7, Direction = 3, Hp = 1000000, MaxHp = 1000000,
                LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
            foreach (var prop in new Dictionary<int, long> { {1,1000}, {2,10}, {3,unit.MaxHp}, {4,unit.Turn.Speed} })
                unit.Properties.Add(new FightPropertyValue { Id = prop.Key, Value = prop.Value });
            foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
                "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
                unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
            return unit;
        }
        public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, formula); var target = Unit(2, 2, 10, formula);
            var skill = catalog.CreateSkill(10800, 0, 10028, 2, 20, 1,
                catalog.CalculateSkillProps(10800, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            string committed = FightBattleCheckpointSerializer.Save(cp);
            var ordinary = FightBattleCheckpointSerializer.Load(committed);
            FightBattleExecutor.Restore(ordinary, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
            var views = new OfficialBattleSkillEntityPlayback();
            using var session = new OfficialBattleLiveImpactSession(cp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), views, 0, 1);
            int attempts = 0; bool allowStart = false, hpRefreshed = false, reentrantRejected = false, transferReentrantRejected = false;
            bool Start(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
            { attempts++; id = new EcEntityIdentifier(42); return allowStart; }
            void Refresh(FightBattleDamageEvent damage)
            {
                hpRefreshed = target.Hp == 1000000 && damage.BeforeHp == 1000000 && damage.AfterHp < 1000000
                    && views.Damages == 1 && !session.IsCompleted;
                try { session.Pump(0f, Start); }
                catch (InvalidOperationException) { reentrantRejected = true; }
                try { session.TryTakeCompleted(out _, out _); }
                catch (InvalidOperationException) { transferReentrantRejected = true; }
            }
            Check(SpinWait.SpinUntil(() => { session.Pump(0f, Start, Refresh); return attempts > 0; }, 20000),
                "The actual cast preparation was not offered");
            session.Pump(1f, Start, Refresh);
            Check(target.Hp == 1000000 && views.Selection == null, "A deferred cast ran before its host started it");
            allowStart = true;
            Check(SpinWait.SpinUntil(() => { session.Pump(0f, Start, Refresh); return views.Selection != null; }, 20000),
                "The started cast did not deliver its actual hit selection");
            Check(views.SelectionElapsed >= 0f && target.Hp == 1000000 && views.Damages == 0,
                "The local result changed before the native hit arrival");
            var occurrence = views.Selection.Targets.Single(t => t.TargetId == 2);
            int expectedScope = occurrence.HasScopeIndex ? occurrence.ScopeIndex : -1;
            views.Arrive(expectedScope + 1);
            Check(views.Damages == 0 && target.Hp == 1000000,
                "An unrelated scope arrival released the pending impact");
            views.Arrive(expectedScope);
            Check(hpRefreshed && views.Damages == 1,
                "The matching arrival returned before its original damage and HP were displayed");
            Check(SpinWait.SpinUntil(() => { session.Pump(.001f, Start, Refresh); return session.IsCompleted; }, 20000)
                && hpRefreshed && reentrantRejected && transferReentrantRejected && views.Damages == 1 && views.Completions == 1
                && views.CompletionElapsed >= views.SelectionElapsed && target.Hp == 1000000,
                "Damage, HP and cast completion were not coordinated at the actual arrival");
            Check(session.TryTakeCompleted(out var completedExecutor, out var completed)
                && ReferenceEquals(completedExecutor.State, completed.Checkpoint) && completedExecutor.ImpactGate == null
                && FightBattleCheckpointSerializer.Save(cp) == committed
                && FightBattleCheckpointSerializer.Save(completed.Checkpoint) == FightBattleCheckpointSerializer.Save(ordinary),
                "The transferred result changed the original checkpoint or deterministic outcome");
            bool duplicate = false;
            try { session.TryTakeCompleted(out _, out _); }
            catch (InvalidOperationException) { duplicate = true; }
            Check(duplicate, "The live result was transferred twice");
            var earlyViews = new OfficialBattleSkillEntityPlayback { ArriveImmediately = true };
            using (var early = new OfficialBattleLiveImpactSession(cp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), earlyViews, 0, 1))
            {
                bool StartEarly(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(44); return true; }
                Check(SpinWait.SpinUntil(() => { early.Pump(0f, StartEarly); return early.IsCompleted; }, 20000)
                    && earlyViews.Damages == 1 && earlyViews.Completions == 1
                    && FightBattleCheckpointSerializer.Save(cp) == committed,
                    "An arrival during selection delivery did not settle without a lost wakeup");
            }

            var jumpCp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var jumper = Unit(1, 1, 7, formula); var jumpTarget = Unit(2, 2, 9, formula);
            var jump = catalog.CreateSkill(10670, 0, 10015, 2, 20, 1,
                catalog.CalculateSkillProps(10670, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            jump.LimitedTimes = 1; jumper.Skills.Add(jump); jumper.SkillState.Slots.Add(catalog.CreateSkillSlot(jump, 0));
            jumpCp.Units.AddRange(new[] { jumper, jumpTarget }); var jumpWave = new FightBattleWaveState();
            jumpWave.UnitIds.Add(2); jumpCp.Waves.Add(jumpWave);
            FightBattleExecutor.Begin(jumpCp, formula, new FightDamageSettlement(), catalog, items);
            string beforeJump = FightBattleCheckpointSerializer.Save(jumpCp);
            var jumpViews = new OfficialBattleSkillEntityPlayback();
            using (var pendingSession = new OfficialBattleLiveImpactSession(jumpCp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), jumpViews, 0, 1))
            {
                bool StartJump(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(43); return true; }
                Check(SpinWait.SpinUntil(() => { pendingSession.Pump(0f, StartJump); return jumpViews.Selection != null; }, 20000),
                    "The status-bearing jump did not offer its selected hit");
                var jumpOccurrence = jumpViews.Selection.Targets.Single(t => t.TargetId == 2);
                jumpViews.Arrive(jumpOccurrence.HasScopeIndex ? jumpOccurrence.ScopeIndex : -1);
                Check(SpinWait.SpinUntil(() => { pendingSession.Pump(0f, StartJump); return pendingSession.PendingSettlement != null; }, 20000)
                    && !pendingSession.PendingSettlement.Damage.HasDamageTriggerStatus && jumpViews.Damages == 0,
                    "An unknown status result must remain held rather than being invented");
                Check(!pendingSession.TryCompletePendingSettlement((impact, damage) => false)
                    && pendingSession.PendingSettlement != null && !pendingSession.IsCompleted,
                    "A declined fallback must keep the exact settlement waiting");
                pendingSession.Dispose();
                Check(SpinWait.SpinUntil(() => pendingSession.IsCompleted, 20000)
                    && FightBattleCheckpointSerializer.Save(jumpCp) == beforeJump,
                    "Canceling an unsupported live result changed the committed checkpoint");
            }
            var fallbackViews = new OfficialBattleSkillEntityPlayback();
            using (var fallbackSession = new OfficialBattleLiveImpactSession(jumpCp,
                copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items), fallbackViews, 0, 1))
            {
                bool StartJump(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = new EcEntityIdentifier(45); return true; }
                Check(SpinWait.SpinUntil(() => { fallbackSession.Pump(0f, StartJump); return fallbackViews.Selection != null; }, 20000),
                    "The fallback fixture did not reach its actual hit");
                var hit = fallbackViews.Selection.Targets.Single(t => t.TargetId == 2);
                fallbackViews.Arrive(hit.HasScopeIndex ? hit.ScopeIndex : -1);
                Check(fallbackSession.PendingSettlement != null, "The unresolved result did not remain held in the impact frame");
                bool delivered = false;
                Check(fallbackSession.TryCompletePendingSettlement((impact, damage) => {
                    delivered = !damage.HasDamageTriggerStatus && damage.BeforeHp == 1000000
                        && damage.AfterHp < 1000000 && !fallbackSession.IsCompleted;
                    return delivered;
                }) && delivered, "The host could not deliver the held result while the worker was suspended");
                Check(SpinWait.SpinUntil(() => { fallbackSession.Pump(0f, StartJump); return fallbackSession.IsCompleted; }, 20000)
                    && fallbackViews.Completions == 1 && FightBattleCheckpointSerializer.Save(jumpCp) == beforeJump,
                    "An accepted fallback did not resume the isolated step safely");
                Check(fallbackSession.TryTakeCompleted(out _, out var fallbackResult)
                    && fallbackResult.Event.DamageEvents.Any(d => d.SkillItemId == 10015),
                    "The resumed jump result was not transferable");
            }
            return "Live impact session passed: deferred start, same-frame impact, HP handoff, lifetime, isolation and unresolved-status handling.";
        }
    }
}
