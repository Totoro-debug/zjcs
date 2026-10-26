using System;
using Zjcs.Official.World;

namespace Zjcs.Offline.World
{
    [Serializable] public sealed class Map13WaterDefinition
    {
        public string Manifest;
        public int MapClassId;
        public Map13WaterRule[] Rules;
        public Map13WaterTerminal[] Terminals;
    }

    [Serializable] public sealed class Map13WaterRule
    {
        public int PlacementId, EntityClassId, InitialState;
        public MapGridPoint OriginalGrid;
        public bool InitiallySpawned, HasGridMover;
        public Map13WaterBody[] Body;
        public MapGridPoint[] InteractiveRange;
        public OfficialWorldStageRule Stage;
        public Map13WaterStateValue[] SelfStates;
        public Map13WaterGroundOperation[] GroundOperations;
        public Map13WaterGroundData[] GroundData;
        public int[] UncoveredEntityClasses, UncoveredEntities, IgnoreCoveredEntityClasses, TriggerEntities;
        public Map13WaterStateUpdates[] StateUpdates;
        public Map13WaterRoute[] Routes;
    }

    [Serializable] public sealed class Map13WaterBody
    {
        public MapGridPoint Pos;
        public int GroundType;
        public bool IgnoreClick, OverrideGroundType;
    }

    [Serializable] public sealed class Map13WaterGroundOperation
    {
        public int DataIndex;
        public bool IsRevertGroundType;
    }

    [Serializable] public sealed class Map13WaterGroundData { public Map13WaterBody[] BodyRangeOverrideInfos; }
    [Serializable] public sealed class Map13WaterStateValue { public int State; }
    [Serializable] public sealed class Map13WaterStateUpdate
    {
        public int LoEntId, State;
        public bool UseLoEntDefaultState;
    }
    [Serializable] public sealed class Map13WaterStateUpdates { public Map13WaterStateUpdate[] StageDatas; }
    [Serializable] public sealed class Map13WaterRoute { public MapGridPoint[] OffsetPath; }
    [Serializable] public sealed class Map13WaterTerminal
    {
        public int PlacementId, ResetTransferActionId;
        public int[] ExtendRoadLoEntityIds, NeedResetLoEntityIds;
    }

    [Serializable] public sealed class Map13WaterState
    {
        public int PlacementId, State, NextSegment, GroundOperation = -1;
        public MapGridPoint Grid;
        public bool Spawned, Returning;
    }

    public sealed class Map13WaterOccupant
    {
        public int PlacementId, EntityClassId;
        public MapGridPoint Grid;
        public bool HasGridMover;
    }

    public sealed class Map13WaterMovement
    {
        public int PlacementId;
        public MapGridPoint[] Path;
    }
}
