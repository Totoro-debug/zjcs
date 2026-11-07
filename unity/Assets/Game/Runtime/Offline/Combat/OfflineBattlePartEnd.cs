using System;
using Zjcs.Combat.Official;

namespace Zjcs.Offline.Combat
{
    /// <summary>Confirmed solo substitute for the server's PartEndGame response.
    /// Stops at the current settled state, retaining damage, drops, cooldowns and receipts.</summary>
    public static class OfflineBattlePartEnd
    {
        public static bool Finish(FightBattleCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (checkpoint.Finished) return false;
            long playerHp = 0, enemyHp = 0, enemyMaxHp = 0;
            foreach (FightBattleUnitState unit in checkpoint.Units)
            {
                if (unit.Id == 1) playerHp = unit.Hp;
                if (unit.Side == 1) continue;
                enemyHp = checked(enemyHp + unit.Hp);
                enemyMaxHp = checked(enemyMaxHp + unit.MaxHp);
            }
            checkpoint.Outcome = new FightBattleOutcome
            {
                Finished = true, Won = false, Round = checkpoint.Outcome.Round,
                PlayerHp = playerHp, EnemyHp = enemyHp, EnemyMaxHp = enemyMaxHp
            };
            checkpoint.PartEnded = true;
            checkpoint.Finished = true;
            checkpoint.PreparationPending = false;
            checkpoint.PreparationSecondsRemaining = 0;
            return true;
        }
    }
}
