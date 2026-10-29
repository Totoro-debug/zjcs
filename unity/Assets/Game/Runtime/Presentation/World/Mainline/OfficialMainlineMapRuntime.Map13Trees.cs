using System;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13TreeEntities;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private NativeMap13TreeWorld map13Trees;

        private void InitializeMap13TreeEntities(Vector2 origin)
        {
            if (map13Trees) throw new InvalidOperationException("Map13 original tree world already exists.");
            map13Trees = mapRoot.gameObject.AddComponent<NativeMap13TreeWorld>();
            map13Trees.Initialize(session.Definition, origin,
                id => mapEntities.TryGetValue(id, out var entity) ? entity.State : 0,
                id => mapEntities.TryGetValue(id, out var entity) && entity.Active
                    && !localSave.completedEntityPlacements.Contains(id) && !IsMapLogicEntityDestroyed(id)
                    && map13GridVisibility != null && map13GridVisibility.IsVisible(new Pos2d(entity.Grid.X, entity.Grid.Y)), 1);
            entityView.NativeVisualFactory = (info, owner) => map13Trees.TryCreate(info.PlacementId, info.EntityClassId,
                new Vector2Int(info.Grid[0], info.Grid[1]), info.State, owner);
            entityView.NativeGridSetter = (id, grid) => map13Trees.TrySetGrid(id, new Vector2Int(grid.X, grid.Y));
        }
    }
}
