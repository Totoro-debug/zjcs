using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static RoundForbiddenProbe ConfigureRoundForbiddenProbe(OfficialBattleRuntimeController controller)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_ROUND_FORBIDDEN_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var checkpoint = controller.Checkpoint;
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit.Id == 1 ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                unit.MaxHp = unit.LastMaxHp = unit.Hp = unit.Turn.Hp = hp;
                ConfigureHpRecoverySkill(unit.Skills.First(value => value.Slot == 0), 1000L, false);
            }
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var template = catalog.CreateStatus(25117, 1, 1, 9925117, 25117, 0, 5, 20, 1);
            var status = FightStatusLifecycle.Apply(player, template, player.SkillState.Round, template.InstanceId);
            var executor = (FightBattleExecutor)typeof(OfficialBattleRuntimeController)
                .GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var hits = (FightBattleHitExecutor)typeof(FightBattleExecutor)
                .GetField("hitExecutor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(executor);
            var settlement = (FightDamageSettlement)typeof(FightBattleHitExecutor)
                .GetField("settlement", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hits);
            return new RoundForbiddenProbe(player, status, settlement, catalog,
                OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula(), checkpoint.Units);
        }

        private sealed class RoundForbiddenProbe
        {
            private readonly FightBattleUnitState player;
            private readonly FightBattleStatusState status;
            private readonly FightDamageSettlement settlement;
            private readonly FightOfficialCombatCatalog catalog;
            private readonly FightDamageFormula formula;
            private readonly List<FightBattleUnitState> units;
            private int lastRound, releaseRounds;
            public bool ReadyToCapture => releaseRounds >= 2;
            public RoundForbiddenProbe(FightBattleUnitState player, FightBattleStatusState status,
                FightDamageSettlement settlement, FightOfficialCombatCatalog catalog,
                FightDamageFormula formula, List<FightBattleUnitState> units)
            {
                this.player = player; this.status = status; this.settlement = settlement;
                this.catalog = catalog; this.formula = formula; this.units = units;
                settlement.SourceHit += OnHit;
            }
            private void OnHit(FightDamageEvent hit)
            {
                if (hit.SourceId != player.Id) return;
                int round = player.SkillState.Round;
                if (round == lastRound) return;
                lastRound = round;
                CheckMutexProperties();
                Require(status.RoundStartTriggeredCount == round + releaseRounds,
                    "Original 25117 did not perform its normal round-start invocation.");
                var produced = player.Statuses.Single(value => value.ClassId == 24392);
                // Isolated event-input fixture: an actual hit supplies the active round and
                // timing. Original forbidden-reason producers require separate migration.
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    FightStatusLifecycle.SetForbidden(player, status, FightStatusLifecycle.ForbiddenMutex, true);
                    FightStatusLifecycle.SetForbidden(player, status, FightStatusLifecycle.ForbiddenStatusTrigger, true);
                    int before = status.RoundStartTriggeredCount;
                    FightStatusLifecycle.SetForbidden(player, status, FightStatusLifecycle.ForbiddenMutex, false);
                    Require(status.RoundStartTriggeredCount == before && status.ForbiddenType != 0,
                        "Partial release invoked a still-disabled original status.");
                    FightStatusLifecycle.SetForbidden(player, status, FightStatusLifecycle.ForbiddenStatusTrigger, false);
                    Require(status.RoundStartTriggeredCount == before + (attempt == 0 ? 1 : 0)
                        && player.Statuses.Contains(produced)
                        && produced.SourceSkillRank == 5 && produced.SourceSkillLevel == 20,
                        "Original 25117 lost its extra attempt, repeated it, or replaced its live buff/cultivation.");
                }
                releaseRounds++;
                Require(status.RoundStartNotForbiddenListenerId == player.Id
                    && status.RoundStartNotForbiddenRound == round,
                    "Original 25117 did not retain its per-listener round quota.");
                Debug.Log("[Round forbidden hosts] personalRound=" + round + ", normal=" + round
                    + ", extra=" + releaseRounds + ", actual=" + status.RoundStartTriggeredCount);
            }
            private void CheckMutexProperties()
            {
                long Attack() => player.Properties.First(value => value.Id == formula.PropertyId("Attack")).Value;
                FightBattleStatusState Apply(int id, long equipmentId, int instance)
                {
                    var template = catalog.CreateStatus(id, player.Id, player.Id, instance, 10110, 0, 5, 20, 1);
                    template.Origin = new FightBattleStatusOrigin { IsSet = true, UnitId = player.Id,
                        EntityKind = FightBattleStatusOrigin.Equipment, EntityId = equipmentId,
                        SkillEntityId = 10110 };
                    return FightStatusLifecycle.Apply(player, template, player.SkillState.Round, instance);
                }
                FightStatusPropertyRuntime.RebuildAll(units, catalog, formula);
                long baseline = Attack();
                var low = Apply(24040, 8801, 9924040);
                FightStatusPropertyRuntime.RebuildAll(units, catalog, formula);
                long lowAttack = Attack();
                var cached = low.ResolvedProperties;
                var high = Apply(24041, 8801, 9924041);
                Require(low.ForbiddenType == FightStatusLifecycle.ForbiddenMutex && high.ForbiddenType == 0
                    && Attack() < lowAttack && lowAttack < baseline && ReferenceEquals(cached, low.ResolvedProperties),
                    "Real status startup did not suppress the lower-weight attack contribution immediately.");
                var independent = Apply(24040, 8802, 9924042);
                FightStatusPropertyRuntime.RebuildAll(units, catalog, formula);
                long combined = Attack();
                Require(independent.ForbiddenType == 0 && combined < lowAttack,
                    "Independent equipment was suppressed by another origin's mutex group.");
                FightStatusLifecycle.SetForbidden(player, low, FightStatusLifecycle.ForbiddenStatusTrigger, true);
                FightStatusLifecycle.End(player, high);
                Require(low.ForbiddenType == FightStatusLifecycle.ForbiddenStatusTrigger && Attack() == lowAttack,
                    "Removing the winner cleared another forbidden reason or lost the independent contribution.");
                FightStatusLifecycle.SetForbidden(player, low, FightStatusLifecycle.ForbiddenStatusTrigger, false);
                Require(low.ForbiddenType == 0 && Attack() < lowAttack && Attack() > combined
                    && ReferenceEquals(cached, low.ResolvedProperties) && low.StackCount == 1,
                    "Fully released status did not restore its retained contribution.");
                FightStatusLifecycle.End(player, low); FightStatusLifecycle.End(player, independent);
                FightStatusPropertyRuntime.RebuildAll(units, catalog, formula);
                Require(Attack() == baseline, "Removed mutex fixtures left an attack contribution behind.");
                Debug.Log("[Status mutex hosts] 24040/24041: actual startup/end, independent equipment,"
                    + " combined forbidden reasons, cached values and immediate attack restoration passed.");
            }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                string saved = FightBattleCheckpointSerializer.Save(controller.Checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    "Forbidden-release checkpoint did not preserve its reason mask/quota.");
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                settlement.SourceHit -= OnHit;
                int rounds = checkpoint.Units.Single(value => value.Id == player.Id).SkillState.Round;
                Require(releaseRounds >= 3 && status.RoundStartTriggeredCount == rounds + releaseRounds,
                    host + " did not execute normal plus one extra invocation across real turns.");
                Debug.Log("[Round forbidden hosts] " + host + ": original 25117/24392, controlled hit-event releases, quota and reload passed.");
            }
        }

        private static RoundDelayProbe ConfigureRoundDelayProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_ROUND_DELAY_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit.Id == 1 ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit.Id != 1 && property.Id == catalog.PropertyType("Attack")) property.Value = 1000L;
                }
                unit.MaxHp = unit.LastMaxHp = unit.Hp = unit.Turn.Hp = hp;
            }
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var statuses = new[] { 25140, 25213, 64603, 25285, 24207 }.Select(id =>
            {
                var template = catalog.CreateStatus(id, 1, 1, 990000 + id, id, 0, 5, 20, 1, null);
                return FightStatusLifecycle.Apply(player, template, player.SkillState.Round, template.InstanceId);
            }).ToArray();
            return new RoundDelayProbe(catalog, statuses);
        }

        private sealed class RoundDelayProbe
        {
            private readonly FightOfficialCombatCatalog catalog;
            private readonly FightBattleStatusState recurring, once, bomb, filtered, exclusion;
            private int lastRound, actualBombCasts, filteredCasts;
            private bool refreshed, checkedRenewal;
            public bool ReadyToCapture => recurring.RoundStartTriggeredCount >= 2;
            public RoundDelayProbe(FightOfficialCombatCatalog catalog, FightBattleStatusState[] states)
            { this.catalog = catalog; recurring = states[0]; once = states[1]; bomb = states[2];
                filtered = states[3]; exclusion = states[4]; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var player = cp.Units.Single(value => value.Id == 1);
                int round = player.SkillState.Round;
                var step = cp.Receipts.Last().Event;
                var casts = step.SkillCasts.Where(value => value.SkillEntityId == 64604).ToArray();
                actualBombCasts += casts.Length;
                var filterCasts = step.SkillCasts.Where(value => value.SkillEntityId == 13370).ToArray();
                filteredCasts += filterCasts.Length;
                Require(filterCasts.Length == 0 || round == 4 && filterCasts.Length == 1
                    && filterCasts[0].ActorId == 1,
                    "Filtered delayed skill did not wait for a friend free of the excluded statuses.");
                Require(casts.Length == 0 || round == 3 && casts.Length == 1 && casts[0].ActorId == 1,
                    "Original delayed bomb did not execute from its real source in personal round three.");
                if (round == lastRound) return;
                lastRound = round;
                int expected = round < 3 ? 0 : round < 6 ? 1 : round - 4;
                Require(recurring.RoundStartTriggeredCount == expected
                    && once.RoundStartTriggeredCount == (round < 3 ? 0 : 1)
                    && bomb.RoundStartTriggeredCount == (round < 3 ? 0 : 1)
                    && filtered.RoundStartTriggeredCount == (round < 4 ? 0 : 1),
                    "Delayed status invoked early, repeated its initial wait, or lost counters on renewal.");
                if (round >= 3)
                    Require(player.Statuses.Any(value => value.ClassId == 24431)
                        && player.Statuses.Any(value => value.ClassId == 24852)
                        && !player.Statuses.Contains(bomb),
                        "Original delayed effects were missing or the three-round bomb did not expire.");
                if (round == 3 && !refreshed)
                {
                    var latest = catalog.CreateStatus(25140, 1, 1, 999140, 25140, 0, 5, 20, 1, null);
                    var renewed = FightStatusLifecycle.Apply(player, latest, round, latest.InstanceId);
                    Require(ReferenceEquals(renewed, recurring) && recurring.RoundStartDelayStartRound == 3
                        && recurring.RoundStartTriggeredCount == 1 && recurring.RoundStartMeetProgress == 1,
                        "Same-source delay refresh reset accumulated effects instead of its waiting clock.");
                    refreshed = true;
                    Require(filtered.RoundStartMeetProgress == 0 && player.Statuses.Contains(exclusion),
                        "Failed friend condition accumulated progress or lost its original exclusion.");
                    FightStatusLifecycle.End(player, exclusion);
                }
                if (round >= 6) checkedRenewal = true;
                Debug.Log("[Round delay hosts] personalRound=" + round + ", recurring="
                    + recurring.RoundStartTriggeredCount + ", once=" + once.RoundStartTriggeredCount
                    + ", bomb=" + bomb.RoundStartTriggeredCount + ", filtered=" + filtered.RoundStartTriggeredCount
                    + ", delayAnchor=" + recurring.RoundStartDelayStartRound);
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(refreshed && checkedRenewal && actualBombCasts == 1 && filteredCasts == 1
                    && once.RoundStartTriggeredCount == 1,
                    host + " did not finish original delayed execution and renewed waiting.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed listener delay or retained effects after checkpoint reload.");
                Debug.Log("[Round delay hosts] " + host + ": original third-round bomb/statuses, one initial wait,"
                    + " renewal waits through rounds four/five then resumes from six, friend condition blocks round three"
                    + " then permits original 13370 in round four, retained counters and checkpoint passed.");
            }
        }

        private static RoundStatusSourcesProbe ConfigureRoundStatusSourcesProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_STATUS_SOURCES_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit.Id == 1 ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit.Id != 1 && property.Id == catalog.PropertyType("Attack")) property.Value = 1000L;
                }
                unit.MaxHp = unit.LastMaxHp = unit.Hp = unit.Turn.Hp = hp;
            }
            var guard = checkpoint.Units.Single(value => value.Id == 2);
            var passives = new List<FightOfficialPassiveSkillBinding>();
            foreach (long equipmentId in new[] { 8801L, 8801L, 8802L })
            {
                passives.Add(new FightOfficialPassiveSkillBinding
                {
                    ItemId = 17056, Rank = 1, Level = 1, SubRank = 1,
                    Origin = new FightBattleStatusOrigin { EntityKind = FightBattleStatusOrigin.Equipment,
                        EntityId = equipmentId }
                });
            }
            var skillItems = OfficialCombatAssetCatalog.LoadDefault().LoadSkillItems();
            catalog.AssembleUnit(guard, null, catalog.CreatePassiveStatusBindings(guard, skillItems, passives));
            catalog.AssembleUnit(guard, null, catalog.CreatePassiveStatusBindings(guard, skillItems, passives));
            Require(guard.Statuses.Count(value => value.ClassId == 60560) == 2
                && guard.Statuses.Where(value => value.ClassId == 60560).All(value => value.StartRound == -1
                    && value.RoundStartTriggeredCount == 0),
                "Repeated initial equipment binding duplicated or prematurely started its passive.");
            guard.Skills.Clear(); guard.SkillState.Slots.Clear();
            var summon = catalog.CreateSkill(10710, 0, 0, 5, 20, 1, null);
            summon.LimitedTimes = 1;
            guard.Skills.Add(summon); guard.SkillState.Slots.Add(catalog.CreateSkillSlot(summon, 0));
            foreach (var entry in checkpoint.Timeline.Entries) if (entry.Id == 2) entry.Next = 0;
            checkpoint.Timeline.Entries.Sort((left, right) => left.Next.CompareTo(right.Next));
            return new RoundStatusSourcesProbe(catalog);
        }

        private sealed class RoundStatusSourcesProbe
        {
            private readonly FightOfficialCombatCatalog catalog;
            private FightBattleStatusState guardState, rockState;
            private int rockId, guardCalls, rockCalls, departedRockCalls;
            private bool independentEquipmentObserved, equipmentRemovalObserved;
            public bool ReadyToCapture => guardCalls >= 4;
            public RoundStatusSourcesProbe(FightOfficialCombatCatalog catalog) { this.catalog = catalog; }
            private FightBattleStatusState Create(int creator, int instance) =>
                catalog.CreateStatus(24640, 1, creator, instance, 63280, 0, 5, 20, 1, null);
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var player = cp.Units.Single(value => value.Id == 1);
                if (guardState == null)
                {
                    var rock = cp.Units.FirstOrDefault(value => value.MasterId == 2);
                    if (rock == null) return;
                    rockId = rock.Id;
                    foreach (var property in rock.Properties.Concat(rock.BaseProperties))
                    {
                        if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 2000000L;
                        if (property.Id == catalog.PropertyType("Attack")) property.Value = 1000L;
                    }
                    rock.MaxHp = rock.LastMaxHp = rock.Hp = rock.Turn.Hp = 2000000L;
                    guardState = FightStatusLifecycle.Apply(player, Create(2, 9924640), player.SkillState.Round, 9924640);
                    rockState = FightStatusLifecycle.Apply(player, Create(rockId, 9924641), player.SkillState.Round, 9924641);
                    Require(player.Statuses.Count(value => value.ClassId == 24640) == 2,
                        "Independent guard/rock status sources were merged on application.");
                    return;
                }
                var step = cp.Receipts.Last().Event;
                var equipmentEffects = cp.Units.Single(value => value.Id == 2).Statuses
                    .Where(value => value.ClassId == 60561).ToArray();
                bool rockEnded = cp.Units.Single(value => value.Id == rockId).Ended;
                if (equipmentEffects.Length == 0 && rockEnded) equipmentRemovalObserved = true;
                else
                {
                    Require(equipmentEffects.Length == 2 && equipmentEffects.All(value => value.StackCount == 1
                        && value.Origin.EntityKind == FightBattleStatusOrigin.Equipment)
                        && equipmentEffects.Select(value => value.Origin.EntityId).OrderBy(value => value)
                            .SequenceEqual(new[] { 8801L, 8802L }),
                        "Same-owner equipment summon-count effects collided or accumulated each other's layers.");
                    independentEquipmentObserved = true;
                }
                foreach (int caster in new[] { 2, rockId })
                {
                    var casts = step.SkillCasts.Where(value => value.SkillEntityId == 63280 && value.ActorId == caster).ToArray();
                    if (casts.Length == 0) continue;
                    var state = caster == 2 ? guardState : rockState;
                    int calls = caster == 2 ? ++guardCalls : ++rockCalls;
                    var damage = step.DamageEvents.Where(value => value.SkillEntityId == 63280 && value.SourceId == caster).ToArray();
                    Require(casts.Length == 1 && casts[0].TargetIds.SequenceEqual(new[] { 1 })
                        && damage.Length == 1 && damage[0].TargetId == 1 && damage[0].Damage < 0
                        && damage[0].AfterHp < damage[0].BeforeHp && state.RoundStartTriggeredCount == calls,
                        "Independent guard/rock source lost its original victim, actual damage or invocation count.");
                    if (caster == rockId && rockEnded) departedRockCalls++;
                    Debug.Log("[Status sources hosts] original 24640 -> 63280 -> 63281: caster=" + caster
                        + ", call=" + calls + ", damage=" + damage[0].Damage + ", personalRound=" + player.SkillState.Round
                        + ", departedCreator=" + (caster == rockId && rockEnded));
                }
                if (guardCalls == 1 && guardState.RoundStartStackRoundCount == 1)
                {
                    var refreshed = FightStatusLifecycle.Apply(player, Create(2, 9924642), player.SkillState.Round, 9924642);
                    Require(ReferenceEquals(refreshed, guardState) && guardState.RoundStartTriggeredCount == 1
                        && FightStatusLifecycle.RoundStartMaximumCount(guardState) == 6
                        && rockState.RoundStartStackRoundCount == 1 && rockState.RoundStartTriggeredCount == 1,
                        "Refreshing guard poison replaced its live state or changed rock poison's quota.");
                }
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(guardCalls == 6 && rockCalls == 3 && departedRockCalls > 0
                    && independentEquipmentObserved && equipmentRemovalObserved
                    && guardState.RoundStartStackRoundCount == 2 && rockState.RoundStartStackRoundCount == 1
                    && guardState.SourceSkillRank == 5 && guardState.SourceSkillLevel == 20
                    && !checkpoint.Units.Single(value => value.Id == 1).Statuses.Any(value => value.ClassId == 24640),
                    host + " did not settle independent renewed and unrenewed poison sources exactly.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed independent source state after checkpoint reload.");
                Debug.Log("[Status sources hosts] " + host + ": guard six calls, rock three calls, departed rock still casts, independent equipment effects are removed after its departure, retained live instance and cultivation, independent expiry and checkpoint passed.");
            }
        }

        private static RoundMergeFormulaProbe ConfigureRoundMergeFormulaProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_ROUND_MERGE_FORMULA_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit.Id == 1 ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit.Id != 1 && property.Id == catalog.PropertyType("Attack")) property.Value = 1000L;
                }
                unit.MaxHp = unit.LastMaxHp = unit.Hp = unit.Turn.Hp = hp;
            }
            var source = catalog.CreateStatus(24640, 1, 2, 9924640, 0, 0, 5, 20, 1, null);
            // Formula-only fixture; the separate source probe exercises actual instance selection.
            FightStatusLifecycle.Initialize(source, 0);
            checkpoint.Units.Single(value => value.Id == 1).Statuses.Add(source);
            return new RoundMergeFormulaProbe(source);
        }

        private sealed class RoundMergeFormulaProbe
        {
            private readonly FightBattleStatusState source;
            private int calls;
            public bool ReadyToCapture => calls >= 4;
            public RoundMergeFormulaProbe(FightBattleStatusState source) { this.source = source; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                var casts = step.SkillCasts.Where(value => value.SkillEntityId == 63280 && value.ActorId == 2).ToArray();
                if (casts.Length == 0) return;
                calls++;
                if (calls == 1)
                    FightStatusLifecycle.ExtendDurationOnStack(source,
                        cp.Units.Single(value => value.Id == 1).SkillState.Round);
                var damage = step.DamageEvents.Where(value => value.SkillEntityId == 63280 && value.SourceId == 2).ToArray();
                Require(casts.Length == 1 && casts[0].TargetIds.SequenceEqual(new[] { 1 })
                    && damage.Length == 1 && damage[0].TargetId == 1 && damage[0].Damage < 0
                    && damage[0].AfterHp < damage[0].BeforeHp,
                    "Duration formula fixture lost actual creator damage against its victim.");
                Require(source.RoundStartTriggeredCount == calls && calls <= 6
                    && source.RoundStartStackRoundCount == 2 && FightStatusLifecycle.RoundStartMaximumCount(source) == 6
                    && source.CurDurationRound == 5 && source.StartRound == 1
                    && source.SourceSkillRank == 5 && source.SourceSkillLevel == 20,
                    "Duration formula fixture lost retained invocation count, quota or cultivation.");
                Debug.Log("[Round merge formula hosts] 24640 -> 63280 -> 63281: call=" + calls
                    + ", damage=" + damage[0].Damage + ", renewed-duration=5, start-round=1, stack-round=2, maximum=6.");
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(calls == 6 && !checkpoint.Units.Single(value => value.Id == 1).Statuses.Contains(source),
                    host + " did not finish all six formula-fixture calls and remove its exhausted state.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed the duration formula fixture after checkpoint reload.");
                Debug.Log("[Round merge formula hosts] " + host + ": six actual damage calls, exhausted removal and checkpoint passed; instance selection uses the separate source probe.");
            }
        }

        private static RoundSourceProbe ConfigureRoundSourceProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_ROUND_SOURCE_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit.Id == 1 ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit.Id != 1 && property.Id == catalog.PropertyType("Attack")) property.Value = 1000L;
                }
                unit.MaxHp = unit.LastMaxHp = unit.Hp = unit.Turn.Hp = hp;
            }
            var player = checkpoint.Units.Single(value => value.Id == 1);
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_ROUND_DEAD_SOURCE_ACCEPTANCE") == "1")
            {
                var guard = checkpoint.Units.Single(value => value.Id == 2);
                guard.Skills.Clear(); guard.SkillState.Slots.Clear();
                var summon = catalog.CreateSkill(10710, 0, 0, 5, 20, 1, null);
                summon.LimitedTimes = 1;
                guard.Skills.Add(summon); guard.SkillState.Slots.Add(catalog.CreateSkillSlot(summon, 0));
                foreach (var entry in checkpoint.Timeline.Entries) if (entry.Id == 2) entry.Next = 0;
                checkpoint.Timeline.Entries.Sort((left, right) => left.Next.CompareTo(right.Next));
                return new RoundSourceProbe(null, catalog);
            }
            var source = catalog.CreateStatus(24105, 1, 2, 9924105, 0, 0, 5, 20, 1, null);
            player.Statuses.Add(source);
            return new RoundSourceProbe(source);
        }

        private sealed class RoundSourceProbe
        {
            private FightBattleStatusState source;
            private readonly FightOfficialCombatCatalog catalog;
            private int calls;
            private bool creatorWasDead, invokedAfterDeath, creatorDefeatedByHit;
            public bool ReadyToCapture => catalog == null ? calls > 0 : invokedAfterDeath;
            public RoundSourceProbe(FightBattleStatusState source, FightOfficialCombatCatalog catalog = null)
            { this.source = source; this.catalog = catalog; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                var player = cp.Units.Single(value => value.Id == 1);
                var guard = cp.Units.Single(value => value.Id == 2);
                if (source == null)
                {
                    var rock = cp.Units.FirstOrDefault(value => value.MasterId == 2);
                    if (rock == null) return;
                    foreach (var property in rock.Properties.Concat(rock.BaseProperties))
                        if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 2000000L;
                    rock.Hp = rock.Turn.Hp = rock.MaxHp = rock.LastMaxHp = 2000000L;
                    source = catalog.CreateStatus(24105, 1, 2, 9924105, 0, 0, 5, 20, 1, null);
                    source = FightStatusLifecycle.Apply(player, source, player.SkillState.Round, 9924105);
                    // Isolated input: another original status hit defeats the creator;
                    // its actual summoned enemy keeps the battlefield active afterward.
                    guard.Hp = guard.Turn.Hp = 1;
                    FightStatusLifecycle.Apply(guard,
                        catalog.CreateStatus(24105, 2, 1, 9924106, 0, 0, 5, 20, 1, null),
                        guard.SkillState.Round, 9924106);
                    return;
                }
                var casts = step.SkillCasts.Where(value => value.SkillEntityId == 59680 && value.ActorId == 2).ToArray();
                bool alreadyDead = creatorWasDead;
                creatorWasDead = guard.Hp <= 0;
                if (step.DamageEvents.Any(value => value.SkillEntityId == 59680
                    && value.SourceId == 1 && value.TargetId == 2 && value.Damage < 0 && value.AfterHp == 0))
                    creatorDefeatedByHit = true;
                if (casts.Length == 0) return;
                calls++;
                var damage = step.DamageEvents.Where(value => value.SkillEntityId == 59680 && value.SourceId == 2).ToArray();
                Require(casts.Length == 1 && casts[0].ActorId == 2
                    && casts[0].TargetIds.SequenceEqual(new[] { 1 })
                    && damage.Length == 1 && damage[0].SourceId == 2 && damage[0].TargetId == 1
                    && damage[0].Damage < 0 && damage[0].AfterHp < damage[0].BeforeHp,
                    "Original 24105 did not retain its creator, explicit victim or actual damage.");
                Require(source.RoundStartTriggeredCount == calls && calls <= 2
                    && source.SourceSkillRank == 5 && source.SourceSkillLevel == 20
                    && player.SkillState.Round == calls,
                    "Creator damage lost its original cultivation, finite calls or victim round clock.");
                if (alreadyDead) invokedAfterDeath = true;
                Debug.Log("[Round source hosts] original 24105 -> 59680 -> 59681: caster=2, victim=1, call="
                    + calls + ", damage=" + damage[0].Damage + ", creatorAlreadyDead=" + alreadyDead
                    + ", rank=5, level=20.");
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(calls == 2 && !checkpoint.Units.Single(value => value.Id == 1).Statuses.Contains(source),
                    host + " did not finish the original two creator-sourced damage rounds and duration.");
                Require(catalog == null || invokedAfterDeath && creatorDefeatedByHit,
                    host + " did not settle original creator damage after its actual defeat.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed creator source state after checkpoint reload.");
                Debug.Log("[Round source hosts] " + host + ": original caster/victim damage, two-call lifetime and checkpoint passed.");
            }
        }

        private static RoundStartupProbe ConfigureRoundStartupProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_ROUND_STARTUP_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit == player ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit != player && property.Id == catalog.PropertyType("Attack"))
                        property.Value = Math.Min(property.Value, 100L);
                }
                unit.MaxHp = unit.LastMaxHp = hp;
                unit.Hp = unit.Turn.Hp = unit == player ? hp / 2 : hp;
            }
            var source = catalog.CreateStatus(25077, 1, 1, 9925077, 0, 0, 5, 20, 1, null);
            player.Statuses.Add(source);
            return new RoundStartupProbe(source);
        }

        private sealed class RoundStartupProbe
        {
            private readonly FightBattleStatusState source;
            private int calls;
            public bool ReadyToCapture => calls > 0;
            public RoundStartupProbe(FightBattleStatusState source) { this.source = source; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var player = cp.Units.Single(value => value.Id == 1);
                var step = cp.Receipts.Last().Event;
                var casts = step.SkillCasts.Where(value => value.SkillEntityId == 18860).ToArray();
                if (casts.Length == 0) return;
                calls++;
                Require(casts.Length == 1 && casts[0].ActorId == 1 && casts[0].TargetIds.Contains(1)
                    && source.RoundStartTriggeredCount == calls && calls <= 4,
                    "Original 25077 lost its configured startup/round invocation count or self target.");
                Require(player.SkillState.Round == calls - 1
                    && player.Statuses.Contains(source) == (calls < 4),
                    "Original 25077 consumed a turn at startup or remained after its final invocation.");
                Require(step.DamageEvents.Any(value => value.SkillEntityId == 18860 && value.IsCure),
                    "Original 18860 lost its actual healing event.");
                Debug.Log("[Round startup hosts] 25077 -> 18860 call=" + calls
                    + ", personalRound=" + player.SkillState.Round + ", remainingSource=" + player.Statuses.Contains(source));
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(calls == 4 && source.RoundStartStartChecked,
                    host + " did not execute its four original round-start calls.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed its round-start checkpoint after reload.");
                Debug.Log("[Round startup hosts] " + host + ": startup without personal turn; fourth call removed source; checkpoint passed.");
            }
        }

        private static RoundTargetsProbe ConfigureRoundTargetsProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_ROUND_TARGETS_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var guard = checkpoint.Units.Single(value => value.Id == 2);
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit == player ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit != player && property.Id == catalog.PropertyType("Attack")) property.Value = 100L;
                }
                unit.MaxHp = unit.LastMaxHp = hp;
                unit.Hp = unit.Turn.Hp = unit == player ? hp / 2 : hp;
            }
            guard.Skills.Clear(); guard.SkillState.Slots.Clear();
            var summon = catalog.CreateSkill(10710, 0, 0, 5, 20, 1, null);
            summon.LimitedTimes = 1;
            guard.Skills.Add(summon); guard.SkillState.Slots.Add(catalog.CreateSkillSlot(summon, 0));
            foreach (var entry in checkpoint.Timeline.Entries) if (entry.Id == 2) entry.Next = 0;
            checkpoint.Timeline.Entries.Sort((left, right) => left.Next.CompareTo(right.Next));
            return new RoundTargetsProbe(catalog);
        }

        private sealed class RoundTargetsProbe
        {
            private readonly FightOfficialCombatCatalog catalog;
            private bool armed, observed;
            private int personalRound;
            public bool ReadyToCapture => observed;
            public RoundTargetsProbe(FightOfficialCombatCatalog catalog) { this.catalog = catalog; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                var player = cp.Units.Single(value => value.Id == 1);
                var guard = cp.Units.Single(value => value.Id == 2);
                if (!armed)
                {
                    var rock = cp.Units.FirstOrDefault(value => value.MasterId == 2);
                    if (rock == null)
                    {
                        Require(!step.SkillCasts.Any(value => value.ActorId == 2 && value.SkillEntityId == 10710),
                            "Original 10710 did not create its enemy rock for the round-target fixture.");
                        return;
                    }
                    // Isolated roster input: use the already rendered original guard as a
                    // friend and keep its actual summon as an enemy. This is not a party UI test.
                    guard.Side = player.Side;
                    guard.Skills.Clear(); guard.SkillState.Slots.Clear();
                    guard.Statuses.RemoveAll(value => value.ClassId == 24207 || value.ClassId == 24301);
                    guard.Hp = guard.Turn.Hp = guard.MaxHp * 3 / 10;
                    rock.Hp = rock.Turn.Hp = rock.MaxHp = rock.LastMaxHp = 2000000L;
                    foreach (var property in rock.Properties.Concat(rock.BaseProperties))
                        if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 2000000L;
                    player.Statuses.Add(catalog.CreateStatus(25288, 1, 1, 9925288, 0, 0, 5, 20, 1, null));
                    personalRound = player.SkillState.Round;
                    armed = true;
                    return;
                }
                var casts = step.SkillCasts.Where(value => value.SkillEntityId == 61650).ToArray();
                if (casts.Length == 0) return;
                var protection = guard.Statuses.SingleOrDefault(value => value.ClassId == 24207 && value.CreatorId == 1);
                Require(!observed && casts.Length == 1 && casts[0].ActorId == 1
                    && casts[0].TargetIds.SequenceEqual(new[] { 2 }) && player.SkillState.Round == personalRound
                    && protection != null && protection.SourceSkillRank == 5 && protection.SourceSkillLevel == 20,
                    "Original 25288 lost its eligible friend, startup timing or inherited protection.");
                observed = true;
                Debug.Log("[Round targets hosts] original 25288 -> 61650 -> 24207: friend guard 2, inherited rank 5/level 20, startup before another personal round.");
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(observed, host + " never executed its actual minimum-HP friend protection.");
                Require(checkpoint.Receipts.Sum(value => value.Event.SkillCasts.Count(cast => cast.SkillEntityId == 61650)) == 1,
                    host + " repeated its single allowed original protection invocation.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed its round-target checkpoint after reload.");
                Debug.Log("[Round targets hosts] " + host + ": original protection once; checkpoint passed.");
            }
        }
        private static RoundPropsProbe ConfigureRoundPropsProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_ROUND_PROPS_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit.Id == 1 ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit.Id != 1 && property.Id == catalog.PropertyType("Attack")) property.Value = 100L;
                }
                unit.MaxHp = unit.LastMaxHp = unit.Hp = unit.Turn.Hp = hp;
            }
            var player = checkpoint.Units.Single(value => value.Id == 1);
            player.Statuses.Add(catalog.CreateStatus(25119, 1, 1, 9925119, 0, 0, 5, 20, 1, null));
            player.Statuses.Add(catalog.CreateStatus(25206, 1, 1, 9925206, 0, 0, 5, 20, 1, null));
            return new RoundPropsProbe();
        }

        private sealed class RoundPropsProbe
        {
            private bool observed;
            public bool ReadyToCapture => observed;
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var step = cp.Receipts.Last().Event;
                var player = cp.Units.Single(value => value.Id == 1);
                var friendCasts = step.SkillCasts.Where(value => value.SkillEntityId == 12180).ToArray();
                var enemyCasts = step.SkillCasts.Where(value => value.SkillEntityId == 68020).ToArray();
                if (friendCasts.Length == 0 && enemyCasts.Length == 0) return;
                Require(friendCasts.Length == 1 && friendCasts[0].ActorId == 1
                    && friendCasts[0].TargetIds.SequenceEqual(new[] { 1 })
                    && enemyCasts.Length == 1 && enemyCasts[0].ActorId == 1
                    && enemyCasts[0].TargetIds.SequenceEqual(new[] { 2 }),
                    "Original property selectors lost the eligible friend/enemy targets.");
                var guard = cp.Units.Single(value => value.Id == 2);
                Require(player.Statuses.Any(value => value.ClassId == 24395 && value.SourceSkillRank == 5
                        && value.SourceSkillLevel == 20)
                    && guard.Statuses.Any(value => value.ClassId == 24673 && value.CreatorId == 1
                        && value.SourceSkillRank == 5 && value.SourceSkillLevel == 20),
                    "Property-target casts lost their actual inherited original states.");
                observed = true;
                Debug.Log("[Round props hosts] personalRound=" + player.SkillState.Round
                    + ": 25119 -> 12180 -> 24395 friend 1; 25206 -> 68020 -> 24673 enemy 2; rank5/level20.");
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(observed, host + " never executed the original property-target skills.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed its property-target checkpoint after reload.");
                Debug.Log("[Round props hosts] " + host + ": original skills/effects and checkpoint passed.");
            }
        }
        private static RoundMeetProbe ConfigureRoundMeetProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_ROUND_MEET_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit.Id == 1 ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit.Id != 1 && property.Id == catalog.PropertyType("Attack")) property.Value = 100L;
                }
                unit.MaxHp = unit.LastMaxHp = unit.Hp = unit.Turn.Hp = hp;
            }
            var source = catalog.CreateStatus(24026, 1, 1, 9924026, 10500, 10000, 5, 20, 1, null);
            checkpoint.Units.Single(value => value.Id == 1).Statuses.Add(source);
            return new RoundMeetProbe(source);
        }

        private sealed class RoundMeetProbe
        {
            private readonly FightBattleStatusState source;
            private int lastRound;
            public bool ReadyToCapture => lastRound >= 3;
            public RoundMeetProbe(FightBattleStatusState source) { this.source = source; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var player = cp.Units.Single(value => value.Id == 1);
                int round = player.SkillState.Round;
                if (round == lastRound) return;
                lastRound = round;
                int count = Math.Max(0, round - 2);
                var effect = player.Statuses.SingleOrDefault(value => value.ClassId == 24478);
                Require(source.RoundStartMeetProgress == Math.Min(3, round)
                    && source.RoundStartTriggeredCount == count && (effect != null) == (count > 0),
                    "Original 24026 invoked before three eligible rounds or lost its reached threshold.");
                if (effect != null)
                    Require(effect.StackCount == Math.Min(5, count) && effect.SourceSkillRank == 5
                        && effect.SourceSkillLevel == 20,
                        "Original 24026 lost its repeated 24478 application or cultivation.");
                Debug.Log("[Round meet hosts] personalRound=" + round + ", progress="
                    + source.RoundStartMeetProgress + ", invoked=" + count + ", 24478 stacks=" + (effect?.StackCount ?? 0));
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(lastRound >= 4 && source.RoundStartTriggeredCount >= 2,
                    host + " did not exercise both reaching and retaining the original condition gate.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed its condition gate after checkpoint reload.");
                Debug.Log("[Round meet hosts] " + host + ": third round starts 24478; later rounds retain gate; checkpoint passed.");
            }
        }
        private static RoundRandomProbe ConfigureRoundRandomProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_ROUND_RANDOM_ACCEPTANCE") != "1") return null;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            foreach (var unit in checkpoint.Units)
            {
                long hp = unit.Id == 1 ? 5000000L : 2000000L;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = hp;
                    if (unit.Id != 1 && property.Id == catalog.PropertyType("Attack")) property.Value = 100L;
                }
                unit.MaxHp = unit.LastMaxHp = unit.Hp = unit.Turn.Hp = hp;
            }
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var repeated = catalog.CreateStatus(25120, 1, 1, 9925120, 0, 0, 5, 20, 1, null);
            var once = catalog.CreateStatus(60520, 1, 1, 9960520, 0, 0, 5, 20, 1, null);
            player.Statuses.Add(repeated); player.Statuses.Add(once);
            return new RoundRandomProbe(repeated, once);
        }

        private sealed class RoundRandomProbe
        {
            private readonly FightBattleStatusState repeated, once;
            private int lastRound;
            public bool ReadyToCapture => lastRound >= 1;
            public RoundRandomProbe(FightBattleStatusState repeated, FightBattleStatusState once)
            { this.repeated = repeated; this.once = once; }
            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var player = cp.Units.Single(value => value.Id == 1);
                int round = player.SkillState.Round;
                if (round == lastRound) return;
                lastRound = round;
                var effects = player.Statuses.Where(value => value.ClassId == 24396
                    || value.ClassId == 24398 || value.ClassId == 24399).ToArray();
                Require(effects.Length == 1 && repeated.RoundStartTriggeredCount == round
                    && once.RoundStartTriggeredCount == 1 && effects[0].SourceSkillRank == 5
                    && effects[0].SourceSkillLevel == 20
                    && effects[0].StatusProperties.Any(value => value.Value != 0),
                    "Original 25120 selected multiple states, lost its real property effect or changed 60520's one-call cap.");
                if (round == 1)
                    Require(player.Statuses.Count(value => value.ClassId == 60521 || value.ClassId == 60522) == 1,
                        "Original 60520 did not select exactly one configured state.");
                Debug.Log("[Round random hosts] personalRound=" + round + ", 25120 selected="
                    + effects[0].ClassId + ", repeated=" + repeated.RoundStartTriggeredCount
                    + ", 60520 count=" + once.RoundStartTriggeredCount + ", original nonzero props preserved.");
            }
            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(lastRound >= 3 && repeated.RoundStartTriggeredCount >= 3 && once.RoundStartTriggeredCount == 1,
                    host + " did not exercise repeated and one-call random status selection.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    host + " changed its random-state checkpoint after reload.");
                Debug.Log("[Round random hosts] " + host + ": original single random buffs, finite cap and checkpoint passed.");
            }
        }
    }
}
