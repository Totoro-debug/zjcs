using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleStatusPlayableAcceptance
    {
        private sealed partial class Scope
        {
            private readonly BurnDeathEvents burnDeaths = new BurnDeathEvents();

            // A fixture event producer, not a replacement for production death settlement.
            private sealed class BurnDeathEvents : IBattleSourceDeathView, IBattleSourceDeathEvents
            {
                private readonly Dictionary<GameObject, List<Action>> listeners = new Dictionary<GameObject, List<Action>>();
                public int Count => listeners.Values.Sum(value => value.Count);
                public IDisposable SubscribeSourceDeath(GameObject source, Action onDie)
                {
                    if (!listeners.TryGetValue(source, out var list)) listeners.Add(source, list = new List<Action>());
                    list.Add(onDie);
                    return new Release(() => list.Remove(onDie));
                }
                public void Die(GameObject source)
                {
                    if (listeners.TryGetValue(source, out var list))
                        foreach (var callback in list.ToArray()) callback();
                }
                public void ForcePlayDiePlayable(GameObject source) => throw new InvalidOperationException("Unexpected force-death leaf.");
                private sealed class Release : IDisposable
                {
                    private Action action;
                    internal Release(Action action) { this.action = action; }
                    public void Dispose() { var release = action; action = null; release?.Invoke(); }
                }
            }

            private sealed class BurnSound : IProfessionSoundHost
            {
                public readonly List<uint> Events = new List<uint>();
                public uint PostEvent(uint eventId, GameObject emitter)
                {
                    Require(eventId != 0 && emitter != null, "Original fire sound lost its event or emitter.");
                    Events.Add(eventId); return (uint)Events.Count;
                }
            }

            private IEnumerator BurnSteps()
            {
                var previous = OfficialWwiseEvent.Host;
                var sounds = new BurnSound();
                OfficialWwiseEvent.Host = sounds;
                try
                {
                    var data = Resources.Load<TextAsset>("Combat/status-burns");
                    Require(data != null, "Original persistent fire catalog is missing.");
                    var entries = JsonUtility.FromJson<OrbitCatalog>(data.text).graphs;
                    Require(entries.Length == 2, "Persistent fire graph count differs.");
                    var agent = sourceA.GetComponent<MyFightStatusAgent>();
                    foreach (var entry in entries)
                    {
                        var prefab = Resources.Load<GameObject>(entry.resource);
                        Require(prefab != null, "Original persistent fire graph is missing: " + entry.id);
                        var go = new GameObject("Original persistent status " + entry.id);
                        go.transform.SetParent(root.transform, false);
                        var state = new State(1);
                        var view = go.AddComponent<MyFightStatus>();
                        view.myPlayablePrefab = prefab;
                        view.Bind(new Context { State = state, Owner = sourceA, Creator = sourceB, Agent = agent }, manager);
                        int before = sounds.Events.Count;
                        view.OnStart(true);
                        for (int frame = 0; frame < 30; frame++) { yield return null; Flush(); }
                        var graph = BurnRootGraph(view);
                        Require(graph != null && graph.gameObject.activeInHierarchy && graph.GetDuration() < 0,
                            "Original fire duration/source binding differs.");
                        // Only playable13052 contains PlaySound in the original prefab.
                        Require(sounds.Events.Count == before + (entry.id == 13052 ? 1 : 0) && burnDeaths.Count == 1,
                            "Original fire sound or source-death subscription did not execute.");
                        Capture("burn-" + entry.id + "-running");
                        burnDeaths.Die(sourceB); yield return null; Flush();
                        Require(graph.gameObject.activeInHierarchy, "Creator death incorrectly acted as owner death.");
                        burnDeaths.Die(sourceA);
                        for (int frame = 0; frame < 10; frame++) { yield return null; Flush(); }
                        Require(!graph.gameObject.activeInHierarchy && burnDeaths.Count == 0,
                            "Owner death left original fire or its subscription active.");
                        view.OnEnd();
                        view.OnStart(false);
                        for (int frame = 0; frame < 30; frame++) { yield return null; Flush(); }
                        Require(BurnRootGraph(view).gameObject.activeInHierarchy && burnDeaths.Count == 1,
                            "Restored status failed to restart the original fire graph.");
                        view.OnEnd();
                        for (int frame = 0; frame < 10; frame++) { yield return null; Flush(); }
                        Require(burnDeaths.Count == 0 && !RootGraphActive(view), "Status end leaked original fire.");
                        Capture("burn-" + entry.id + "-ended", false);
                        UnityEngine.Object.Destroy(go);
                        manager.RemoveAllPlayable(true); center.RemoveAll();
                    }
                    Debug.Log("Original persistent fire graphs passed: native raster, original sound event dispatch, owner/creator separation, source-death unsubscribe, status end and restored status restart. Sound recording and death events are fixture adapters; production grid binding is separate.");
                }
                finally { OfficialWwiseEvent.Host = previous; }
            }

            private static bool RootGraphActive(MyFightStatus view)
            {
                var value = BurnRootGraph(view);
                return value != null && value.gameObject.activeInHierarchy;
            }

            private static MyPlayable BurnRootGraph(MyFightStatus view) =>
                (MyPlayable)typeof(MyFightStatus).GetField("mPlayable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(view);
        }
    }
}
