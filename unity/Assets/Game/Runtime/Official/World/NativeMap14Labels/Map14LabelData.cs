using System;
using System.Linq;
using UnityEngine;

namespace Zjcs.Official.World.NativeMap14Labels
{
    [Serializable]
    public sealed class Map14LabelData
    {
        [Serializable] public sealed class Text { public string key, value; }
        [Serializable] public sealed class ColorValue { public int Key; public Color Value; }
        [Serializable] public sealed class ColorTheme { public int type; public ColorValue[] colors; }
        public string manifest;
        public int mapClassId, width, height, suppressUnlockRank;
        public float[] origin;
        public int[] regionGrid, maxProgress, subRankToRank;
        public MapRegionInfo[] regions;
        public Text[] texts;
        public ColorTheme[] combatRatingColors;
        // Original GridGroundComponentInfo.PosToGrid -> EntityHandler.DirToGrid casts.
        public int QueryRegion(Vector3 position)
        {
            int x = (int)(position.x - origin[0] + 0.5f);
            int y = (int)(position.y - origin[1] + 0.5f);
            return x < 0 || x >= width || y < 0 || y >= height ? -1 : regionGrid[y * width + x];
        }
        // Original FormulaHandler, BattleFormulaHandler and EnumExt.ToRank.
        public int CalcWorldMapRegionProgress(int map, int region, int progress)
        {
            if (map != 14) return 0;
            int max = region >= 0 && region < maxProgress.Length ? maxProgress[region] : 0;
            if (max == 0) return 0;
            return (int)((double)((float)progress / (float)max) * 10000.0);
        }
        public int CalcRankSuppression(int mySubRank, int otherSubRank)
        {
            if (mySubRank < suppressUnlockRank) return 0;
            return subRankToRank[mySubRank] - subRankToRank[otherSubRank];
        }
        public bool TryGetLevelColor(CombatRateingColorType type, int level, out Color color)
        {
            color = default;
            var theme = combatRatingColors.SingleOrDefault(t => t.type == (int)type);
            var item = theme?.colors.FirstOrDefault(c => c.Key == level);
            if (item == null) { Debug.LogError("Original CombatRatingConfig has no color for " + type + ":" + level); return false; }
            color = item.Value; return true;
        }
    }
}
