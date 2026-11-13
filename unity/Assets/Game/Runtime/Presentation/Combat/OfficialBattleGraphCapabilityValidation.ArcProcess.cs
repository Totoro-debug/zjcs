using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        [Serializable]
        public sealed class ArcProcessHandoff
        {
            public string SaveRoot, Slot, Checkpoint, FinisherCheckpoint, ClipName;
            public int ProcessId, SkillId, ItemId, Facing, NativeCasts, NativeChildren, Frames, Instance, ForeignTurns;
            public long BaseBlock, BaseFixed;
            public int[] VisibleRounds;
            public bool Settled, Animated, Captured;
        }

        public ArcProcessHandoff CaptureArcProcess(OfficialBattleRuntimeController controller,
            string saveRoot, string slot)
        {
            var snapshot = CaptureArcColdScene(controller);
            Require(firstPromotionItemId == 10024 && skillId == 10760 && settled && animated && captured
                && nativeCasts == 1 && firstPromotionFrameCount == FirstPromotionFrameTimes.Length
                && arcEndCount == 0 && arcRoundFrames.SetEquals(new[] { 1 }) && !finisherScheduled,
                "Arc process handoff must follow the real initial cast and precede its second owner round.");
            var carrier = FightBattleCheckpointSerializer.Load(snapshot.Checkpoint);
            var unit = carrier.Units.Single(value => value.Id == 1);
            Require(unit.Skills.Count == 0 && unit.SkillState.Slots.Count == 0,
                "The arc process checkpoint still has its diagnostic skill equipped.");
            // Only this separate carrier contains the diagnostic finisher.
            // The ordinary persisted battle remains byte-for-byte unchanged.
            unit.Skills.Add(finisher); unit.SkillState.Slots.Add(catalog.CreateSkillSlot(finisher, 0));
            string finisherCheckpoint = FightBattleCheckpointSerializer.Save(carrier);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(finisherCheckpoint))
                == finisherCheckpoint, "The isolated arc finisher did not round-trip.");
            return new ArcProcessHandoff {
                SaveRoot = saveRoot, Slot = slot, Checkpoint = snapshot.Checkpoint, FinisherCheckpoint = finisherCheckpoint,
                ClipName = clipName, ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                SkillId = skillId, ItemId = firstPromotionItemId, Facing = firstPromotionFacingIndex,
                NativeCasts = nativeCasts, NativeChildren = nativeChildren, Frames = firstPromotionFrameCount,
                Instance = arcInstance, ForeignTurns = arcForeignTurns, BaseBlock = arcBaseBlock, BaseFixed = arcBaseFixed,
                VisibleRounds = arcRoundFrames.OrderBy(value => value).ToArray(),
                Settled = settled, Animated = animated, Captured = captured
            };
        }

        public static OfficialBattleGraphCapabilityValidation ResumeArcProcess(
            OfficialBattleRuntimeController controller, ArcProcessHandoff saved)
        {
            Require(saved.ProcessId != System.Diagnostics.Process.GetCurrentProcess().Id
                && saved.SkillId == 10760 && saved.ItemId == 10024 && saved.NativeCasts == 1
                && saved.Settled && saved.Animated && saved.Captured && saved.Instance > 0
                && saved.Frames == FirstPromotionFrameTimes.Length
                && saved.VisibleRounds != null && saved.VisibleRounds.SequenceEqual(new[] { 1 }),
                "Arc resume requires the first owner round observations from a different native process.");
            var carrier = FightBattleCheckpointSerializer.Load(saved.FinisherCheckpoint);
            Require(FightBattleCheckpointSerializer.Save(carrier) == saved.FinisherCheckpoint,
                "Arc resume changed the diagnostic finisher's complete definition.");
            var probe = new OfficialBattleGraphCapabilityValidation(OfficialCombatAssetCatalog.LoadDefault().Load(),
                carrier.Units.Single(value => value.Id == 1).Skills.Single(), saved.SkillId, saved.ClipName) {
                firstPromotionItemId = saved.ItemId, firstPromotionFacingIndex = saved.Facing,
                nativeCasts = saved.NativeCasts, nativeChildren = saved.NativeChildren,
                firstPromotionFrameCount = saved.Frames, settled = saved.Settled,
                animated = saved.Animated, captured = saved.Captured,
                arcInstance = saved.Instance, arcForeignTurns = saved.ForeignTurns,
                arcBaseBlock = saved.BaseBlock, arcBaseFixed = saved.BaseFixed,
                arcRestored = true, arcStartClock = Time.time, previousCaptureFramerate = Time.captureFramerate
            };
            Require(probe.ValidateArcShield && probe.ArcColdSceneRequested,
                "Arc process resume requires the original arc-shield lifecycle diagnostic.");
            probe.arcRoundFrames.UnionWith(saved.VisibleRounds);
            Time.captureFramerate = 60;
            probe.BindArcColdScene(controller, new ArcColdSceneSnapshot { Checkpoint = saved.Checkpoint });
            return probe;
        }
    }
}
