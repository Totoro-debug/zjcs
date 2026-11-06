using System;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
namespace Zjcs.Offline.World.NativeMap14ClientComponents
{
    public sealed class NativeMap14ClientComponentBindings
    {
        private readonly NativeMap14SharedAssemblies shared;
        public NativeMap14ClientComponentBindings(NativeMap14SharedAssemblies shared)
        { this.shared = shared ?? throw new ArgumentNullException(nameof(shared)); }
        public void Bind(NativeMap14SharedViewEntity view)
        {
            Bind(view, "EntitySourceComponent", value => new Source(value));
            Bind(view, "WorldEntityLevelComponent", value => new LevelState(value));
            Bind(view, "LevelComponent", value => new CharacterLevel(value));
            Bind(view, "PropComponent", value => new CharacterProp(value));
            Bind(view, "MapRegionStatusComponent", value => new CharacterRegionStatus(value));
            Bind(view, "MapStatusAgentComponent", value => new CharacterMapStatus(value));
            Bind(view, "CharacterComponent", value => new CharacterState(value));
            Bind(view, "GridMoverComponent", value => new CharacterGridMover(value));
            Bind(view, "StageBehTouchComponent", value => new Touch(value));
            Bind(view, "StageResultAwardExtraPropComponent", value => new ExtraProp(value));
            Bind(view, "StageResultAwardStatusEffectComponent", value => new StatusEffect(value));
            Bind(view, "StageResultModifyHpPercentComponent", value => new HpPercent(value));
            Bind(view, "StageConPlayerPeriodicityInteractionComponent", value => new Periodicity(value));
            Bind(view, "StageResultPeriodicityRandomAwardComponent", value => new RandomAward(value));
        }
        private void Bind<T>(NativeMap14SharedViewEntity view, string name, Func<object, T> create) where T : EntityClientComponentData
        {
            var value = view.Original.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
                .Invoke(view.Original, new object[] { shared.CommonType(name) });
            if (value == null) return;
            // Bind by the original client's consumed data abstraction, rather
            // than by the private platform wrapper implementation type.
            var type = typeof(T).BaseType;
            var existing = typeof(NativeMap14SharedViewEntity).GetMethod("GetComponent").MakeGenericMethod(type).Invoke(view, null);
            if (existing != null)
            {
                if (!ReferenceEquals(((EntityClientComponentData)existing).Original, value))
                    throw new InvalidOperationException("Original component binding identity differs.");
                return;
            }
            typeof(NativeMap14SharedViewEntity).GetMethod("BindAdditionalComponent").MakeGenericMethod(type).Invoke(view, new object[] { create(value) });
        }
        private static object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
        private static object Call(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name).Invoke(owner, arguments);
        private sealed class Source : EntitySourceComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => Get(Original, "_Info");
            public Source(object original) { Original = original; }
            public override bool IsMine(EntityOwnershipType type, long ownerId) => (bool)Call(Original, "IsMine",
                Enum.ToObject(Original.GetType().Assembly.GetType("Common.EntityOwnershipType", true), (int)type), ownerId);
            public override bool CanClick() => (bool)Call(Original, "CanClick");
        }
        private sealed class LevelState : WorldEntityLevelComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => Get(Original, "_Info");
            public LevelState(object original) { Original = original; }
            public override float Level => (float)Get(Original, "Level");
            public override int SubRank => Convert.ToInt32(Get(Original, "SubRank"));
            public override int LevelType => Convert.ToInt32(Get(Original, "LevelType"));
        }
        private sealed class CharacterLevel : LevelComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => null;
            public CharacterLevel(object original) { Original = original; }
        }
        private sealed class CharacterProp : PropComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => null;
            public CharacterProp(object original) { Original = original; }
        }
        private sealed class CharacterRegionStatus : MapRegionStatusComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => null;
            public CharacterRegionStatus(object original) { Original = original; }
        }
        private sealed class CharacterMapStatus : MapStatusAgentComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => null;
            public CharacterMapStatus(object original) { Original = original; }
        }
        private sealed class CharacterState : CharacterComponentData
        {
            private readonly System.Reflection.EventInfo onlineUpdated;
            public override object Original { get; }
            public override object ComponentInfo => null;
            public override bool Online => (bool)Get(Original, "Online");
            public override event Action<bool> OnPlayerOnlineStateUpdate
            {
                add => onlineUpdated.AddEventHandler(Original, value);
                remove => onlineUpdated.RemoveEventHandler(Original, value);
            }
            public CharacterState(object original)
            {
                Original = original;
                onlineUpdated = original.GetType().GetEvent("OnPlayerOnlineStateUpdate")
                    ?? throw new MissingMemberException(original.GetType().FullName, "OnPlayerOnlineStateUpdate");
            }
        }
        private sealed class CharacterGridMover : GridMoverComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => Get(Original, "_Info");
            public CharacterGridMover(object original) { Original = original; }
        }
        private sealed class Touch : StageBehTouchComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => Get(Original, "_Info");
            public Touch(object original) { Original = original; }
        }
        private sealed class ExtraProp : StageResultAwardExtraPropComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => Get(Original, "_Info");
            public ExtraProp(object original) { Original = original; }
            public override object GetAwardExtraPropInfo(int index) => Call(Original, "GetAwardExtraPropInfo", index);
        }
        private sealed class StatusEffect : StageResultAwardStatusEffectComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => Get(Original, "_Info");
            public StatusEffect(object original) { Original = original; }
            public override object GetStatusEffectInfo(int index) => Call(Original, "GetStatusEffectInfo", index);
        }
        private sealed class HpPercent : StageResultModifyHpPercentComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => Get(Original, "_Info");
            public HpPercent(object original) { Original = original; }
        }
        private sealed class Periodicity : StageConPlayerPeriodicityInteractionComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => Get(Original, "_Info");
            public Periodicity(object original) { Original = original; }
            public override int GetPeriodictyInteractionId(int index) => (int)Call(Original, "GetPeriodictyInteractionId", index);
        }
        private sealed class RandomAward : StageResultPeriodicityRandomAwardComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => Get(Original, "_Info");
            public RandomAward(object original) { Original = original; }
        }
    }
}
