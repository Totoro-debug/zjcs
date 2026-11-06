using System;
using System.Collections.Generic;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Offline.World.MainlinePlayerState
{
    /// <summary>
    /// Owns the original map player's saved-state publication order. The Unity
    /// host supplies entity presentation state and input coordinates; this module
    /// publishes activations and raw region points before asking the original
    /// world to perform its entity/region transition sequence.
    /// </summary>
    public sealed class MainlineSharedWorldStateModule
    {
        private readonly MainlineSharedWorld world;
        private readonly OfflineMainlineSave save;
        private readonly int mapClassId;
        private object player;

        public MainlineSharedWorldStateModule(MainlineSharedWorld world, OfflineMainlineSave save,
            int mapClassId, long playerId,
            Func<int, IReadOnlyDictionary<int, int>> readRegionPoints)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            if (mapClassId <= 0) throw new ArgumentOutOfRangeException(nameof(mapClassId));
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (readRegionPoints == null) throw new ArgumentNullException(nameof(readRegionPoints));
            this.mapClassId = mapClassId;
            player = MainlineSharedPlayerProjection.Create(world, save, playerId, readRegionPoints);
        }

        public object Player => player ?? throw new ObjectDisposedException(nameof(MainlineSharedWorldStateModule));

        public void StartClient(int x, int y)
        {
            EnsurePlayer();
            world.StartClient(player, x, y);
        }

        /// <summary>
        /// Publishes one committed local snapshot. The order is intentionally
        /// fixed: activations, raw region progress, entity state, original
        /// prepare/remove/add/status transitions, then character position.
        /// </summary>
        public void Publish(IReadOnlyDictionary<int, MainlineSharedEntityState> states,
            IReadOnlyDictionary<int, int> rawRegionPoints, Func<int, int> regionStatus,
            int characterX, int characterY)
        {
            EnsurePlayer();
            if (states == null) throw new ArgumentNullException(nameof(states));
            if (rawRegionPoints == null) throw new ArgumentNullException(nameof(rawRegionPoints));
            if (regionStatus == null) throw new ArgumentNullException(nameof(regionStatus));

            MainlineSharedPlayerProjection.SynchronizeActivations(world, player, save);
            MainlineSharedPlayerProjection.SynchronizeRegionProgress(player, mapClassId, rawRegionPoints);
            if (world.IsReady)
                world.SynchronizeClientState(states, regionStatus);
            else
                world.SynchronizeEntityStates(states);
            if (world.IsReady)
                world.SynchronizeClientCharacter(characterX, characterY);
        }

        public void Dispose()
        {
            player = null;
        }

        private void EnsurePlayer()
        {
            if (player == null) throw new ObjectDisposedException(nameof(MainlineSharedWorldStateModule));
            if (!world.IsLoaded) throw new InvalidOperationException("Load the original map before publishing its saved state.");
        }
    }
}
