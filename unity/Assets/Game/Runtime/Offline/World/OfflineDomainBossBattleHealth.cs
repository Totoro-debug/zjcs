using System;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.World
{
    /// <summary>
    /// Projects the confirmed independent-battle HP rule onto the original activity page.
    /// An active encounter uses the common checkpoint, including when replaying an older tier.
    /// Completed tiers retain the original zero-HP display; an uncompleted tier starts full.
    /// </summary>
    public static class OfflineDomainBossBattleHealth
    {
        public static float Read(OfflineDailyChallengeService progress, OfficialDailyChallengeCatalog catalog,
            int activityId, int selectedDifficulty)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (activityId == OfficialDailyChallengeCatalog.FunnyBossActivityId
                || catalog.Difficulty(activityId, selectedDifficulty) == null)
                throw new ArgumentOutOfRangeException(nameof(selectedDifficulty));

            string saved = progress.ActiveBattleCheckpoint(activityId);
            if (!string.IsNullOrEmpty(saved)
                && progress.ActiveChallengeDifficulty(activityId) == selectedDifficulty)
            {
                FightBattleCheckpoint checkpoint = FightBattleCheckpointSerializer.Load(saved);
                int stageId = catalog.StageId(activityId);
                if (checkpoint.StageId != stageId || checkpoint.StageType != catalog.StageType(activityId))
                    throw new InvalidDataException("The domain boss HP checkpoint belongs to a different stage.");
                var stage = OfflineDailyChallengeBattleCatalog.LoadDefault().FindStage(stageId);
                int monsterId = stage?.waveMonsterIdsFlat?.FirstOrDefault() ?? 0;
                if (monsterId <= 0)
                    throw new InvalidDataException("The domain boss stage has no configured first monster.");
                // Summons and player/NPC allies must never enter the activity boss's HP bar.
                var bosses = checkpoint.Units.Where(unit => unit.Side != 1 && unit.MasterId == 0
                    && unit.MonsterId == monsterId).ToArray();
                if (bosses.Length != 1 || bosses[0].MaxHp <= 0)
                    throw new InvalidDataException("The domain boss checkpoint has no unique configured boss HP.");
                return (float)Math.Max(0d, Math.Min(1d, (double)bosses[0].Hp / bosses[0].MaxHp));
            }

            // The original ActivityDomainBossPanelBattleContent.RefreshContent branches.
            // No server TotalDamage is retained between independent encounters.
            return selectedDifficulty < progress.GetHighestDifficulty(activityId) ? 0f : 1f;
        }
    }
}
