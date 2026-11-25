using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Zjcs.Offline.State;

namespace Zjcs.Offline.Progression
{
    // Per-profile notifications for successful commits made by any session instance.
    // Callbacks observe durable data by reloading; no mutable save object is exposed.
    public static class OfflinePlayerSessionNotifications
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, List<Subscription>> Subscriptions =
            new Dictionary<string, List<Subscription>>(StringComparer.OrdinalIgnoreCase);

        public static IDisposable Subscribe(ProfileSlotCatalog slots, string slotId, Action committed, Action ended = null)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (committed == null) throw new ArgumentNullException(nameof(committed));
            if (!slots.Contains(slotId)) throw new DirectoryNotFoundException("The selected profile slot does not exist.");
            string path = Path.GetFullPath(slots.GetProgressPath(slotId));
            var subscription = new Subscription(path, committed, ended, SynchronizationContext.Current);
            lock (Gate)
            {
                if (!Subscriptions.TryGetValue(path, out List<Subscription> list))
                    Subscriptions.Add(path, list = new List<Subscription>());
                list.Add(subscription);
            }
            return subscription;
        }

        internal static void PublishCommitted(string savePath)
        {
            Subscription[] targets;
            lock (Gate)
                targets = Subscriptions.TryGetValue(Path.GetFullPath(savePath), out List<Subscription> list)
                    ? list.ToArray() : Array.Empty<Subscription>();
            foreach (Subscription subscription in targets) subscription.DispatchCommit();
        }

        internal static void EndPlayerSession()
        {
            var targets = new List<Subscription>();
            lock (Gate)
            {
                foreach (List<Subscription> list in Subscriptions.Values) targets.AddRange(list);
                Subscriptions.Clear();
                foreach (Subscription subscription in targets) subscription.Active = false;
            }
            foreach (Subscription subscription in targets) subscription.DispatchEnd();
        }

        private sealed class Subscription : IDisposable
        {
            private readonly string path;
            private readonly Action committed, ended;
            private readonly SynchronizationContext context;
            internal bool Active = true;

            internal Subscription(string path, Action committed, Action ended, SynchronizationContext context)
            { this.path = path; this.committed = committed; this.ended = ended; this.context = context; }

            internal void DispatchCommit() => Dispatch(() =>
            {
                lock (Gate) if (!Active) return;
                committed();
            });

            internal void DispatchEnd() { if (ended != null) Dispatch(ended); }

            private void Dispatch(Action action)
            {
                void Invoke()
                {
                    try { action(); }
                    catch (Exception error) { Debug.LogException(error); }
                }
                try
                {
                    if (context != null && !ReferenceEquals(SynchronizationContext.Current, context))
                        context.Post(_ => Invoke(), null);
                    else Invoke();
                }
                catch (Exception error) { Debug.LogException(error); }
            }

            public void Dispose()
            {
                lock (Gate)
                {
                    if (!Active) return;
                    Active = false;
                    if (Subscriptions.TryGetValue(path, out List<Subscription> list))
                    {
                        list.Remove(this);
                        if (list.Count == 0) Subscriptions.Remove(path);
                    }
                }
            }
        }
    }
}
