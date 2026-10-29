using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.EntityActions
{
    // Consumed original ActionComponent fields; this adapter executes no action.
    public abstract class ActionComponentData:IEntityComponentInfoSource
    {
        public abstract EcEntityIdentifier ApplyId { get; }
        public abstract object ComponentInfo { get; }
    }
}
