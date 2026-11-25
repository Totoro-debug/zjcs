using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Editor.HomeKeyedCondition
{
    public static class PlayerSessionNotificationsValidation
    {
        sealed class QueuedContext : SynchronizationContext
        {
            readonly Queue<(SendOrPostCallback callback, object state)> queue = new Queue<(SendOrPostCallback, object)>();
            public override void Post(SendOrPostCallback callback, object state)
            { lock (queue) queue.Enqueue((callback, state)); }
            public int Count { get { lock (queue) return queue.Count; } }
            public void Drain()
            {
                for (;;)
                {
                    (SendOrPostCallback callback, object state) item;
                    lock (queue) { if (queue.Count == 0) return; item = queue.Dequeue(); }
                    item.callback(item.state);
                }
            }
        }

        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace)) throw new InvalidOperationException("An isolated validation workspace is required.");
            var slots = new ProfileSlotCatalog(Path.Combine(workspace, "session-notifications-save-" + Guid.NewGuid().ToString("N")));
            string first = slots.CreateSlot(), second = slots.CreateSlot();
            var writerA = OfflineMainlineProgressSession.OpenCharacter(slots, first);
            var writerB = OfflineMainlineProgressSession.OpenCharacter(slots, second);
            var observerA = OfflineMainlineProgressSession.OpenCharacter(slots, first);
            int callsA = 0, callsB = 0, endedA = 0, endedB = 0, lastLevel = 0;
            IDisposable watchA = OfflinePlayerSessionNotifications.Subscribe(slots, first,
                () => { observerA.Reload(); lastLevel = observerA.Save.Player.Level; callsA++; }, () => endedA++);
            IDisposable watchB = OfflinePlayerSessionNotifications.Subscribe(slots, second, () => callsB++, () => endedB++);
            Require(writerA.ApplyCharacterMutation(player => player.Level = 10, "session-test:first").IsApplied,
                "The first isolated save must commit.");
            Require(callsA == 1 && callsB == 0 && lastLevel == 10, "Only the matching profile sees durable cross-instance data.");
            Require(writerB.ApplyCharacterMutation(player => player.Level = 20, "session-test:second").IsApplied,
                "The second isolated save must commit.");
            Require(callsA == 1 && callsB == 1, "Another profile must not notify the first profile.");
            Require(!writerA.ApplyCharacterMutation(player => player.Level = -1, "session-test:invalid").IsApplied
                && callsA == 1, "Rejected transactions must not publish.");
            Require(writerA.ApplyCharacterMutation(player => player.Level = 11, "session-test:first").Status == ProgressionResultStatus.Unchanged
                && callsA == 1, "Repeated receipts must not publish.");
            watchB.Dispose();
            Require(writerB.ApplyCharacterMutation(player => player.Level = 21, "session-test:disposed").IsApplied && callsB == 1,
                "Disposed listeners must not receive subsequent commits.");
            OfflineMainlineProgressSession.EndPlayerSession();
            Require(endedA == 1 && endedB == 0, "Ending the player session must notify only active subscriptions.");
            Require(writerA.ApplyCharacterMutation(player => player.Level = 11, "session-test:ended").IsApplied && callsA == 1,
                "Ended subscriptions must not receive later commits.");
            watchA.Dispose();

            var reloaded = OfflineMainlineProgressSession.OpenCharacter(slots, first);
            Require(reloaded.Save.Player.Level == 11, "The isolated saved character must survive session restart.");
            var context = new QueuedContext();
            var previous = SynchronizationContext.Current;
            IDisposable watchThread;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                watchThread = OfflinePlayerSessionNotifications.Subscribe(slots, first, () => callsA++);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
            ProgressionResult result;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                result = reloaded.ApplyCharacterMutation(player => player.Level = 12, "session-test:thread");
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
            Require(result.IsApplied && callsA == 1 && context.Count == 1,
                "Commits without the subscriber context must queue callbacks to it.");
            context.Drain();
            Require(callsA == 2, "The queued callback must execute on the subscriber's context.");
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                result = reloaded.ApplyCharacterMutation(player => player.Level = 13, "session-test:queued-cancel");
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
            Require(result.IsApplied && context.Count == 1, "The next commit must enqueue another callback.");
            watchThread.Dispose();
            context.Drain();
            Require(callsA == 2, "Disposing a subscription must suppress already-queued commit callbacks.");

            IDisposable noContext;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                noContext = OfflinePlayerSessionNotifications.Subscribe(slots, first, () => callsA++);
                result = reloaded.ApplyCharacterMutation(player => player.Level = 14, "session-test:no-context");
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
            Require(result.IsApplied && callsA == 3, "Subscriptions without a context must run synchronously.");
            noContext.Dispose();

            var loggerEnabled = Debug.unityLogger.logEnabled;
            IDisposable failing = OfflinePlayerSessionNotifications.Subscribe(slots, first,
                () => throw new InvalidOperationException("Expected subscriber failure"));
            int afterFailure = 0;
            IDisposable remaining = OfflinePlayerSessionNotifications.Subscribe(slots, first, () => afterFailure++);
            try
            {
                Debug.unityLogger.logEnabled = false;
                result = reloaded.ApplyCharacterMutation(player => player.Level = 15, "session-test:subscriber-failure");
            }
            finally { Debug.unityLogger.logEnabled = loggerEnabled; }
            Require(result.IsApplied && afterFailure == 1 && OfflineMainlineProgressSession.OpenCharacter(slots, first).Save.Player.Level == 15,
                "A failing subscriber must not change the saved result or stop other listeners.");
            failing.Dispose(); remaining.Dispose();
            OfflineMainlineProgressSession.EndPlayerSession();
            Debug.Log("OFFLINE_PLAYER_SESSION_NOTIFICATIONS_VALIDATION_PASSED");
        }
    }
}
