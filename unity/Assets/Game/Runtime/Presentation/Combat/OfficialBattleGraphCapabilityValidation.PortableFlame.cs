using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySounds;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidatePortableFlameLife =>
            Environment.GetEnvironmentVariable("ZJCS_PORTABLE_FLAME_LIFECYCLE") == "1";

        private sealed class PortableFlameHolder
        {
            public int OwnerId, InstanceId, ExpiryRound;
            public float EndClock = -1f;
        }

        private readonly Dictionary<int, PortableFlameHolder> flameHolders = new Dictionary<int, PortableFlameHolder>();
        private readonly HashSet<int> flameVisibleOwners = new HashSet<int>();
        private readonly List<MyPlayable> flameGraphs = new List<MyPlayable>();
        // Original playable11200 references Skill_10061 / A90E36C7-577F-4BAF-8415-2976D6EF638F.
        private const uint PortableFlameSoundEventId = 3828123635u;
        private readonly HashSet<uint> flameSoundPlayingIds = new HashSet<uint>();
        private int[] flameCandidateIds = Array.Empty<int>();
        private bool flamePartyFrame, flameRestored, flameRestoreFrame, flameSecondRoundFrame, flameComplete;
        private float flameDeadline, flameRestoreClock;

        private void ConfigurePortableFlameDiagnostic(FightBattleCheckpoint checkpoint)
        {
            if (!ValidatePortableFlameLife) return;
            Require(firstPromotionItemId == 10061 && skillId == 11200,
                "The fire-ring lifecycle requires original 10061/11200.");
            if (ValidatePortableFlameReplacement) { ConfigurePortableFlameReplacement(checkpoint); return; }
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var enemy = checkpoint.Units.First(value => value.Side != player.Side);
            // Four controlled friendly original 3331 models compete for three slots.
            // They keep their original native models and have no equipped active skills.
            var offsets = new[] { new FightGridPoint(1, 0), new FightGridPoint(-1, 0),
                new FightGridPoint(0, -1), new FightGridPoint(1, -1) };
            if (ValidatePortableFlameMovement || ValidatePortableFlameEnemyEntry)
            {
                Require(firstPromotionFacingIndex == 2 && !PortableFlameColdRequested,
                    "The independent moving fire-ring diagnostic faces up without a cold-host handoff.");
                // Leave one legal cardinal approach cell between caster and target.
                // Every friendly candidate is adjacent to the enemy, so the original
                // cast still proves the distant creator's ally-held damage source.
                enemy.X = player.X; enemy.Y = player.Y + 2;
                offsets = new[] { new FightGridPoint(-1, 1), new FightGridPoint(1, 1),
                    new FightGridPoint(-1, 2), new FightGridPoint(1, 2) };
            }
            var ids = new List<int>();
            foreach (var offset in offsets)
            {
                var ally = JsonUtility.FromJson<FightBattleUnitState>(JsonUtility.ToJson(enemy));
                ally.Id = checkpoint.Units.Max(value => value.Id) + 1; ally.Turn.Id = ally.Id;
                ally.Side = player.Side; ally.X = player.X + offset.X; ally.Y = player.Y + offset.Y;
                Require(ally.X >= 0 && ally.Y >= 0 && ally.X < checkpoint.Grid.Width && ally.Y < checkpoint.Grid.Height
                    && checkpoint.Units.All(value => value.X != ally.X || value.Y != ally.Y),
                    "The controlled friendly fire-ring footprint must be free and inside the original grid.");
                checkpoint.Units.Add(ally); ids.Add(ally.Id);
            }
            flameCandidateIds = ids.ToArray();
            foreach (var unit in checkpoint.Units)
            {
                unit.Turn.Speed = 1000;
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (property.Id == catalog.PropertyType("Speed")) property.Value = 1000;
                    if (property.Id == catalog.PropertyType("MoveDist")
                        || property.Id == catalog.PropertyType("FixedMoveDist")) property.Value = 0;
                }
            }
            checkpoint.MaxRound = 24;
        }

        private MyFightStatus[] PortableFlameViews() => UnityEngine.Object.FindObjectsOfType<MyFightStatus>()
            .Where(view => view.GetComponent<MyEntity>()?.ClassId == 24210).ToArray();

        private void ObservePortableFlameSoundFrame(OfficialBattleRuntimeController controller)
        {
            if (!ValidatePortableFlameLife || nativeCasts == 0) return;
            var body = controller.TryGetUnitTransform(1);
            foreach (var sound in UnityEngine.Object.FindObjectsOfType<MyPlayableSound>())
            {
                if (sound.SoundEvent?.Id != PortableFlameSoundEventId || sound.SoundEvent.PlayingId == 0) continue;
                var second = ValidatePortableFlameReplacement ? controller.TryGetUnitTransform(flameReplacementCaster) : null;
                Require(body && (sound.mySource == body.gameObject || sound.myTarget == body.gameObject
                    || second && (sound.mySource == second.gameObject || sound.myTarget == second.gameObject)),
                    "Original Skill_10061 sound lost its casting character.");
                uint playing = sound.SoundEvent.PlayingId;
                Require(!flameColdSceneComplete || ArcNativeSoundEvent(playing) != PortableFlameSoundEventId,
                    "Restoring fire rings replayed the original casting sound.");
                if (ArcNativeSoundEvent(playing) == PortableFlameSoundEventId && flameSoundPlayingIds.Add(playing))
                    Debug.Log("[Portable flame sound] Original Skill_10061 event=" + PortableFlameSoundEventId
                        + " playing=" + playing + " is live in the native engine.");
            }
        }

        private void ObservePortableFlameStep(FightBattleCheckpoint checkpoint)
        {
            if (!ValidatePortableFlameLife || flameComplete) return;
            if (ValidatePortableFlameReplacement) { ObservePortableFlameReplacementStep(checkpoint); return; }
            var step = checkpoint.Receipts.Last().Event;
            foreach (var value in step.StatusViewEvents.Where(value => value.Status.ClassId == 24210))
            {
                if (value.Kind == FightBattleStatusViewEventKind.Start)
                {
                    var owner = checkpoint.Units.Single(unit => unit.Id == value.OwnerId);
                    var status = owner.Statuses.Single(item => item.InstanceId == value.Status.InstanceId);
                    Require(!flameHolders.ContainsKey(owner.Id) && status.CreatorId == 1
                        && status.ApplyingSkillItemId == 10061 && status.SourceSkillRank == 2
                        && status.SourceSkillLevel == 20 && status.DurationRound == 2 && !status.CanDispersed,
                        "A friendly fire ring duplicated its recipient or lost original cultivation/lifetime.");
                    flameHolders.Add(owner.Id, new PortableFlameHolder { OwnerId = owner.Id,
                        InstanceId = status.InstanceId, ExpiryRound = status.StartRound + 2 });
                    flameDeadline = Time.realtimeSinceStartup + 180f;
                }
                if (value.Kind == FightBattleStatusViewEventKind.End)
                {
                    Require(flameHolders.TryGetValue(value.OwnerId, out var holder)
                        && holder.EndClock < 0 && step.ActorId == holder.OwnerId && step.ActorRound == holder.ExpiryRound,
                        "A fire ring expired twice or on another unit's personal clock.");
                    holder.EndClock = Time.time + value.Delay;
                }
            }
            if (flameHolders.Count == 0) return;
            Require(flameHolders.Count == 4 && flameHolders.ContainsKey(1)
                && flameHolders.Keys.Count(id => flameCandidateIds.Contains(id)) == 3,
                "Original 10061 must give itself and exactly three of four friendly models one ring each.");
            ObservePortableFlameMovementStep(checkpoint);
            ObservePortableFlameEnemyEntryStep(checkpoint);
        }

        private bool ObservePortableFlameFrame(OfficialBattleRuntimeController controller)
        {
            if (ValidatePortableFlameReplacement) return ObservePortableFlameReplacementFrame(controller);
            if (!ValidatePortableFlameLife || flameComplete || flameHolders.Count == 0) return false;
            Require(Time.realtimeSinceStartup < flameDeadline, "Original friendly fire-ring lifecycle timed out.");
            var views = PortableFlameViews();
            var renderedOwners = new HashSet<int>();
            foreach (var view in views)
            {
                int instance = view.GetComponent<MyEntity>().Id.EntityId;
                var holder = flameHolders.Values.Single(value =>
                    controller.TryGetUnitTransform(value.OwnerId)?.gameObject == view.Context.Owner);
                var body = controller.TryGetUnitTransform(holder.OwnerId);
                Require(body && view.Context.Owner == body.gameObject && instance == holder.InstanceId,
                    "A native fire ring attached to the wrong friendly body.");
                var playable = (MyPlayable)typeof(MyFightStatus)
                    .GetField("mPlayable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                if (playable && !flameGraphs.Contains(playable)) flameGraphs.Add(playable);
                bool rendered = UnityEngine.Object.FindObjectsOfType<MyPlayableFollowBone>().Any(follow =>
                    follow.myTarget == body.gameObject
                    && follow.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0)
                    && follow.GetComponentsInChildren<ParticleSystemRenderer>().Any(renderer => renderer.enabled && renderer.isVisible));
                if (rendered) { renderedOwners.Add(holder.OwnerId); flameVisibleOwners.Add(holder.OwnerId); }
            }
            if (!flamePartyFrame && renderedOwners.Count == 4)
            {
                flamePartyFrame = true; CaptureName = "portable-flame-party"; return true;
            }
            if (!captured) return false;
            if (flamePartyFrame && !flameRestored && views.Length == 4 && !controller.IsPresentingStep)
            {
                string path = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "portable-flame-checkpoint.json");
                string saved = FightBattleCheckpointSerializer.Save(controller.Checkpoint);
                File.WriteAllText(path, saved);
                var state = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(
                    FightBattleCheckpointSerializer.Load(File.ReadAllText(path))).State;
                Require(FightBattleCheckpointSerializer.Save(state) == saved, "Restoring friendly rings changed the checkpoint.");
                var old = views.ToDictionary(view => view.Context.Owner,
                    view => Tuple.Create(view, view.GetComponent<MyEntity>().Id.EntityId));
                var graph = (OfficialBattleSkillGraphPlayback)typeof(OfficialBattleRuntimeController)
                    .GetField("skillGraphPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                graph.RefreshGridSnapshot(state);
                var restored = PortableFlameViews();
                Require(restored.Length == 4 && restored.All(view =>
                    old.TryGetValue(view.Context.Owner, out var previous)
                    && view != previous.Item1 && view.GetComponent<MyEntity>().Id.EntityId == previous.Item2
                    && !view.GetComponent<MyEntity>().myNewEntity),
                    "Friendly fire rings did not rebuild four independent original non-fresh status views.");
                flameRestored = true; flameRestoreClock = Time.time; return true;
            }
            if (flameRestored && !flameRestoreFrame && Time.time >= flameRestoreClock + .15f && renderedOwners.Count == 4)
            {
                flameRestoreFrame = true; CaptureName = "portable-flame-restored"; return true;
            }
            if (ObservePortableFlameMovementFrame(controller)) return true;
            if (ObservePortableFlameEnemyEntryFrame(controller)) return true;
            var player = controller.Checkpoint.Units.Single(value => value.Id == 1);
            if (!flameSecondRoundFrame && player.SkillState.Round == 2 && renderedOwners.Contains(1))
            {
                flameSecondRoundFrame = true; CaptureName = "portable-flame-owner-round-2"; return true;
            }
            if (flameHolders.Values.All(holder => holder.EndClock >= 0)
                && Time.time >= flameHolders.Values.Max(holder => holder.EndClock) + 1f)
            {
                Require(views.Length == 0 && flamePartyFrame && flameRestoreFrame && flameSecondRoundFrame
                    && flameVisibleOwners.Count == 4 && flameGraphs.Count >= 4
                    && flameGraphs.All(playable => !playable || playable.myStatus == MyPlayable.PlayableStatus.Stop),
                    "Original friendly fire-ring particles, disk rebind, two personal rounds or native retirement were incomplete.");
                flameComplete = true; CaptureName = "portable-flame-ended";
                Debug.Log("[Portable flame lifecycle] Four friendly original status views, disk rebind, two personal rounds and retirement passed.");
                return true;
            }
            return true;
        }

        private void VerifyPortableFlameDiagnostic()
        {
            if (!ValidatePortableFlameLife) return;
            if (ValidatePortableFlameReplacement) { VerifyPortableFlameReplacement(); return; }
            Require(flameComplete, "Original friendly fire-ring lifecycle did not finish.");
            VerifyPortableFlameMovement();
            VerifyPortableFlameEnemyEntry();
            Require(!PortableFlameColdRequested || flameColdSceneComplete,
                "Original friendly fire-ring host restoration did not finish.");
            // A new native sound host may reuse old playing IDs. Completion was
            // verified before teardown; restored statuses must not post the cast again.
            Require(flameSoundPlayingIds.Count == 1 && (flameSoundEndedBeforeColdScene
                || flameSoundPlayingIds.All(id => ArcNativeSoundEvent(id) == 0)),
                "Original Skill_10061 sound did not play once and finish through the native engine.");
            Debug.Log("[Portable flame sound] Original Skill_10061 native playback and completion passed.");
        }
    }
}
