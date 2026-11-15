using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryFollowBone;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        public sealed class PortableFlameColdSnapshot
        {
            internal Transform[] Bodies = Array.Empty<Transform>();
            internal MyFightStatus[] Views = Array.Empty<MyFightStatus>();
            public string Checkpoint { get; internal set; }
        }

        [Serializable]
        public sealed class PortableFlameProcessHandoff
        {
            public string SaveRoot, Slot, Checkpoint, FinisherCheckpoint, ClipName;
            public int ProcessId, SkillId, ItemId, Facing, NativeCasts, NativeChildren, Frames;
            public int[] Owners, Instances, ExpiryRounds, Candidates;
            public uint[] SoundPlayingIds;
            public bool Settled, Animated, Captured, SoundEnded;
        }

        private bool PortableFlameColdRequested =>
            Environment.GetEnvironmentVariable("ZJCS_PORTABLE_FLAME_COLD_SCENE") == "1";
        private bool flameColdSceneComplete, flameSoundEndedBeforeColdScene;

        public bool ReadyForPortableFlameColdScene(OfficialBattleRuntimeController controller) =>
            ValidatePortableFlameLife && PortableFlameColdRequested && !flameColdSceneComplete
            && flameRestored && flameRestoreFrame && !controller.IsPresentingStep
            && PortableFlameViews().Length == 4 && flameSoundPlayingIds.Count == 1
            && flameSoundPlayingIds.All(id => ArcNativeSoundEvent(id) == 0);

        public PortableFlameColdSnapshot CapturePortableFlameColdScene(OfficialBattleRuntimeController controller)
        {
            Require(ReadyForPortableFlameColdScene(controller)
                && flameHolders.Count == 4 && flameHolders.Values.All(holder => holder.EndClock < 0)
                && controller.Checkpoint.Units.Single(unit => unit.Id == 1).SkillState.Round == 1
                && !flameSecondRoundFrame,
                "Four live fire rings and their completed original casting sound are required for host restoration.");
            flameSoundEndedBeforeColdScene = true;
            return new PortableFlameColdSnapshot {
                Checkpoint = FightBattleCheckpointSerializer.Save(controller.Checkpoint),
                Bodies = controller.Checkpoint.Units.Select(unit => controller.TryGetUnitTransform(unit.Id)).ToArray(),
                Views = PortableFlameViews()
            };
        }

        public void BindPortableFlameColdScene(OfficialBattleRuntimeController controller, PortableFlameColdSnapshot saved)
        {
            Require(saved.Bodies.All(body => !body) && saved.Views.All(view => !view),
                "An old original body or fire-ring status survived host destruction.");
            Require(FightBattleCheckpointSerializer.Save(controller.Checkpoint) == saved.Checkpoint,
                "Fire-ring resume changed RNG, receipts, positions, sources or remaining lifetimes.");
            var hero = controller.TryGetUnitTransform(1);
            var views = PortableFlameViews();
            Require(hero && views.Length == 4, "Four original fire-ring status views were not recreated.");
            foreach (var holder in flameHolders.Values)
            {
                var owner = controller.Checkpoint.Units.Single(unit => unit.Id == holder.OwnerId);
                var body = controller.TryGetUnitTransform(owner.Id);
                Require(body, "A restored friendly fire-ring body is missing: " + owner.Id);
                var status = owner.Statuses.Single(value => value.ClassId == 24210);
                var view = views.Single(value => value.Context.Owner == body.gameObject);
                Require(body && view.Context.Creator == hero.gameObject
                    && view.GetComponent<MyEntity>().Id.EntityId == holder.InstanceId
                    && !view.GetComponent<MyEntity>().myNewEntity
                    && status.InstanceId == holder.InstanceId && status.StartRound + 2 == holder.ExpiryRound
                    && status.DurationRound == 2 && status.CreatorId == 1 && status.ApplyingSkillItemId == 10061
                    && status.SourceSkillRank == 2 && status.SourceSkillLevel == 20 && !status.CanDispersed,
                    "Restored original fire ring lost its owner/instance identity, creator, cultivation or expiry: " + owner.Id);
            }
            flameColdSceneComplete = true;
            flameDeadline = Time.realtimeSinceStartup + 180f;
            BindPresentation(controller);
        }

        public bool PortableFlameColdSceneVisible(OfficialBattleRuntimeController controller)
        {
            ObservePortableFlameSoundFrame(controller);
            return flameHolders.Keys.All(id => {
                var body = controller.TryGetUnitTransform(id);
                return body && UnityEngine.Object.FindObjectsOfType<MyPlayableFollowBone>().Any(follow =>
                    follow.myTarget == body.gameObject
                    && follow.GetComponentsInChildren<ParticleSystem>().Any(particle => particle.particleCount > 0)
                    && follow.GetComponentsInChildren<ParticleSystemRenderer>().Any(renderer => renderer.enabled && renderer.isVisible));
            });
        }

        public PortableFlameProcessHandoff CapturePortableFlameProcess(OfficialBattleRuntimeController controller,
            string saveRoot, string slot)
        {
            var snapshot = CapturePortableFlameColdScene(controller);
            Require(firstPromotionItemId == 10061 && skillId == 11200 && settled && animated && captured
                && nativeCasts == 1 && firstPromotionFrameCount == FirstPromotionFrameTimes.Length
                && controller.Checkpoint.Units.Single(unit => unit.Id == 1).SkillState.Round == 1
                && !flameSecondRoundFrame && !finisherScheduled,
                "Fire-ring process handoff must follow its original cast and precede the second owner round.");
            var carrier = FightBattleCheckpointSerializer.Load(snapshot.Checkpoint);
            var unit = carrier.Units.Single(value => value.Id == 1);
            Require(unit.Skills.Count == 0 && unit.SkillState.Slots.Count == 0,
                "The ordinary fire-ring checkpoint still has its diagnostic active skill equipped.");
            unit.Skills.Add(finisher); unit.SkillState.Slots.Add(catalog.CreateSkillSlot(finisher, 0));
            var holders = flameHolders.Values.OrderBy(value => value.OwnerId).ToArray();
            return new PortableFlameProcessHandoff {
                SaveRoot = saveRoot, Slot = slot, Checkpoint = snapshot.Checkpoint,
                FinisherCheckpoint = FightBattleCheckpointSerializer.Save(carrier), ClipName = clipName,
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id, SkillId = skillId,
                ItemId = firstPromotionItemId, Facing = firstPromotionFacingIndex,
                NativeCasts = nativeCasts, NativeChildren = nativeChildren, Frames = firstPromotionFrameCount,
                Owners = holders.Select(value => value.OwnerId).ToArray(),
                Instances = holders.Select(value => value.InstanceId).ToArray(),
                ExpiryRounds = holders.Select(value => value.ExpiryRound).ToArray(), Candidates = flameCandidateIds,
                SoundPlayingIds = flameSoundPlayingIds.ToArray(), SoundEnded = flameSoundEndedBeforeColdScene,
                Settled = settled, Animated = animated, Captured = captured
            };
        }

        public static OfficialBattleGraphCapabilityValidation ResumePortableFlameProcess(
            OfficialBattleRuntimeController controller, PortableFlameProcessHandoff saved)
        {
            Require(saved.ProcessId != System.Diagnostics.Process.GetCurrentProcess().Id
                && saved.SkillId == 11200 && saved.ItemId == 10061 && saved.NativeCasts == 1
                && saved.Settled && saved.Animated && saved.Captured && saved.SoundEnded
                && saved.SoundPlayingIds?.Length == 1 && saved.Frames == FirstPromotionFrameTimes.Length
                && saved.Owners?.Length == 4 && saved.Instances?.Length == 4 && saved.ExpiryRounds?.Length == 4
                && saved.Owners.Distinct().Count() == 4 && saved.Owners.Contains(1) && saved.Candidates?.Length == 4,
                "Fire-ring resume requires complete first-round observations from a different native process.");
            var carrier = FightBattleCheckpointSerializer.Load(saved.FinisherCheckpoint);
            Require(FightBattleCheckpointSerializer.Save(carrier) == saved.FinisherCheckpoint,
                "Fire-ring resume changed the isolated diagnostic finisher definition.");
            var probe = new OfficialBattleGraphCapabilityValidation(OfficialCombatAssetCatalog.LoadDefault().Load(),
                carrier.Units.Single(unit => unit.Id == 1).Skills.Single(), saved.SkillId, saved.ClipName) {
                firstPromotionItemId = saved.ItemId, firstPromotionFacingIndex = saved.Facing,
                nativeCasts = saved.NativeCasts, nativeChildren = saved.NativeChildren,
                firstPromotionFrameCount = saved.Frames, settled = saved.Settled, animated = saved.Animated,
                captured = saved.Captured, flameCandidateIds = saved.Candidates, flamePartyFrame = true,
                flameRestored = true, flameRestoreFrame = true, flameSoundEndedBeforeColdScene = saved.SoundEnded,
                previousCaptureFramerate = Time.captureFramerate
            };
            Require(probe.ValidatePortableFlameLife && probe.PortableFlameColdRequested,
                "Fire-ring process resume requires the original lifecycle and cold-scene diagnostics.");
            for (int index = 0; index < saved.Owners.Length; index++)
                probe.flameHolders.Add(saved.Owners[index], new PortableFlameHolder { OwnerId = saved.Owners[index],
                    InstanceId = saved.Instances[index], ExpiryRound = saved.ExpiryRounds[index] });
            probe.flameSoundPlayingIds.UnionWith(saved.SoundPlayingIds);
            Time.captureFramerate = 60;
            probe.BindPortableFlameColdScene(controller, new PortableFlameColdSnapshot { Checkpoint = saved.Checkpoint });
            return probe;
        }
    }
}
