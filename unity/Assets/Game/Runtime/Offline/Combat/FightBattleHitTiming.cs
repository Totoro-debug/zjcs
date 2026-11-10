using System;

namespace Zjcs.Combat.Official
{
    /// <summary>Official CalcSkillHitDelay/CalcSkillStartPos, in the battle grid's unit coordinates.</summary>
    public static class FightBattleHitTiming
    {
        public static float EffectDelay(FightBattleHitDefinition hit,
            FightBattleSkillCastPresentation cast, FightGridPoint center)
        {
            if (hit == null || cast == null) throw new ArgumentNullException();
            var trajectory = hit.Trajectory;
            float sx = cast.SourceX, sy = cast.SourceY, sz = 0f;
            float cx = sx, cy = sy, cz = sz;
            if (trajectory != null)
            {
                float flip = cast.FlipX ? -1f : 1f;
                sx += flip * trajectory.OffsetX;
                sy += trajectory.OffsetY;
                sz += trajectory.OffsetZ;
                if (trajectory.IsUseCenterRotation)
                {
                    cx += flip * trajectory.RotateCenterOffsetX;
                    cy += trajectory.RotateCenterOffsetY;
                    cz += trajectory.RotateCenterOffsetZ;
                    float tx = center.X + flip * trajectory.TargetOffsetX - cx;
                    float ty = center.Y + trajectory.TargetOffsetY - cy;
                    float tz = trajectory.TargetOffsetZ - cz;
                    float radius = Length(sx - cx, sy - cy, sz - cz);
                    float magnitude = Length(tx, ty, tz);
                    sx = (magnitude > 1e-5f ? tx / magnitude : 0f) * radius + cx;
                    sy = (magnitude > 1e-5f ? ty / magnitude : 0f) * radius + cy;
                    sz = (magnitude > 1e-5f ? tz / magnitude : 0f) * radius + cz;
                }
            }
            // Official client measures to the hit grid, not the target-offset point.
            return hit.Delay + Length(center.X - sx, center.Y - sy, -sz) * hit.DelayDistFactor;
        }

        public static float DamageDelay(FightBattleHitDefinition hit,
            FightBattleSkillCastPresentation cast, FightHitTargetSelection selection, int targetIndex)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            FightHitOccurrence occurrence = selection.Occurrences == null ? null : selection.Occurrences[targetIndex];
            int centerIndex = occurrence == null ? 0 : occurrence.EffectCenterIndex;
            if (centerIndex < 0 || centerIndex >= selection.EffectCenters.Count)
                throw new InvalidOperationException("Actual hit selection has no corresponding effect centre.");
            return EffectDelay(hit, cast, selection.EffectCenters[centerIndex])
                + (occurrence == null ? 0f : occurrence.Delay);
        }

        private static float Length(float x, float y, float z) => (float)Math.Sqrt(x * x + y * y + z * z);
    }
}
