using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryGroundEffects;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleGridMapper : IPlayableGroundSurface
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
            return LocalToGrid(local);
        }

        public GroundType GetGroundType(Vector3 attachmentLocalPosition)
        {
            int value = grid.GroundAt(LocalToGrid(attachmentLocalPosition));
            // Original GridGroundComponentInfo returns Block for an absent
            // cell; GroundAt uses -1 for coordinates outside the public grid.
            return value < 0 ? GroundType.Block : (GroundType)value;
        }

        private FightGridPoint LocalToGrid(Vector3 local)
        {
            // GridGroundComponentInfo.PosToGrid -> EntityHandler.DirToGrid: add half a cell,
            // then integer conversion truncates toward zero (including outside-grid pointers).
            return new FightGridPoint((int)(local.x - grid.OriginX + .5f),
                (int)(local.y - grid.OriginY + .5f));
        }
    }
}
