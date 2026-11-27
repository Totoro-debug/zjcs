using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        [Serializable] private sealed class SwiftWindDanceReceipt
        {
            public int casts, ends, intervals, restoredBaseIntervals, suppressedTurns;
            public long sourceSpeed, bonus;
            public bool restored, buffCaptured, suppressionStarted;
        }
        private static SwiftWindDanceReceipt swiftWindDance;
        private static int swiftWindLastRound;
        private static long swiftWindLastSpeed;
        private static long swiftWindCastSourceSpeed;

        private static void PrepareAcquiredSwiftWindDance(FightBattleUnitState actor)
        {
            swiftWindDance = new SwiftWindDanceReceipt { sourceSpeed = actor.Properties.Single(p => p.Id == 4).Value };
            swiftWindLastSpeed = swiftWindDance.sourceSpeed;
            swiftWindCastSourceSpeed = swiftWindLastSpeed;
            swiftWindLastRound = actor.SkillState.Round;
        }

        private static void VerifyAcquiredSwiftWindStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            bool cast = step.SkillCasts.Any(value => value.ActorId == actor.Id && value.SkillEntityId == 11930);
            if (cast) swiftWindCastSourceSpeed = swiftWindLastSpeed;
            swiftWindDance.ends += step.StatusViewEvents.Count(view => view.OwnerId == actor.Id
                && (view.Status.ClassId == 24373 || view.Status.ClassId == 24374)
                && view.Kind == FightBattleStatusViewEventKind.End);
            if (actor.SkillState.Round > swiftWindLastRound)
            {
                var active = actor.Skills.Single(skill => skill.Slot == 0);
                var values = OfficialCombatAssetCatalog.LoadDefault().Load()
                    .CalculateSkillProps(24373, active.Rank, active.Level, active.SubRank);
                long bonus = (long)(swiftWindCastSourceSpeed * (decimal)values[2000] / 10000m + values[2010]);
                long speed = cast ? swiftWindDance.sourceSpeed + bonus : swiftWindLastSpeed;
                long interval = Math.Max(1L, (long)Math.Floor(100000000d / Math.Sqrt(Math.Max(1L, speed)
                    * (double)Math.Max(1, actor.Turn.RankSpeedScale)) + .5d));
                Require(actor.Turn.IntervalTicks == interval,
                    "Swift Wind Dance's next queued interval did not read its current final speed.");
                swiftWindDance.intervals++;
                if (!cast && speed == swiftWindDance.sourceSpeed) swiftWindDance.restoredBaseIntervals++;
                if (swiftWindDance.suppressionStarted && swiftWindDance.suppressedTurns < 3)
                {
                    Require(!cast, "The existing official stun did not prevent Swift Wind Dance's refresh.");
                    swiftWindDance.suppressedTurns++;
                    if (swiftWindDance.suppressedTurns < 3) ApplySwiftWindDiagnosticStun(checkpoint);
                }
            }
            swiftWindLastRound = actor.SkillState.Round;
            swiftWindLastSpeed = actor.Properties.Single(p => p.Id == 4).Value;
        }

        private static void VerifyAcquiredSwiftWindCast(FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] hits, FightBattleCheckpoint checkpoint, string output)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == cast.ActorId);
            var active = actor.Skills.Single(skill => skill.Slot == 0);
            var speed = actor.Statuses.Single(status => status.ClassId == 24373);
            var move = actor.Statuses.Single(status => status.ClassId == 24374);
            foreach (var status in new[] { speed, move })
                Require(status.CreatorId == actor.Id && status.ApplyingSkillItemId == 10086
                    && status.RootSkillId == 11930 && status.SourceSkillRank == active.Rank
                    && status.SourceSkillLevel == 2 && status.SourceSubRank == 5 && status.DurationRound == 2
                    && status.RoundTarget == 2 && status.RoundUpdateTiming == 1 && status.CanDispersed,
                    "Swift Wind Dance's movement/speed status lost its original cultivation or beneficiary clock.");
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var values = catalog.CalculateSkillProps(24373, active.Rank, active.Level, active.SubRank);
            long source = speed.SourceProperties.Single(p => p.Id == 4).Value;
            long bonus = (long)(source * (decimal)values[2000] / 10000m + values[2010]);
            Require(cast.HitSelections.Count == 1 && cast.HitSelections[0].HitEntityId == 11931
                && source == swiftWindCastSourceSpeed && speed.ResolvedProperties.Single(p => p.Id == 4).Value == bonus
                && actor.Properties.Single(p => p.Id == 4).Value == swiftWindDance.sourceSpeed + bonus
                && hits.All(hit => hit.SkillEntityId == 11930 && hit.HitEntityId == 11931
                    && hit.DamageEntityId == 11932 && hit.TargetId == actor.Id && hit.Damage == 0),
                "Swift Wind Dance lost its unique fixed/proportional speed snapshot or dealt damage.");
            int moveId = OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula().PropertyId("MoveDist");
            long baseMove = actor.BaseProperties.FirstOrDefault(p => p.Id == moveId)?.Value ?? 0L;
            Require(actor.Properties.Single(p => p.Id == moveId).Value == baseMove + 2,
                "Swift Wind Dance did not apply the original two movement cells.");
            swiftWindDance.casts++; swiftWindDance.bonus = bonus;
            // Exercise natural refreshes first. Three existing one-round stuns then
            // prevent refresh in this isolated scenario so the actual buff can expire.
            if (swiftWindDance.casts == 3)
            {
                swiftWindDance.suppressionStarted = true;
                ApplySwiftWindDiagnosticStun(checkpoint);
            }
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            Require(OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored)
                .Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == saved,
                "Restoring Swift Wind Dance repeated its opening enhancement or changed speed scheduling.");
            swiftWindDance.restored = true;
            File.WriteAllText(Path.Combine(output, "acquired-swift-wind-dance-checkpoint.json"), saved);
        }

        private static void CompleteAcquiredSwiftWindDance(string output)
        {
            Require(swiftWindDance.casts > 0 && swiftWindDance.ends >= 2 && swiftWindDance.intervals >= 2
                && swiftWindDance.restoredBaseIntervals > 0 && swiftWindDance.suppressedTurns == 3
                && swiftWindDance.restored && swiftWindDance.buffCaptured,
                "Swift Wind Dance must render, expire and restore its cultivated movement/speed enhancement and action intervals.");
            File.WriteAllText(Path.Combine(output, "acquired-swift-wind-dance-receipt.json"),
                JsonUtility.ToJson(swiftWindDance, true));
            Debug.Log("[Acquired active combat] 10086 cultivated movement/speed enhancement, native effect, final-speed intervals, expiry and restoration passed.");
        }

        private static void ApplySwiftWindDiagnosticStun(FightBattleCheckpoint checkpoint)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            int instance = FightStatusLifecycle.NextInstanceId(actor);
            var template = catalog.CreateStatus(24010, actor.Id, 2, instance);
            Require(template.DurationRound == 1 && template.RoundUpdateTiming == 1,
                "Review the existing official one-round stun used by the isolated expiry scenario.");
            Require(FightStatusLifecycle.Apply(actor, template, actor.SkillState.Round, instance) != null,
                "The isolated expiry scenario could not apply its existing official stun.");
            FightStatusPropertyRuntime.RebuildAll(checkpoint.Units, catalog,
                OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula());
        }
    }
}
