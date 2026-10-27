using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Port of 871_156598 ChangeRangeTool.FillRange/GetInArea/GetClosestGridInArea.
    /// TeamChangePosCenter is supplied by StageInfo (its getter uses width/2 and height/2).
    /// Control, occupancy and server acceptance remain the caller's responsibility.
    /// </summary>
    public sealed class FightBattlePreparationRange
    {
        public FightGridPoint Center { get; }
        public int Width { get; }
        public int Height { get; }
        private readonly List<FightGridPoint> offsets;

        public FightBattlePreparationRange(FightGridPoint center, int width, int height,
            FightGridPoint teamChangePosCenter)
        {
            if (width < 0 || height < 0) throw new ArgumentOutOfRangeException(nameof(width));
            Center = center;
            Width = width;
            Height = height;
            offsets = new List<FightGridPoint>(checked(width * height));
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    offsets.Add(new FightGridPoint(j - teamChangePosCenter.X,
                        i - teamChangePosCenter.Y));
        }

        public static float RangeCenterOffset(int length) => (length % 2 - 1) * 1f / 2f;

        public bool Contains(FightGridPoint grid)
        {
            foreach (FightGridPoint offset in offsets)
                if ((Center + offset).Equals(grid)) return true;
            return false;
        }

        public static bool TryGetCenter(IReadOnlyList<FightBattlePreparationRange> ranges,
            FightGridPoint grid, out FightGridPoint center)
        {
            if (ranges != null)
                foreach (FightBattlePreparationRange range in ranges)
                    if (range.Contains(grid))
                    {
                        center = range.Center;
                        return true;
                    }
            center = new FightGridPoint(0, 0);
            return false;
        }

        public static bool TryGetClosest(IReadOnlyList<FightBattlePreparationRange> ranges,
            FightGridPoint grid, Func<FightGridPoint, bool> condition, out FightGridPoint result)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            bool found = false;
            result = new FightGridPoint(0, 0);
            if (ranges == null) return false;
            foreach (FightBattlePreparationRange range in ranges)
            {
                // The original resets the distance for EACH area. Keep that behavior, including
                // its last matching area preference; do not replace it with a global minimum.
                int distance = int.MaxValue;
                foreach (FightGridPoint offset in range.offsets)
                {
                    FightGridPoint candidate = range.Center + offset;
                    if (!condition(candidate)) continue;
                    int nextDistance = Math.Abs(candidate.X - grid.X) + Math.Abs(candidate.Y - grid.Y);
                    if (nextDistance >= distance) continue;
                    distance = nextDistance;
                    result = candidate;
                    found = true;
                }
            }
            return found;
        }
    }
}
