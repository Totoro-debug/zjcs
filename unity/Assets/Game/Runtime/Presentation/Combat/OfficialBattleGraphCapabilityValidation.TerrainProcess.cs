using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        [Serializable]
        public sealed class TerrainProcessHandoff
        {
            public string SaveRoot, Slot, Checkpoint;
            public int ProcessId, ItemId, TriggerCount;
            public int[] GridIds;
            public bool HitScope;
        }

        public TerrainProcessHandoff CaptureTerrainProcess(OfficialBattleRuntimeController controller,
            string saveRoot, string slot)
        {
            var snapshot = CaptureTerrainScopeColdScene(controller);
            Require(settled && animated && captured && nativeCasts == 1
                && firstPromotionFrameCount == FirstPromotionFrameTimes.Length && !controller.Checkpoint.Finished,
                "Terrain process exit must follow the original action and precede saved ground expiry.");
            return new TerrainProcessHandoff {
                SaveRoot = saveRoot, Slot = slot, Checkpoint = snapshot.Checkpoint,
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ItemId = firstPromotionItemId, TriggerCount = terrainScopeSavedTriggerCount,
                GridIds = snapshot.Ids.ToArray(), HitScope = ValidateHitScopeLifecycle
            };
        }

        public sealed class TerrainProcessResumeProbe : IDisposable
        {
            private readonly OfficialBattleRuntimeController controller;
            private readonly OfficialBattleGraphCapabilityValidation probe;
            private readonly Action<FightBattleExecutionResult> stepCompleted;
            public bool Ended => probe.terrainScopeComplete;

            public TerrainProcessResumeProbe(OfficialBattleRuntimeController controller, TerrainProcessHandoff saved)
            {
                this.controller = controller;
                Require(saved.ProcessId != System.Diagnostics.Process.GetCurrentProcess().Id
                    && FightBattleCheckpointSerializer.Save(controller.Checkpoint) == saved.Checkpoint,
                    "Terrain resume requires the exact ordinary save in a different Windows process.");
                Require(saved.HitScope ? saved.ItemId == 10072 : saved.ItemId == 10029 || saved.ItemId == 10055,
                    "Unexpected terrain process source skill.");
                // Observe the ordinary restored runner directly; no fixture skill,
                // status, receipt or finisher is inserted into the saved battle.
                probe = new OfficialBattleGraphCapabilityValidation(OfficialCombatAssetCatalog.LoadDefault().Load(),
                    null, 0, null) { captured = true, terrainScopeSavedTriggerCount = saved.TriggerCount };
                Require(probe.ValidateTerrainScopeLifecycle && probe.ValidateHitScopeLifecycle == saved.HitScope
                    && saved.GridIds != null && saved.GridIds.Length >= probe.TerrainScopeMinimumTiles
                    && saved.GridIds.Distinct().Count() == saved.GridIds.Length,
                    "Terrain process mode or saved original grid identities differ.");
                probe.terrainScopeSnapshot = new TerrainScopeColdSnapshot {
                    Ids = saved.GridIds.ToArray(), Checkpoint = saved.Checkpoint,
                    // Unity references cannot cross a process boundary. The distinct
                    // PID replaces the in-process old-object destruction assertions.
                    Bodies = Array.Empty<Transform>(), Views = Array.Empty<MyFightStatus>(),
                    Playables = Array.Empty<MyPlayable>()
                };
                Require(probe.TerrainScopeTriggerCount(controller.Checkpoint) == saved.TriggerCount,
                    "Restoring the ordinary terrain save repeated its passive trigger.");
                probe.BindTerrainScopeColdScene(controller, probe.terrainScopeSnapshot);
                stepCompleted = _ => probe.ObserveTerrainScopeStep(controller);
                controller.StepCompleted += stepCompleted;
            }

            public bool FireVisible() => probe.TerrainScopeColdFireVisible(controller);
            public void ObserveFrame() => probe.ObserveTerrainScopeFrame(controller);
            public string TakeCapture()
            {
                string name = probe.CaptureName;
                probe.CaptureName = null;
                return name;
            }
            public void Dispose() => controller.StepCompleted -= stepCompleted;
        }
    }
}
