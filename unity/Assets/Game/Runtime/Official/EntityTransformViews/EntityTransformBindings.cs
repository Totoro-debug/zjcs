using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.EntityTransformViews
{
    // Consumed shared state/event/query surfaces; concrete shared grid rules
    // and event publication must be supplied by the original local entity.
    public abstract class ECGridTransformInfo
    {
        public abstract Vector2 RealCenter { get; }
        public abstract List<Pos2d> GetBodyRange();
    }
    public abstract class ECGridTransformData
    {
        public abstract Pos2d Pos { get; }
        public abstract Direction Direction { get; }
        public abstract bool FlipX { get; }
        public abstract ECGridTransformInfo Info { get; }
        public abstract Vector3 GridToPosition(Pos2d grid);
        public abstract Pos2d PositionToGrid(Vector3 pos);
        public abstract void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver);
        public abstract void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver);
    }
    public abstract class ECTransformData
    {
        public abstract Vector3 Position { get; }
        public abstract Quaternion Rotation { get; }
        public abstract Vector3 Scale { get; }
    }
    public abstract class ECViewMoveAgent : MonoBehaviour
    {
        public abstract bool IsMoving { get; }
        public abstract void Stop();
    }
}
