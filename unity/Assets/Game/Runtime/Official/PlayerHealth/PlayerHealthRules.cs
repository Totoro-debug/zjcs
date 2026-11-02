using System;
using System.Collections.Generic;

namespace Zjcs.Official.PlayerHealth
{
    // Common.Player.HpPercent / CanAutoAddHpInFight, FormulaHandler.GetHangUpHpAddSpeed
    // and TimeManager.CalcElapsedSecond from the selected 884_156692 client.
    public static class PlayerHealthRules
    {
        public static double GetHangUpHpAddSpeed(double hpAdd, double hpAddPercent)
            => hpAdd * (1.0 + hpAddPercent);

        public static bool CanAutoAddHpInFight(IReadOnlyList<bool> currentGamesUsePlayerHp)
        {
            if (currentGamesUsePlayerHp == null || currentGamesUsePlayerHp.Count == 0) return true;
            foreach (bool usePlayerHp in currentGamesUsePlayerHp)
                if (usePlayerHp) return false;
            return true;
        }

        public static float HpPercent(float value, DateTime updatedAt, DateTime now,
            int mapCount, IReadOnlyList<bool> currentGamesUsePlayerHp, Func<double> hpAddSpeed)
        {
            if (mapCount > 0 || !CanAutoAddHpInFight(currentGamesUsePlayerHp)) return value;
            double elapsed = Math.Max(0.0, (now - updatedAt).TotalSeconds);
            double speed = hpAddSpeed();
            return (float)Math.Min(1.0, (double)value + speed * elapsed);
        }
    }
}
