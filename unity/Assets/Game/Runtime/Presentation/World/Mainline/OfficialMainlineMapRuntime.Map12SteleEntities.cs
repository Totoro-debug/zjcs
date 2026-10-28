using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World.NativeMap12Steles;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private NativeMap12SteleWorld map12SteleEntities;

        private bool IsMap12StelePresentation(Map10RuntimeEntity entity) => mapClassId == 12
            && entity.TypeId == 218 && (entity.EntityClassId == 6650 || entity.EntityClassId == 6655);

        private void InitializeMap12SteleEntities(Vector2 origin)
        {
            if (map12SteleEntities != null) throw new InvalidOperationException("Map12 stele entity scope already exists.");
            map12Steles ??= OfficialMap12SteleCatalog.Load();
            map12SteleEntities = mapRoot.gameObject.AddComponent<NativeMap12SteleWorld>();
            // The existing mainline interaction layer still owns popup routing.
            // The dedicated stele window operation is not invoked by this visual factory.
            map12SteleEntities.Initialize(origin, GetMap12StelePeriodicityCount, GetMap12SteleWorldState, _ => null);
            entityView.NativeVisualFactory = (info, owner) => map12SteleEntities.TryCreate(
                info.PlacementId, info.EntityClassId, new Vector2Int(info.Grid[0], info.Grid[1]),
                info.Direction, info.State, owner);
            entityView.NativeGridSetter = (placement, grid) => map12SteleEntities.TrySetGrid(placement,
                new Vector2Int(grid.X, grid.Y));
        }

        private int GetMap12StelePeriodicityCount(int groupId)
        {
            var source = map12Steles.Data.Steles.SingleOrDefault(v => v.PeriodicityGroupId == groupId)
                ?? throw new InvalidDataException("Original stele periodicity group is unbound: " + groupId);
            // The profile transaction is authoritative even when a later map-file
            // write has failed. Cold-load recovery uses the same receipt.
            return localSave.completedPeriodicityGroups.Contains(groupId)
                || progressionSave.Save.World.CompletedEntityTags.Contains(OfficialMap12SteleCatalog.CompletionTag(source.PlacementId))
                ? 1 : 0;
        }

        private int GetMap12SteleWorldState(int placement)
        {
            if (!mapEntities.TryGetValue(placement, out var entity) || !IsMap12StelePresentation(entity))
                throw new InvalidDataException("Original stele entity is absent from the current map.");
            int state = entity.State;
            if (mapLogicTags.TryGetValue(placement, out var tag))
                state = localSave.mapLogicStates.FirstOrDefault(v => v != null && v.Tag == tag.Tag)?.State ?? state;
            return state;
        }
    }
}
