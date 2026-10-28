using System;

namespace Zjcs.Official.StoryBubbles
{
    public static class BubbleMath
    {
        // Original Common.MathUtils.Lerp(int,int,float), including decimal banker's rounding.
        public static int Lerp(int start, int end, float t) =>
            (int)(Math.Round((decimal)(end - start) * (decimal)Clamp(t, 0f, 1f)) + (decimal)start);
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
