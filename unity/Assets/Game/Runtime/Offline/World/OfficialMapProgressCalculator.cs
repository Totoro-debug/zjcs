using System.Collections.Generic;
using Zjcs.Official.World;

namespace Zjcs.Offline.World
{
    public static class OfficialMapProgressCalculator
    {
        public static int CalculateRegionProgress(OfficialMapDefinition map, int region, int progress)
        {
            if (map == null || map.ClassId <= 0 || map.MaxRegionProgress == null)
                return 0;

            int maximum = 0;
            foreach (OfficialMapRegionProgress entry in map.MaxRegionProgress)
            {
                if (entry != null && entry.Region == region)
                {
                    maximum = entry.Value;
                    break;
                }
            }
            if (maximum == 0)
                return 0;

            return FloatToFixed((float)progress / maximum);
        }

        public static int CalculateMapProgress(OfficialMapDefinition map, IReadOnlyList<int> regionProgresses, bool allRegionsActivated)
        {
            if (map == null || map.ClassId <= 0 || map.MaxRegionProgress == null || regionProgresses == null || regionProgresses.Count == 0)
                return 0;

            int progress = 0;
            for (int i = 0; i < regionProgresses.Count; i++)
                progress += regionProgresses[i];

            int mapMaxProgress = 100;
            foreach (OfficialMapRegionProgress entry in map.MaxRegionProgress)
                if (entry != null)
                    mapMaxProgress += entry.Value;
            if (mapMaxProgress <= 0)
                return 0;

            if (allRegionsActivated)
                progress += 100;

            float ratio = (float)progress / mapMaxProgress;
            if (progress > 0 && ratio <= 0.01f)
                return 100;
            return FloatToFixed(ratio);
        }

        private static int FloatToFixed(float value)
        {
            return (int)((double)value * 10000.0);
        }
    }
}
