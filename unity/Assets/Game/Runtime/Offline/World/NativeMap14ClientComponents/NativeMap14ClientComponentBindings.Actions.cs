using System;
using System.Collections.Generic;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.StoryPlayables;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Offline.World.NativeMap14ClientComponents
{
    public sealed partial class NativeMap14ClientComponentBindings
    {
        private static EcEntityIdentifier ActionId(object id) =>
            new EcEntityIdentifier((long)Get(id, "ContainerId"), (int)Get(id, "EntityId"));

        // One original receiver registration per client subscription. The
        // original dispatcher retains iteration/order and duplicate semantics.
        private sealed class ActionSubscriptions<T> : IDisposable where T : Delegate
        {
            private readonly List<KeyValuePair<T, IDisposable>> subscriptions = new List<KeyValuePair<T, IDisposable>>();
            public void Add(T receiver, object dispatcher, Type contract, Action<object[]> callback) =>
                subscriptions.Add(new KeyValuePair<T, IDisposable>(receiver,
                    new NativeMap14SharedEventSubscription(dispatcher, contract, callback)));
            public void Remove(T receiver)
            {
                int index = subscriptions.FindIndex(pair => pair.Key.Equals(receiver));
                if (index < 0) return;
                subscriptions[index].Value.Dispose(); subscriptions.RemoveAt(index);
            }
            public void Dispose()
            {
                foreach (var pair in subscriptions) pair.Value.Dispose();
                subscriptions.Clear();
            }
        }

        private sealed class CharacterActionAgent : ActionAgentComponentData, IDisposable
        {
            private readonly NativeMap14SharedAssemblies shared;
            private readonly Dictionary<object, CharacterAction> actions = new Dictionary<object, CharacterAction>();
            private readonly ActionSubscriptions<Action<ActionAgentComponentData, ActionAgentActionData>> starts = new ActionSubscriptions<Action<ActionAgentComponentData, ActionAgentActionData>>();
            private readonly ActionSubscriptions<Action<ActionAgentComponentData, ActionAgentActionData>> ends = new ActionSubscriptions<Action<ActionAgentComponentData, ActionAgentActionData>>();
            public override object Original { get; }
            public override object ComponentInfo => null;
            public CharacterActionAgent(NativeMap14SharedAssemblies shared, object original)
            { this.shared = shared; Original = original; }
            public override bool HasActionType(ActionType type) => (bool)Query("HasActionType", type);
            public override EcEntityIdentifier FindAction(ActionType type) => ActionId(Query("FindAction", type));
            private object Query(string method, ActionType type)
            {
                var contract = shared.CommonType("ActionType");
                return Original.GetType().GetMethod(method, new[] { contract })
                    .Invoke(Original, new[] { Enum.ToObject(contract, (int)type) });
            }
            private CharacterAction Wrap(object original)
            {
                if (actions.TryGetValue(original, out var known)) return known;
                var action = new CharacterAction(shared, original); actions.Add(original, action); return action;
            }
            private void Subscribe(ActionSubscriptions<Action<ActionAgentComponentData, ActionAgentActionData>> list,
                Action<ActionAgentComponentData, ActionAgentActionData> receiver, string field, string contract)
            {
                list.Add(receiver, Get(Original, field), shared.CommonType(contract), args =>
                {
                    if (!ReferenceEquals(args[0], Original)) throw new InvalidOperationException("Original action agent event owner differs.");
                    receiver(this, Wrap(args[1]));
                });
            }
            public override event Action<ActionAgentComponentData, ActionAgentActionData> StartEvent
            {
                add => Subscribe(starts, value, "StartEvent", "IActionAgentStartEventReceiver");
                remove => starts.Remove(value);
            }
            public override event Action<ActionAgentComponentData, ActionAgentActionData> EndEvent
            {
                add => Subscribe(ends, value, "EndEvent", "IActionAgentEndEventReceiver");
                remove => ends.Remove(value);
            }
            public void Dispose()
            {
                starts.Dispose(); ends.Dispose();
                foreach (var action in actions.Values) action.Dispose();
                actions.Clear();
            }
        }

        private sealed class CharacterAction : ActionAgentActionData, IDisposable
        {
            private readonly NativeMap14SharedAssemblies shared;
            private readonly ActionSubscriptions<Action<ActionAgentActionData, object>> ends = new ActionSubscriptions<Action<ActionAgentActionData, object>>();
            public override object Original { get; }
            public override ActionType ActionType => (ActionType)Convert.ToInt32(Get(Original, "ActionType"));
            public override EcEntityIdentifier EntityId => ActionId(Get(Get(Original, "Entity"), "Id"));
            public CharacterAction(NativeMap14SharedAssemblies shared, object original)
            { this.shared = shared; Original = original; }
            public override event Action<ActionAgentActionData, object> EntityEndEvent
            {
                add
                {
                    var entity = Get(Original, "Entity");
                    ends.Add(value, Get(entity, "EndEvent"), shared.CommonType("IEcEntityEndEventReceiver"), args =>
                    {
                        if (!ReferenceEquals(args[0], entity)) throw new InvalidOperationException("Original action entity event owner differs.");
                        value(this, args[1]);
                    });
                }
                remove => ends.Remove(value);
            }
            public void Dispose() => ends.Dispose();
        }
    }
}
