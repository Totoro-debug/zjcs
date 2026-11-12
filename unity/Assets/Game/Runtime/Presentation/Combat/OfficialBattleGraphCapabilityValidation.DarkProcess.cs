using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        // Test observations live beside the isolated profile, never in a game save.
        [Serializable]
        public sealed class DarkProcessHandoff
        {
            public string SaveRoot, Slot, Checkpoint, ClipName, FinisherCheckpoint;
            public int ProcessId, SkillId, ItemId, Facing, NativeCasts, NativeChildren, Frames;
            public int Owner, Instance, Count;
            public bool Settled, Animated, Captured;
        }

        public DarkProcessHandoff CaptureDarkProcess(OfficialBattleRuntimeController controller,
            string saveRoot, string slot)
        {
            var snapshot = CaptureDarkColdScene(controller);
            Require(firstPromotionItemId == 10029 && skillId == 10810 && settled && animated && captured
                && nativeCasts == 1 && firstPromotionFrameCount == FirstPromotionFrameTimes.Length
                && snapshot.Count == 3 && darkStatusRuleCounts.Count == 0 && !finisherScheduled,
                "Process handoff must follow the real initial cast and precede the first dark-status tick.");
            // This separate copy only carries the diagnostic finisher. Retain a
            // real, valid battle header/roster; never apply it to the live runner.
            var carrier = FightBattleCheckpointSerializer.Load(snapshot.Checkpoint);
            var unit = carrier.Units.Single(value => value.Id == 1);
            Require(unit.Skills.Count == 0 && unit.SkillState.Slots.Count == 0,
                "The captured diagnostic source still has an ordinary skill equipped.");
            unit.Skills.Add(finisher); unit.SkillState.Slots.Add(catalog.CreateSkillSlot(finisher, 0));
            string finisherCheckpoint = FightBattleCheckpointSerializer.Save(carrier);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(finisherCheckpoint))
                == finisherCheckpoint, "The complete finisher carrier did not round-trip before process exit.");
            return new DarkProcessHandoff {
                SaveRoot = saveRoot, Slot = slot, Checkpoint = snapshot.Checkpoint, ClipName = clipName,
                FinisherCheckpoint = finisherCheckpoint,
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                SkillId = skillId, ItemId = firstPromotionItemId, Facing = firstPromotionFacingIndex,
                NativeCasts = nativeCasts, NativeChildren = nativeChildren, Frames = firstPromotionFrameCount,
                Owner = snapshot.Owner, Instance = snapshot.Instance, Count = snapshot.Count,
                Settled = settled, Animated = animated, Captured = captured
            };
        }

        public static OfficialBattleGraphCapabilityValidation ResumeDarkProcess(
            OfficialBattleRuntimeController controller, DarkProcessHandoff saved)
        {
            Require(saved.ProcessId != System.Diagnostics.Process.GetCurrentProcess().Id
                && saved.SkillId == 10810 && saved.ItemId == 10029 && saved.NativeCasts == 1
                && saved.Settled && saved.Animated && saved.Captured
                && saved.Frames == FirstPromotionFrameTimes.Length && saved.Count == 3,
                "Resume requires the completed observations from a different native process.");
            // Keep every nested skill field through the ordinary checkpoint serializer.
            // JsonUtility cannot represent nested lists in a skill definition.
            var carrier = FightBattleCheckpointSerializer.Load(saved.FinisherCheckpoint);
            Require(FightBattleCheckpointSerializer.Save(carrier) == saved.FinisherCheckpoint,
                "The isolated finisher's complete definition changed in its diagnostic handoff.");
            var probe = new OfficialBattleGraphCapabilityValidation(OfficialCombatAssetCatalog.LoadDefault().Load(),
                carrier.Units.Single(value => value.Id == 1).Skills.Single(), saved.SkillId, saved.ClipName) {
                firstPromotionItemId = saved.ItemId, firstPromotionFacingIndex = saved.Facing,
                nativeCasts = saved.NativeCasts, nativeChildren = saved.NativeChildren,
                firstPromotionFrameCount = saved.Frames, settled = saved.Settled,
                animated = saved.Animated, captured = saved.Captured,
                darkStatusOwner = saved.Owner, darkStatusInstance = saved.Instance,
                darkStatusRestored = true, previousCaptureFramerate = Time.captureFramerate
            };
            Require(probe.ValidateDarkStatus && !probe.ValidateFirstPromotionStatusViews,
                "Process resume requires only the original dark-status lifecycle diagnostic.");
            Time.captureFramerate = 60;
            probe.BindDarkColdScene(controller, new DarkColdSceneSnapshot {
                Owner = saved.Owner, Instance = saved.Instance, Count = saved.Count, Checkpoint = saved.Checkpoint
            });
            return probe;
        }
    }
}
