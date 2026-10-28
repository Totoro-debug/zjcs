using System;
using System.Collections;
using System.Collections.Generic;
using Zjcs.Official.EntityWorldInput;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;

namespace Zjcs.Offline.World.NativeMap14WorldInput
{
    // These are original shared methods/state reads. Player/team/UI/resource
    // services remain explicit host dependencies of the original input class.
    public sealed class NativeMap14WorldInputQueries : IWorldInputEntityState, IWorldInputGeometry
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly object formula;
        public NativeMap14WorldInputQueries(NativeMap14SharedAssemblies shared)
        {
            this.shared = shared ?? throw new ArgumentNullException(nameof(shared));
            formula = Activator.CreateInstance(shared.CommonType("FormulaHandler"));
        }
        private static NativeMap14SharedViewEntity Native(ECViewEntityData entity) =>
            entity as NativeMap14SharedViewEntity ?? throw new ArgumentException("Original shared entity binding is required.");
        public bool IsRemoving(ECViewEntityData entity) => (bool)NativeMap14SharedPrefabs.Get(Native(entity).Original, "Removing");
        public bool HasEnded(ECViewEntityData entity) => (bool)NativeMap14SharedPrefabs.Get(Native(entity).Original, "Ended");
        public GridRect GetPosListGridRect(List<Pos2d> positions)
        {
            IList native = null;
            if (positions != null)
            {
                native = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(shared.CommonType("Pos2d")));
                foreach (var position in positions)
                    native.Add(Activator.CreateInstance(shared.CommonType("Pos2d"), position.X, position.Y));
            }
            var result = formula.GetType().GetMethod("GetPosListGridRect").Invoke(formula, new object[] { native });
            return result == null ? null : new GridRect { MinGrid = Position(NativeMap14SharedPrefabs.Get(result, "MinGrid")),
                MaxGrid = Position(NativeMap14SharedPrefabs.Get(result, "MaxGrid")) };
        }
        private static Pos2d Position(object value) => new Pos2d((int)NativeMap14SharedPrefabs.Get(value, "X"), (int)NativeMap14SharedPrefabs.Get(value, "Y"));
        public void Bind(NativeMap14SharedViewEntity entity)
        {
            var native = entity.Original.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
                .Invoke(entity.Original, new object[] { shared.CommonType("EntityPlayerInteractionComponent") });
            if (native == null) return;
            var previous = entity.GetComponent<EntityPlayerInteractionComponentData>();
            if (previous != null)
            {
                if (!ReferenceEquals(previous.Original, native)) throw new InvalidOperationException("Original player interaction identity differs.");
                return;
            }
            entity.BindAdditionalComponent<EntityPlayerInteractionComponentData>(new Interaction(native));
        }
        private sealed class Interaction : EntityPlayerInteractionComponentData
        {
            public override object Original { get; }
            public override object ComponentInfo => NativeMap14SharedPrefabs.Get(Original, "_Info");
            public Interaction(object original) { Original = original; }
            public override bool IsPlayerInteracting(long playerId) => (bool)Original.GetType()
                .GetMethod("IsPlayerInteracting", new[] { typeof(long) }).Invoke(Original, new object[] { playerId });
        }
    }
}
