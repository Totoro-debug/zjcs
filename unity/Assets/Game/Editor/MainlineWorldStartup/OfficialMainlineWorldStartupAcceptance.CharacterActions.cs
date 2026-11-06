using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Offline.World.NativeMap14ClientComponents;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        private static void VerifyCharacterActions(MainlineSharedWorld world)
        {
            object Get(object owner, string field) => NativeMap14SharedPrefabs.Get(owner, field);
            void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
            var binding = new NativeMap14ClientComponentBindings(world.Assemblies);
            using (var cache = new NativeMap14SharedViewCache(world.Assemblies, binding.Bind))
            {
                var view = cache.Wrap(world.ClientCharacter);
                var host = new GameObject("Original action client fixture"); host.SetActive(false);
                var entity = host.AddComponent<MyEntity>();
                var agent = host.AddComponent<MyActionAgent>();
                var startPrefab = new GameObject("Action start request fixture"); startPrefab.transform.SetParent(host.transform, false);
                var endPrefab = new GameObject("Action end condition fixture"); endPrefab.transform.SetParent(host.transform, false);
                agent.ActionTypePlayables = new List<MyActionAgent.ActionTypePlayable>
                {
                    new MyActionAgent.ActionTypePlayable { ActionType = ActionType.Stun, Playable = startPrefab, EndPlayable = endPrefab, StopOnActionEnd = true },
                    new MyActionAgent.ActionTypePlayable { ActionType = ActionType.Frozen, Playable = startPrefab },
                    new MyActionAgent.ActionTypePlayable { ActionType = ActionType.Burn, EndPlayable = endPrefab }
                };
                var trace = new List<string>();
                var requests = new ActionPlayableRequests(host, trace);
                agent.Configure(requests); host.SetActive(true); entity.Bind(null, view, true);
                var original = agent.Component.Original;
                var nativeAgent = world.ClientCharacter.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
                    .Invoke(world.ClientCharacter, new object[] { world.Assemblies.CommonType("ActionAgentComponent") });
                Require(ReferenceEquals(original, nativeAgent) && agent.Component.ComponentInfo == null,
                    "Original action agent binding differs.");
                var start = Get(original, "StartEvent"); var end = Get(original, "EndEvent");
                int starts = ActionReceiverCount(start), ends = ActionReceiverCount(end);
                var actions = new List<Tuple<object, object, object>>();
                ActionAgentActionData lastStart = null, lastEnd = null;
                agent.OnMyStartEvent += (owner, action) => { Require(owner == agent, "Action start owner differs."); lastStart = action; trace.Add("start"); };
                agent.OnMyEndEvent += (owner, action) => { Require(owner == agent, "Action end owner differs."); lastEnd = action; trace.Add("end"); };
                var dictionary = (IDictionary)Get(original, "ActionDatas");
                bool running = false;
                try
                {
                    Require(!agent.HasActionType(ActionType.Stun) && agent.FindAction(ActionType.Stun) == default,
                        "Fresh original action lookup differs.");
                    entity.MyStart(); running = true;
                    Require(ActionReceiverCount(start) == starts + 1 && ActionReceiverCount(end) == ends + 1,
                        "Action agent did not subscribe to original dispatchers.");
                    Tuple<object, object, object> NewAction(ActionType type)
                    {
                        var shared = world.Assemblies;
                        var actionEntity = Activator.CreateInstance(shared.CommonType("EcEntity"));
                        var id = Activator.CreateInstance(shared.CommonType("EcEntityIdentifier"), 990000L + world.Data.MapClassId, actions.Count + 1);
                        actionEntity.GetType().GetField("Id").SetValue(actionEntity, id);
                        var component = Activator.CreateInstance(shared.CommonType("ActionComponent"));
                        component.GetType().GetField("mActionType", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(component, Enum.ToObject(shared.CommonType("ActionType"), (int)type));
                        actionEntity.GetType().GetMethod("AddComponent").Invoke(actionEntity, new[] { component });
                        var data = Activator.CreateInstance(shared.CommonType("ActionRuntimeData"));
                        data.GetType().GetField("Type").SetValue(data, Enum.ToObject(shared.CommonType("ActionType"), (int)type));
                        data.GetType().GetField("EntityId").SetValue(data, id);
                        dictionary.Add(id, data);
                        var result = Tuple.Create(actionEntity, component, id); actions.Add(result); return result;
                    }
                    void Fire(object dispatcher, params object[] arguments)
                    {
                        var signature = Array.ConvertAll(arguments, item => item?.GetType() ?? world.Assemblies.CommonType("MySerializable"));
                        dispatcher.GetType().GetMethod("FireEvent", signature).Invoke(dispatcher, arguments);
                    }
                    var stun = NewAction(ActionType.Stun);
                    Require(agent.HasActionType(ActionType.Stun) && agent.FindAction(ActionType.Stun) == new EcEntityIdentifier(990000L + world.Data.MapClassId, 1),
                        "Action queries did not use the original runtime dictionary.");
                    Fire(start, original, stun.Item2);
                    Require(trace.Count == 2 && trace[0] == "start" && trace[1] == "play" && requests.Count == 1
                        && ReferenceEquals(lastStart.Original, stun.Item2), "Original action start order or identity differs.");
                    var first = requests.Last;
                    Require(ActionReceiverCount(Get(stun.Item1, "EndEvent")) == 1, "Tracked action did not subscribe to its original entity.");
                    Fire(end, original, stun.Item2);
                    Require(ReferenceEquals(lastStart, lastEnd) && requests.Count == 2 && requests.Prefab == startPrefab
                        && trace[2] == "play" && trace[3] == "end" && first.Stops == 0,
                        "Original EndPlayable condition, Playable request or end ordering changed.");
                    Fire(Get(stun.Item1, "EndEvent"), stun.Item1, null);
                    Require(first.Stops == 1 && !first.Immediate && ActionReceiverCount(Get(stun.Item1, "EndEvent")) == 0,
                        "Original action entity end did not stop/unsubscribe exactly once.");
                    var frozen = NewAction(ActionType.Frozen); int count = requests.Count;
                    Fire(start, original, frozen.Item2); var untracked = requests.Last;
                    Fire(end, original, frozen.Item2); Fire(Get(frozen.Item1, "EndEvent"), frozen.Item1, null);
                    Require(requests.Count == count + 1 && untracked.Stops == 0
                        && ActionReceiverCount(Get(frozen.Item1, "EndEvent")) == 0, "Untracked original action changed stop behaviour.");
                    var burn = NewAction(ActionType.Burn); count = requests.Count;
                    Fire(start, original, burn.Item2);
                    Require(requests.Count == count, "Null start prefab unexpectedly played.");
                    Fire(end, original, burn.Item2);
                    Require(requests.Count == count + 1 && requests.Prefab == null,
                        "Original end branch substituted EndPlayable for the null Playable.");
                    var move = NewAction(ActionType.Move); count = requests.Count;
                    Fire(start, original, move.Item2); Fire(end, original, move.Item2);
                    Require(requests.Count == count, "Unconfigured original action requested a playable.");
                    string receiverOrder = "";
                    Action<ActionAgentComponentData, ActionAgentActionData> duplicate = (owner, action) => receiverOrder += "a";
                    Action<ActionAgentComponentData, ActionAgentActionData> middle = (owner, action) => receiverOrder += "b";
                    agent.Component.StartEvent += duplicate; agent.Component.StartEvent += middle; agent.Component.StartEvent += duplicate;
                    agent.Component.StartEvent -= duplicate;
                    Fire(start, original, move.Item2);
                    Require(receiverOrder == "ba", "Original receiver first-occurrence removal or registration order changed.");
                    agent.Component.StartEvent -= duplicate; agent.Component.StartEvent -= middle;
                    var pending = NewAction(ActionType.Stun); Fire(start, original, pending.Item2); var stoppedByRoot = requests.Last;
                    entity.End(null); running = false;
                    Require(stoppedByRoot.Stops == 1 && !stoppedByRoot.Immediate && ActionReceiverCount(start) == starts && ActionReceiverCount(end) == ends,
                        "Original action agent end did not stop tracked playables or release its event receivers.");
                    Fire(Get(pending.Item1, "EndEvent"), pending.Item1, null);
                    Require(stoppedByRoot.Stops == 1, "Cleared original tracked playable stopped again.");
                    entity.Remove(null); entity.OnGameObjectPoolReset(); entity.Bind(null, view, false); entity.MyStart(); running = true;
                    Require(ReferenceEquals(agent.Component, view.GetComponent<ActionAgentComponentData>())
                        && ActionReceiverCount(start) == starts + 1, "Original action agent rebind changed adapter identity.");
                    Debug.Log($"MAINLINE_CHARACTER_ACTIONS_PASS map={world.Data.MapClassId} original_events=True start_end_stop=True original_end_prefab_branch=True rebind=True");
                }
                finally
                {
                    if (running) entity.End(null);
                    foreach (var action in actions) dictionary.Remove(action.Item3);
                    cache.Dispose();
                    foreach (var action in actions)
                        Require(ActionReceiverCount(Get(action.Item1, "EndEvent")) == 0, "Disposed action adapter retained a receiver.");
                    UnityEngine.Object.Destroy(host);
                }
            }
        }

        private static int ActionReceiverCount(object dispatcher)
        {
            for (var type = dispatcher.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField("mRecordEventReceivers", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null) return ((ICollection)field.GetValue(dispatcher)).Count;
            }
            throw new InvalidDataException("Original action dispatcher storage differs.");
        }

        public sealed class ActionStopProbe : MyPlayable
        {
            public int Stops; public bool Immediate;
            public override void Stop(bool immediate = false) { Stops++; Immediate = immediate; }
        }

        private sealed class ActionPlayableRequests : IMyPlayableManager
        {
            private readonly GameObject host;
            private readonly List<string> trace;
            public int Count; public GameObject Prefab; public ActionStopProbe Last;
            public ActionPlayableRequests(GameObject host, List<string> trace) { this.host = host; this.trace = trace; }
            public Transform PlayableSlot => throw new NotSupportedException();
            public MyPlayable Play(GameObject prefab, GameObject source, GameObject target, float speed = 1f,
                GameObject parentGo = null, Dictionary<string, object> param = null, IMyPlayableNeedStop needStopObj = null)
            {
                if (source != host || target != host || speed != 1f || parentGo != null || param != null || needStopObj != null)
                    throw new InvalidDataException("Original action playable request changed.");
                Count++; Prefab = prefab; trace.Add("play");
                var probe = new GameObject("Action playable stop fixture"); probe.transform.SetParent(host.transform, false);
                return Last = probe.AddComponent<ActionStopProbe>();
            }
            public void RemoveAllPlayable(bool release) => throw new NotSupportedException();
            public void SetObjectPool(GamePrefabTypeSetPools<MyPlayable, GameObjectPoolBase<MyPlayable>> pools) => throw new NotSupportedException();
            public void ForceReleaseObjectPool() => throw new NotSupportedException();
        }
    }
}
