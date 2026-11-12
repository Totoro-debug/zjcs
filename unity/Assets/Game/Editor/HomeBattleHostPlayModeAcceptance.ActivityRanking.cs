using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static void PrepareActivityRankingLoadout(HomeFeatureRuntime home)
        {
            // Actual original profession-1 recommendations: charge (10670) and ranged hit (10590).
            // Keep their configured ranges/movement/CD/damage; the isolated role must exercise
            // real positive damage as well as the separately checked zero-damage completion.
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            Require(home.Profile.ApplyCharacterMutation(player =>
            {
                foreach (int id in new[] { 10015, 10043 })
                    if (!player.Skills.Skills.Any(value => value.ClassId == id))
                        player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = id, Rank = 1 });
            }, "activity-ranking-recommended-skills").IsApplied, "Could not prepare the isolated recommended skills.");
            var progression = new OfflineCharacterProgressionService(home.Profile, assets.Load());
            progression.AttachSkillRules(assets.LoadSkillRules(), assets.LoadSkillRecommendations().recommendations);
            Require(progression.EquipSkill(10015, 0).IsApplied && progression.EquipSkill(10043, 1).IsApplied,
                "Could not equip the original charge/ranged activity fixture.");
        }

        private static void VerifyDomainBossRankingRules(string output)
        {
            string path = Path.Combine(output, "domain-boss-rank-rules-" + Guid.NewGuid().ToString("N") + ".json");
            var progress = new OfflineDailyChallengeService(path);
            var now = new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Local);
            const int duration = 335;
            Require(progress.DomainBossMaxDamage(209, now, duration) == null,
                "An unplayed domain boss fabricated a rank entry.");
            progress.EnterFeature(now);
            progress.RecordParticipation(209, now, duration, 1);
            Require(progress.DomainBossMaxDamage(209, now, duration) == null,
                "Starting/interruption fabricated a completed rank entry.");
            progress.RecordDomainBossResult(209, now, duration, 0);
            Require(new OfflineDailyChallengeService(path).DomainBossMaxDamage(209, now, duration) == 0,
                "A real zero-damage completion was confused with an empty ranking.");
            progress.RecordDomainBossResult(209, now, duration, 1234);
            progress.RecordDomainBossResult(209, now, duration, 50);
            progress.RecordDomainBossResult(209, now, duration, 1234);
            progress = new OfflineDailyChallengeService(path);
            Require(progress.DomainBossMaxDamage(209, now, duration) == 1234,
                "Cold reload/replayed settlement lost the original highest damage.");
            progress.ParticipationCount(209, now.AddDays(1));
            Require(progress.DomainBossMaxDamage(209, now.AddDays(1), duration) == 1234,
                "The 06:00 reset incorrectly cleared a period ranking.");
            Require(progress.DomainBossMaxDamage(212, now.AddDays(1), duration) == null,
                "A different activity inherited another boss's score.");
            Require(progress.DomainBossMaxDamage(209, now.AddHours(duration), duration) == null,
                "A previous period leaked into the new ranking.");
            Require(new OfflineDailyChallengeService(path).DomainBossMaxDamage(209, now, duration) == null,
                "Clock rollback resurrected an old rank.");
            progress.RecordDomainBossResult(209, now.AddHours(duration), duration, 90);
            Require(progress.DomainBossMaxDamage(209, now.AddHours(duration), duration) == 90,
                "A new period inherited the previous maximum.");

            // Controlled report fixture, not a fabricated gameplay outcome. The expected 35
            // follows original target-type filtering: 10 HP + 5 shield + 20 from an NPC source.
            var checkpoint = new FightBattleCheckpoint();
            checkpoint.Units.Add(new FightBattleUnitState { Id = 1, CharacterType = 1 });
            checkpoint.Units.Add(new FightBattleUnitState { Id = 2, CharacterType = 2 });
            checkpoint.Units.Add(new FightBattleUnitState { Id = 3, CharacterType = 4 });
            var step = new FightBattleStepEvent();
            step.DamageEvents.Add(new FightBattleDamageEvent { SourceId = 1, TargetId = 2, Damage = -10, ShieldDamage = 5 });
            step.DamageEvents.Add(new FightBattleDamageEvent { SourceId = 3, TargetId = 2, Damage = -20 });
            step.DamageEvents.Add(new FightBattleDamageEvent { SourceId = 2, TargetId = 1, Damage = -100 });
            step.DamageEvents.Add(new FightBattleDamageEvent { SourceId = 1, TargetId = 2, Damage = 200, IsCure = true });
            step.DamageEvents.Add(new FightBattleDamageEvent { SourceId = 1, TargetId = 2, Damage = -300, IsSkillCost = true });
            step.DamageEvents.Add(new FightBattleDamageEvent { SourceId = 1, TargetId = 999, Damage = -400 });
            checkpoint.Receipts.Add(new FightBattleStepReceipt { Event = step });
            Require(FightBattleReportBuilder.Build(checkpoint).DamageToMonsters == 35,
                "The common report deviated from original target/HP/shield/cure/cost filtering.");
            Debug.Log("[Domain boss rank] empty/zero, highest damage, cold load, daily/period reset, rollback, independent activities and original hit filter passed.");
        }

        private static void VerifyDomainBossActualRank(HomeFeatureRuntime home, int activity,
            FightBattleCheckpoint checkpoint)
        {
            if (activity == OfflineDailyChallengeService.FunnyBossActivityId) return;
            Require(checkpoint.Finished && checkpoint.Outcome != null && !checkpoint.Outcome.Invalid,
                "The rank assertion requires a real completed battle.");
            long expected = 0;
            foreach (var receipt in checkpoint.Receipts)
                foreach (var hit in receipt.Event.DamageEvents)
                {
                    var target = checkpoint.Units.Single(value => value.Id == hit.TargetId);
                    if (target.CharacterType == 2 && !hit.IsCure && !hit.IsSkillCost)
                        expected = checked(expected + Math.Abs(hit.Damage) + hit.ShieldDamage);
                }
            var report = FightBattleReportBuilder.Build(checkpoint);
            Require(report.DamageToMonsters == expected,
                "The final common report lost actual domain-boss damage.");
            var catalog = OfficialDailyChallengeCatalog.LoadDefault();
            long? saved = home.DailyChallenges.DomainBossMaxDamage(activity, DateTime.Now, catalog.DurationHours(activity));
            Require(saved == expected, "The real completion did not persist its ranking score.");
            string path = Path.Combine(new ProfileSlotCatalog(home.SavesRoot).GetSlotDirectory(home.ActiveProfileSlotId),
                "daily-challenges.json");
            Require(new OfflineDailyChallengeService(path).DomainBossMaxDamage(activity, DateTime.Now,
                catalog.DurationHours(activity)) == expected, "The real rank did not survive cold save loading.");
            Require(string.IsNullOrEmpty(home.DailyChallenges.ActiveBattleCheckpoint(activity)),
                "The rank completion left an active encounter checkpoint.");
            Debug.Log("[Domain boss rank] real activity " + activity + " won=" + checkpoint.Outcome.Won
                + " damage=" + expected + " receipts=" + checkpoint.Receipts.Count + "; common report/persistence/cold load passed.");
        }
    }
}
