using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView : IBattleSourceDeathEvents
    {
        private readonly Dictionary<int, List<Action>> sourceDeathListeners =
            new Dictionary<int, List<Action>>();
        private readonly HashSet<int> notifiedSourceDeaths = new HashSet<int>();

        IDisposable IBattleSourceDeathEvents.SubscribeSourceDeath(GameObject source, Action onDie)
        {
            if (source == null || onDie == null || latestCheckpoint?.Units == null) return null;
            foreach (var state in latestCheckpoint.Units)
            {
                if (state == null || Body(state.Id)?.gameObject != source) continue;
                if (!sourceDeathListeners.TryGetValue(state.Id, out var listeners))
                    sourceDeathListeners.Add(state.Id, listeners = new List<Action>());
                listeners.Add(onDie);
                return new SourceDeathSubscription(() => {
                    if (sourceDeathListeners.TryGetValue(state.Id, out var current)) current.Remove(onDie);
                });
            }
            return null;
        }

        private void NotifySourceDeath(int id)
        {
            if (!notifiedSourceDeaths.Add(id) || !sourceDeathListeners.TryGetValue(id, out var listeners)) return;
            Action notify = () => { foreach (var listener in new List<Action>(listeners)) listener(); };
            if (nativePlayableScope != null) nativePlayableScope.Dispatch(notify);
            else notify();
        }

        private sealed class SourceDeathSubscription : IDisposable
        {
            private Action release;
            internal SourceDeathSubscription(Action release) { this.release = release; }
            public void Dispose() { var current = release; release = null; current?.Invoke(); }
        }
    }
}
