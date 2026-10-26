using System;
using UnityEngine;

namespace Zjcs.Official.World.NativeMap13Labels
{
    [Serializable]
    public sealed class Map13LabelData
    {
        public string manifest;
        public int mapClassId, width, height;
        public SubRank suppressUnlockRank;
        public float[] origin;
        public int[] regionGrid, maxProgress;
        public MapRegionInfo[] regions;

        // Original GridGroundComponentInfo.PosToGrid -> EntityHandler.DirToGrid.
        public int QueryRegion(Vector3 position)
        {
            int x = (int)(position.x - origin[0] + 0.5f);
            int y = (int)(position.y - origin[1] + 0.5f);
            return x < 0 || y < 0 || x >= width || y >= height ? -1 : regionGrid[y * width + x];
        }
        // Original FormulaHandler uses float division then double * 10000 cast.
        public int CalcWorldMapRegionProgress(int mapClassId, int region, int progress)
        {
            int max = mapClassId == this.mapClassId && region >= 0 && region < maxProgress.Length ? maxProgress[region] : 0;
            if (max == 0) return 0;
            return (int)((double)((float)progress / (float)max) * 10000.0);
        }
        public int CalcRankSuppression(SubRank mySubRank, SubRank otherSubRank)
        {
            if (mySubRank < suppressUnlockRank) return 0;
            return (int)Map13LabelFormula.ToRank(mySubRank) - (int)Map13LabelFormula.ToRank(otherSubRank);
        }
    }
}
