using System;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleGridMapper
    {
        private readonly Transform groundRoot;
        private readonly FightBattleGrid grid;

        public OfficialBattleGridMapper(Transform groundRoot, FightBattleGrid grid)
        {
            this.groundRoot = groundRoot != null ? groundRoot : throw new ArgumentNullException(nameof(groundRoot));
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public Vector3 GridToWorld(FightGridPoint point)
        {
            if (!grid.Contains(point)) throw new ArgumentOutOfRangeException(nameof(point));
            return groundRoot.TransformPoint(new Vector3(
                grid.OriginX + point.X,
                grid.OriginY + point.Y,
                grid.OriginZ));
        }

        public bool TryScreenToWorld(Camera camera, Vector2 screen, out Vector3 world)
        {
            world = default;
            if (camera == null) return false;
            var plane = new Plane(groundRoot.forward, groundRoot.TransformPoint(new Vector3(0, 0, grid.OriginZ)));
            Ray ray = camera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out float distance)) return false;
            world = ray.GetPoint(distance);
            return true;
        }

        public FightGridPoint WorldToGrid(Vector3 world)
        {
            Vector3 local = groundRoot.InverseTransformPoint(world);
            // GridGroundComponentInfo.PosToGrid -> EntityHandler.DirToGrid: add half a cell,
            // then integer conversion truncates toward zero (including outside-grid pointers).
            return new FightGridPoint((int)(local.x - grid.OriginX + .5f),
                (int)(local.y - grid.OriginY + .5f));
        }
    }
}
