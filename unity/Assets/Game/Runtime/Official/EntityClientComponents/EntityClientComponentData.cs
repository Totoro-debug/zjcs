using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTipDotViews;
namespace Zjcs.Official.EntityClientComponents
{
    // Typed consumption of existing original component instances and info.
    // Shared getters and queries execute unchanged Console methods.
    public abstract class EntityClientComponentData : IEntityComponentInfoSource
    {
        public abstract object Original { get; }
        public abstract object ComponentInfo { get; }
    }
    public abstract class EntitySourceComponentData : EntityClientComponentData
    {
        public abstract bool IsMine(EntityOwnershipType type, long ownerId);
        public abstract bool CanClick();
    }
    public abstract class WorldEntityLevelComponentData : EntityClientComponentData
    {
        public abstract float Level { get; }
        public abstract int SubRank { get; }
        public abstract int LevelType { get; }
    }
    public abstract class StageBehTouchComponentData : EntityClientComponentData { }
    public abstract class StageResultAwardExtraPropComponentData : EntityClientComponentData
    { public abstract object GetAwardExtraPropInfo(int compIndex); }
    public abstract class StageResultAwardStatusEffectComponentData : EntityClientComponentData
    { public abstract object GetStatusEffectInfo(int compIndex); }
    public abstract class StageResultModifyHpPercentComponentData : EntityClientComponentData { }
    public abstract class StageConPlayerPeriodicityInteractionComponentData : EntityClientComponentData
    { public abstract int GetPeriodictyInteractionId(int index); }
    public abstract class StageResultPeriodicityRandomAwardComponentData : EntityClientComponentData { }
}
