using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
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
            var source = catalog.CreateStatus(24026, 1, 1, 9924026, 0, 0, 5, 20, 1, null);
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
    }
}
