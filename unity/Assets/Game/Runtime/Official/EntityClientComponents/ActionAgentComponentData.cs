using System;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.EntityClientComponents
{
    // Borrow the original action state and event dispatch; execute no action rules.
    public abstract class ActionAgentComponentData : EntityClientComponentData
    {
        public abstract bool HasActionType(ActionType type);
        public abstract EcEntityIdentifier FindAction(ActionType type);
        public abstract event Action<ActionAgentComponentData, ActionAgentActionData> StartEvent;
        public abstract event Action<ActionAgentComponentData, ActionAgentActionData> EndEvent;
    }

    public abstract class ActionAgentActionData
    {
        public abstract object Original { get; }
        public abstract ActionType ActionType { get; }
        public abstract EcEntityIdentifier EntityId { get; }
        public abstract event Action<ActionAgentActionData, object> EntityEndEvent;
    }
}
