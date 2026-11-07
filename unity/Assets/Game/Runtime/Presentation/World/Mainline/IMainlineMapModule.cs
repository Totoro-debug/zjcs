using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.World
{
    [Flags]
    public enum MainlineMapFeatures
    {
        None = 0,
        MapModel = 1 << 0,
        Hud = 1 << 1,
        Regions = 1 << 2,
        Statues = 1 << 3,
        GridFog = 1 << 4,
        Exploration = 1 << 5,
        MiniMap = 1 << 6,
        ResourceStamina = 1 << 7,
        CrossMapState = 1 << 8
    }

    public enum MainlineStatueKind
    {
        Goddess,
        LostGoddess,
        Hero,
        Witch
    }

    public sealed class MainlineStatueState
    {
        public int PlacementId { get; }
        public int Region { get; }
        public MainlineStatueKind Kind { get; }
        public bool Active { get; }
        public bool Activated { get; }
        public bool DailyVisitAvailable { get; }

        public MainlineStatueState(int placementId, int region, MainlineStatueKind kind,
            bool active, bool activated, bool dailyVisitAvailable)
        {
            PlacementId = placementId;
            Region = region;
            Kind = kind;
            Active = active;
            Activated = activated;
            DailyVisitAvailable = dailyVisitAvailable;
        }
    }

    public sealed class MainlineResourceState
    {
        public int PlacementId { get; }
        public int EntityClassId { get; }
        public long AvailableAt { get; }
        public int Generation { get; }
        public bool BattlePending { get; }

        public MainlineResourceState(int placementId, int entityClassId, long availableAt,
            int generation, bool battlePending)
        {
            PlacementId = placementId;
            EntityClassId = entityClassId;
            AvailableAt = availableAt;
            Generation = generation;
            BattlePending = battlePending;
        }
    }

    /// <summary>
    /// The seam used by every interactive mainline map.  The implementation
    /// owns the official map model, HUD, region state, statues, fog,
    /// exploration, minimap and stamina modules; callers only coordinate the
    /// map lifecycle and consume a read-only snapshot.
    /// </summary>
    public interface IMainlineMapModule : IDisposable
    {
        int MapClassId { get; }
        OfficialMainlineMapProfile Profile { get; }
        bool IsOpen { get; }

        void Begin(int mapClassId, string slotId, string savesRoot, Transform uiParent,
            GameObject characterModel, string displayName, Action onClose);

        /// <summary>Requests a path to a logical grid cell through the official pathfinder.</summary>
        bool TryMove(MapGridPoint target);

        /// <summary>Requests movement and interaction for an official placement.</summary>
        bool TryInteract(int placementId);

        MainlineMapStateSnapshot ReadState();
    }

    /// <summary>
    /// Stable state projection for HUD, minimap and tests. Arrays and maps are
    /// copied by the producer, so reading a snapshot never mutates a session.
    /// </summary>
    public sealed class MainlineMapStateSnapshot
    {
        public int MapClassId { get; }
        public int MapProgress { get; }
        public MapGridPoint PlayerGrid { get; }
        public IReadOnlyDictionary<int, int> RegionProgress { get; }
        public IReadOnlyList<int> PublicRegions { get; }
        public IReadOnlyList<int> ActivatedWitchPlacements { get; }
        public IReadOnlyList<int> ActivePlacements { get; }
        public byte[] GridVisibility { get; }
        public IReadOnlyList<int> ExploredEntityPlacements { get; }
        public IReadOnlyList<MainlineStatueState> Statues { get; }
        public IReadOnlyList<MainlineResourceState> ResourcePoints { get; }
        public int ExplorationTotalCount { get; }
        public int ExplorationDailyCount { get; }

        public MainlineMapStateSnapshot(int mapClassId, MapGridPoint playerGrid,
            IReadOnlyDictionary<int, int> regionProgress, IReadOnlyList<int> publicRegions,
            IReadOnlyList<int> activatedWitchPlacements, IReadOnlyList<int> activePlacements,
            byte[] gridVisibility, IReadOnlyList<int> exploredEntityPlacements, int mapProgress,
            IReadOnlyList<MainlineStatueState> statues, IReadOnlyList<MainlineResourceState> resourcePoints,
            int explorationTotalCount, int explorationDailyCount)
        {
            MapClassId = mapClassId;
            PlayerGrid = playerGrid;
            MapProgress = mapProgress;
            if (regionProgress == null) throw new ArgumentNullException(nameof(regionProgress));
            if (publicRegions == null) throw new ArgumentNullException(nameof(publicRegions));
            if (activatedWitchPlacements == null) throw new ArgumentNullException(nameof(activatedWitchPlacements));
            if (activePlacements == null) throw new ArgumentNullException(nameof(activePlacements));
            RegionProgress = new ReadOnlyDictionary<int, int>(new Dictionary<int, int>(regionProgress));
            PublicRegions = new List<int>(publicRegions).AsReadOnly();
            ActivatedWitchPlacements = new List<int>(activatedWitchPlacements).AsReadOnly();
            ActivePlacements = new List<int>(activePlacements).AsReadOnly();
            GridVisibility = gridVisibility == null ? Array.Empty<byte>() : (byte[])gridVisibility.Clone();
            if (exploredEntityPlacements == null) throw new ArgumentNullException(nameof(exploredEntityPlacements));
            ExploredEntityPlacements = new List<int>(exploredEntityPlacements).AsReadOnly();
            if (statues == null) throw new ArgumentNullException(nameof(statues));
            if (resourcePoints == null) throw new ArgumentNullException(nameof(resourcePoints));
            Statues = new List<MainlineStatueState>(statues).AsReadOnly();
            ResourcePoints = new List<MainlineResourceState>(resourcePoints).AsReadOnly();
            ExplorationTotalCount = explorationTotalCount;
            ExplorationDailyCount = explorationDailyCount;
        }
    }
}
