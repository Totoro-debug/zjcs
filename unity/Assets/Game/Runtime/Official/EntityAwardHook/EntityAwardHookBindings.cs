using System;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.EntityAwardHook
{
    public abstract class EntityAwardMessageData
    {
        public abstract object Original { get; }
        public abstract object SourceData { get; }
    }
    public abstract class EntityMapAwardSourceData
    {
        public abstract object Original { get; }
        public abstract EcEntityIdentifier EntityId { get; }
    }
    public interface IEntityAwardManager
    {
        void RegisterHandler(Func<EntityAwardMessageData, bool> handler, EntityAwardHandlerPriority priority);
        void RemoveHandler(Func<EntityAwardMessageData, bool> handler);
        void ShowAward(EntityAwardMessageData message);
    }
}
