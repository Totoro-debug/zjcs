using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.World
{
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
        public MapGridPoint PlayerGrid { get; }
        public IReadOnlyDictionary<int, int> RegionProgress { get; }
        public IReadOnlyList<int> PublicRegions { get; }
        public IReadOnlyList<int> ActivatedWitchPlacements { get; }
        public IReadOnlyList<int> ActivePlacements { get; }
        public byte[] GridVisibility { get; }
        public IReadOnlyList<int> ExploredEntityPlacements { get; }

        public MainlineMapStateSnapshot(int mapClassId, MapGridPoint playerGrid,
            IReadOnlyDictionary<int, int> regionProgress, IReadOnlyList<int> publicRegions,
            IReadOnlyList<int> activatedWitchPlacements, IReadOnlyList<int> activePlacements,
            byte[] gridVisibility, IReadOnlyList<int> exploredEntityPlacements)
        {
            MapClassId = mapClassId;
            PlayerGrid = playerGrid;
            if (regionProgress == null) throw new ArgumentNullException(nameof(regionProgress));
            if (publicRegions == null) throw new ArgumentNullException(nameof(publicRegions));
            if (activatedWitchPlacements == null) throw new ArgumentNullException(nameof(activatedWitchPlacements));
            if (activePlacements == null) throw new ArgumentNullException(nameof(activePlacements));
            RegionProgress = new ReadOnlyDictionary<int, int>(new Dictionary<int, int>(regionProgress));
            PublicRegions = new List<int>(publicRegions).AsReadOnly();
            ActivatedWitchPlacements = new List<int>(activatedWitchPlacements).AsReadOnly();
            ActivePlacements = new List<int>(activePlacements).AsReadOnly();
            GridVisibility = gridVisibility == null ? Array.Empty<byte>() : (byte[])gridVisibility.Clone();
            ExploredEntityPlacements = exploredEntityPlacements ?? throw new ArgumentNullException(nameof(exploredEntityPlacements));
        }
    }
}
