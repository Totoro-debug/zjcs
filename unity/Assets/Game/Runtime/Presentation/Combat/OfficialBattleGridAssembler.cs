using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleGridBuild
    {
        public FightBattleGrid Grid { get; }
        public int MoveMask { get; }

        internal OfficialBattleGridBuild(FightBattleGrid grid, int moveMask)
        {
            Grid = grid;
            MoveMask = moveMask;
        }
    }

    /// <summary>Builds the shared battle grid from a mode's already-adapted cell data.</summary>
    public static class OfficialBattleGridAssembler
    {
        public static OfficialBattleGridBuild Build(int width, int height,
            float originX, float originY, float originZ,
            IEnumerable<FightBattleGridCell> cells, string sourceName)
        {
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("Official " + sourceName + " battle grid has invalid dimensions.");
            if (cells == null) throw new InvalidOperationException("Official " + sourceName + " battle grid has no cells.");

            var grid = new FightBattleGrid
            {
                Width = width, Height = height,
                OriginX = originX, OriginY = originY, OriginZ = originZ,
                DefaultGroundType = 2
            };
            int moveMask = 0;
            foreach (FightBattleGridCell cell in cells)
            {
                if (cell == null) continue;
                grid.Cells.Add(cell);
                if (cell.Height >= 999) grid.BlockedPositions.Add(new FightGridPoint(cell.X, cell.Y));
                else if (cell.GroundType >= 0 && cell.GroundType < 31) moveMask |= 1 << cell.GroundType;
            }
            if (moveMask == 0)
                throw new InvalidOperationException("Official " + sourceName + " battle grid has no walkable cells.");
            return new OfficialBattleGridBuild(grid, moveMask);
        }
    }
}
