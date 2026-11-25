using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryGroundEffects;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleGridMapper : IPlayableGroundSurface
    {
        private readonly PlanarGridProjection projection;
        private readonly FightBattleGrid grid;

        public OfficialBattleGridMapper(Transform groundRoot, FightBattleGrid grid)
        {
            if (groundRoot == null) throw new ArgumentNullException(nameof(groundRoot));
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            projection = new PlanarGridProjection(groundRoot, Origin(grid));
        }

        public static Transform CreateAnchoredRoot(string name, FightBattleGrid grid, FightGridPoint entry,
            Vector3 entryWorld, Transform parent = null)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (!grid.Contains(entry)) throw new ArgumentOutOfRangeException(nameof(entry));
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var placement = new PlanarGridProjection(root, Origin(grid));
            root.position += entryWorld - placement.GridToWorld(entry.X, entry.Y);
            return root;
        }
        private static Vector3 Origin(FightBattleGrid grid) =>
            new Vector3(grid.OriginX, grid.OriginY, grid.OriginZ);

        public Vector3 GridToWorld(FightGridPoint point)
        {
            if (!grid.Contains(point)) throw new ArgumentOutOfRangeException(nameof(point));
            return projection.GridToWorld(point.X, point.Y);
        }

        public bool TryScreenToWorld(Camera camera, Vector2 screen, out Vector3 world) =>
            projection.TryScreenToWorld(camera, screen, out world);

        public FightGridPoint WorldToGrid(Vector3 world)
        {
            Vector2Int cell = projection.WorldToGrid(world);
            return new FightGridPoint(cell.x, cell.y);
        }

        public GroundType GetGroundType(Vector3 attachmentLocalPosition)
        {
            Vector2Int cell = projection.LocalToGrid(attachmentLocalPosition);
            int value = grid.GroundAt(new FightGridPoint(cell.x, cell.y));
            // Original GridGroundComponentInfo returns Block for an absent
            // cell; GroundAt uses -1 for coordinates outside the public grid.
            return value < 0 ? GroundType.Block : (GroundType)value;
        }

    }
}
