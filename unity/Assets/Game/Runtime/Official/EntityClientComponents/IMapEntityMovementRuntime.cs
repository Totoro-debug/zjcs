using System;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Official.EntityClientComponents
{
    // Consumed original TimeManager/MapMoveUtils calls. The local shared world
    // owns their configuration, clock and calculations.
    public interface IMapEntityMovementRuntime
    {
        long GetTick();
        DateTime GetDateTime();
        double CalcElapsedSecond(long tick);
        float SClientDistanceThreshold { get; }
        bool IsWithinThresholdSqrDistance(Vector2 pos1, Vector2 pos2);
    }

    // The original movement agent consumes only this action-provider query.
    public interface IActionMovementProvider
    {
        Pos2d GetTargetPos();
    }
}
