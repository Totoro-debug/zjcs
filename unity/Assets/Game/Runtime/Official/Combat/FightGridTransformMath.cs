using System;

namespace Zjcs.Combat.Official
{
    /// <summary>Common.GridTransformComponent.CalcLocalToWorldPos/Dir and MyMath.Rotate.
    /// Direction uses the original Common.Direction values: Up=0, Down=1, Left=2, Right=3.
    /// Keep the original float arithmetic, flip-before-rotation and Math.Round conversion.</summary>
    public static class FightGridTransformMath
    {
        // Pos2d.FourDirs: Down/Left/Up/Right -> Common.Direction: Up/Down/Left/Right.
        public static int DirectionFromFacingIndex(int facing) =>
            facing == 0 ? 1 : facing == 1 ? 2 : facing == 2 ? 0 : facing;

        public static FightGridPoint CalcLocalToWorldPos(FightGridPoint local, FightGridPoint origin,
            float realCenterX, float realCenterY, int direction, bool flipX)
        {
            float x = local.X - realCenterX;
            float y = local.Y - realCenterY;
            if (flipX) x = -x;
            float rotatedX = x, rotatedY = y;
            switch (direction)
            {
                case 1: rotatedX = -x; rotatedY = -y; break;
                case 2: rotatedX = -y; rotatedY = x; break;
                case 3: rotatedX = y; rotatedY = -x; break;
                // Up and the original unsupported-direction fallback leave the vector unchanged.
            }
            return origin + new FightGridPoint((int)Math.Round(rotatedX + realCenterX),
                (int)Math.Round(rotatedY + realCenterY));
        }
    }
}
