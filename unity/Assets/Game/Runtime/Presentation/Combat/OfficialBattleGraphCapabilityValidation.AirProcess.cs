using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        public bool ReadyForAirProcess => ValidateAirCombo && airComboPaused && !airComboResumed
            && airComboAnimated && airComboAdvanced && airComboSaved;

        [Serializable]
        public sealed class AirProcessHandoff
        {
            public string SaveRoot, Slot, Checkpoint;
            public int ProcessId, Owner, Instance, ItemId, PendingHits;
            public double Remaining, Duration, Elapsed;
        }

        public AirProcessHandoff CaptureAirProcess(OfficialBattleRuntimeController controller,
            string saveRoot, string slot)
        {
            Require(ReadyForAirProcess && controller.IsPaused && !controller.Checkpoint.Finished,
                "Air process exit requires a real, paused, partially consumed original airborne status.");
            var status = controller.Checkpoint.Units.Single(unit => unit.Id == airComboTarget).Statuses
                .Single(value => value.ClassId == 24269);
            var seconds = status.Components.Seconds;
            Require(seconds.RemainingSeconds > 0 && seconds.RemainingSeconds < seconds.DurationSeconds,
                "Air process checkpoint has no consumed seconds.");
            return new AirProcessHandoff {
                SaveRoot = saveRoot, Slot = slot,
                Checkpoint = FightBattleCheckpointSerializer.Save(controller.Checkpoint),
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                Owner = airComboTarget, Instance = status.InstanceId, ItemId = firstPromotionItemId,
                PendingHits = controller.Checkpoint.StatusSeconds.PendingHits.Count,
                Remaining = seconds.RemainingSeconds, Duration = seconds.DurationSeconds,
                Elapsed = controller.Checkpoint.StatusSeconds.ElapsedSeconds
            };
        }

        public sealed class AirProcessResumeProbe : IDisposable
        {
            private readonly OfficialBattleRuntimeController controller;
            private readonly AirProcessHandoff saved;
            private readonly FightBattleExecutor executor;
            private readonly List<string> transitions = new List<string>();
            private Vector3[] restoredPose;
            private bool geometryAdvanced;
            public bool Ended { get; private set; }
            public string CaptureName { get; private set; }

            public AirProcessResumeProbe(OfficialBattleRuntimeController controller, AirProcessHandoff saved)
            {
                this.controller = controller; this.saved = saved;
                Require(saved.ProcessId != System.Diagnostics.Process.GetCurrentProcess().Id
                    && FightBattleCheckpointSerializer.Save(controller.Checkpoint) == saved.Checkpoint,
                    "Air resume must use an unchanged checkpoint in a different Windows process.");
                var status = controller.Checkpoint.Units.Single(unit => unit.Id == saved.Owner).Statuses
                    .Single(value => value.InstanceId == saved.Instance && value.ClassId == 24269);
                // Compare through the real save reader, rather than the observation envelope's
                // JsonUtility double parser, whose last-bit rounding can differ on Mono.
                var expected = FightBattleCheckpointSerializer.Load(saved.Checkpoint);
                var expectedSeconds = expected.Units.Single(unit => unit.Id == saved.Owner).Statuses
                    .Single(value => value.InstanceId == saved.Instance).Components.Seconds;
                Require(status.Components.Seconds.RemainingSeconds == expectedSeconds.RemainingSeconds
                    && status.Components.Seconds.DurationSeconds == expectedSeconds.DurationSeconds
                    && controller.Checkpoint.StatusSeconds.ElapsedSeconds == expected.StatusSeconds.ElapsedSeconds
                    && controller.Checkpoint.StatusSeconds.PendingHits.Count == saved.PendingHits,
                    "Air resume reset the original seconds or pending hits.");
                var body = controller.TryGetUnitTransform(saved.Owner);
                var view = body.GetComponent<MyFightStatusAgent>().MyStatuses.Single(value => value
                    && value.GetComponent<MyEntity>().Id.EntityId == saved.Instance);
                Require(!view.GetComponent<MyEntity>().myNewEntity && view.Context.Owner == body.gameObject
                    && view.ApplyEntity == body.gameObject && !view.ShowSpriteAbovePlayer
                    && view.myPlayablePrefab && view.InvokePlayable == view.myPlayablePrefab,
                    "Air resume did not bind the original non-fresh status and playable to its target.");
                executor = (FightBattleExecutor)typeof(OfficialBattleRuntimeController)
                    .GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                executor.StatusSecondsChanged += Changed;
            }

            public bool AirborneStillActive => controller.Checkpoint.Units.Single(unit => unit.Id == saved.Owner)
                .Statuses.Any(value => value.InstanceId == saved.Instance && value.ClassId == 24269
                    && value.Components.Seconds.RemainingSeconds > 0);

            public bool TryObserveVisibleFloating()
            {
                var body = controller.TryGetUnitTransform(saved.Owner);
                var renderer = body.GetComponentInChildren<BattleNativeMonsterRenderer>();
                var meshRenderer = renderer ? renderer.GetComponent<MeshRenderer>() : null;
                var mesh = renderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                bool visible = renderer && renderer.Animator && renderer.Animation == "floating_01"
                    && meshRenderer && meshRenderer.enabled && meshRenderer.isVisible && !meshRenderer.forceRenderingOff
                    && mesh && mesh.vertexCount > 0 && mesh.vertices.All(v => FiniteAirCombo(v.x)
                        && FiniteAirCombo(v.y) && FiniteAirCombo(v.z));
                if (!visible) return false;
                restoredPose = mesh.vertices;
                return true;
            }

            public void ObserveFrame()
            {
                var body = controller.TryGetUnitTransform(saved.Owner);
                var renderer = body.GetComponentInChildren<BattleNativeMonsterRenderer>();
                var mesh = renderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                Require(mesh && restoredPose != null && mesh.vertices.All(v => FiniteAirCombo(v.x)
                    && FiniteAirCombo(v.y) && FiniteAirCombo(v.z)), "Resumed airborne geometry became invalid.");
                var vertices = mesh.vertices;
                geometryAdvanced |= vertices.Length != restoredPose.Length
                    || vertices.Where((value, index) => (value - restoredPose[index]).sqrMagnitude > 1e-10f).Any();
            }

            private void Changed(FightBattleUnitState owner, FightBattleStatusState status, bool started)
            {
                if (Ended || owner.Id != saved.Owner || status.ClassId != 24269 && status.ClassId != 24270) return;
                transitions.Add(status.ClassId + ":" + started);
                var body = controller.TryGetUnitTransform(owner.Id);
                var views = body.GetComponent<MyFightStatusAgent>().MyStatuses.Where(value => value
                    && value.GetComponent<MyEntity>().Entity.ClassId == status.ClassId).ToArray();
                Require(started ? views.Length == 1 : views.Length == 0,
                    "Resumed airborne/recovery view did not follow its real seconds lifecycle.");
                if (status.ClassId == 24270 && started)
                {
                    Require(!views[0].myPlayablePrefab && !views[0].InvokePlayable,
                        "Resumed original recovery acquired a replacement playable.");
                    CaptureName = "air-process-recovery";
                }
                if (status.ClassId == 24270 && !started)
                {
                    Require(transitions.SequenceEqual(new[] { "24269:False", "24270:True", "24270:False" })
                        && controller.Checkpoint.StatusSeconds.ElapsedSeconds > saved.Elapsed && geometryAdvanced,
                        "Resumed original airborne/recovery timer did not finish in its original order.");
                    Ended = true; CaptureName = "air-process-ended";
                    Debug.Log("[Air process] Original remaining seconds, pending hits and both native view lifecycles passed.");
                }
            }

            public string TakeCapture() { string value = CaptureName; CaptureName = null; return value; }
            public void Dispose() { executor.StatusSecondsChanged -= Changed; }
        }
    }
}
