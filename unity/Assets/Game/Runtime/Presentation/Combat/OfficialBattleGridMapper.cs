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
    }
}
