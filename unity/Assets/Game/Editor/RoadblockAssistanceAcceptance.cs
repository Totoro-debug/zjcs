using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.State;
using Zjcs.Offline.World;

namespace Zjcs.Editor
{
    public static class RoadblockAssistanceAcceptance
    {
        public static void Run()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Use an isolated validation workspace.");
            var slots = new ProfileSlotCatalog(Path.Combine(output, "roadblock-usage-rules"));
            string slot = slots.CreateSlot();
            var progress = OfflineNpcAssistanceProgress.Open(slots, slot);
            const int play = 1, npc = 8402;
            const string gate = "map14:roadblock:701";
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            Require(progress.SetSelection(play, gate, new[] { npc }, 3), "Selection was not saved.");
            Require(progress.RecordUsage(play, gate, 3, 1, -1, NpcAssistRefreshCycle.Daily, now).SequenceEqual(new[] { npc }), "Wrong committed selection.");
            Require(progress.AssistCount(play, npc, NpcAssistRefreshCycle.Daily, now) == 1, "Usage was not charged.");
            // Simulate process loss before the separate map file was committed.
            progress = OfflineNpcAssistanceProgress.Open(slots, slot);
            Require(progress.HasRecordedUsage(play, gate), "Gate receipt was not durable.");
            Require(progress.RecordUsage(play, gate, 3, 1, -1, NpcAssistRefreshCycle.Daily, now).SequenceEqual(new[] { npc }), "Retry lost its original selection.");
            Require(progress.AssistCount(play, npc, NpcAssistRefreshCycle.Daily, now) == 1, "Retry charged usage twice.");
            long tomorrow = now + 48 * 60 * 60;
            progress.RecordUsage(play, gate, 3, 1, -1, NpcAssistRefreshCycle.Daily, tomorrow);
            Require(progress.AssistCount(play, npc, NpcAssistRefreshCycle.Daily, tomorrow) == 0, "Completed gate charged usage after daily reset.");
            const string solo = "map14:roadblock:2701";
            Require(progress.RecordUsage(play, solo, 3, 1, -1, NpcAssistRefreshCycle.Daily, now).Length == 0, "Solo gate changed invitation selection.");
            progress = OfflineNpcAssistanceProgress.Open(slots, slot);
            Require(progress.HasRecordedUsage(play, solo), "Solo completion was not recoverable.");
            const string otherGate = "map14:roadblock:3801";
            progress.SetSelection(play, otherGate, new[] { npc }, 3);
            bool refused = false;
            try { progress.RecordUsage(play, otherGate, 3, 1, -1, NpcAssistRefreshCycle.Daily, now); }
            catch (InvalidOperationException) { refused = true; }
            Require(refused && !progress.HasRecordedUsage(play, otherGate), "Rejected usage completed another gate.");
            Debug.Log("Roadblock assistance durability passed: retry, cold load, daily reset, solo and rejected commits.");
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidDataException(message); }
    }
}
