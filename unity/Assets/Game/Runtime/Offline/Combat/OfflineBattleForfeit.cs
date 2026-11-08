using System;
using Zjcs.Combat.Official;

namespace Zjcs.Offline.Combat
{
    // Confirmed arena/tower abandon policy. Keep the settled units and action
    // journal intact so normal final report creation also works during a cast.
    public static class OfflineBattleForfeit
    {
        public static bool Finish(FightBattleCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (checkpoint.Finished) return false;
            long playerHp = 0, enemyHp = 0, enemyMaxHp = 0;
            foreach (var unit in checkpoint.Units)
            {
                if (unit.Id == 1) playerHp = unit.Hp;
                if (unit.Side == 1) continue;
                enemyHp = checked(enemyHp + unit.Hp);
                enemyMaxHp = checked(enemyMaxHp + unit.MaxHp);
            }
            checkpoint.Outcome = new FightBattleOutcome
            {
                Finished = true, Won = false, Reason = 2, Round = checkpoint.Outcome.Round,
                PlayerHp = playerHp, EnemyHp = enemyHp, EnemyMaxHp = enemyMaxHp
            };
            checkpoint.PartEnded = false;
            checkpoint.Finished = true;
            checkpoint.PreparationPending = false;
            checkpoint.PreparationSecondsRemaining = 0;
            return true;
        }
    }
}
