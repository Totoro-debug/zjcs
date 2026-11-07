using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator RunDemonAwardHost(HomeFeatureRuntime home, string slot,
            string savesRoot, string output, Action<Selectable> click, Action<HomeFeatureRuntime> adoptHome)
        {
            WeeklyChallengeSessionAcceptance.Run();
            Combat.BattleGameViewInput.Focus();
            yield return Combat.BattleGameViewInput.ResizePortrait(450);
            long initialItems = home.ItemCount(41021);
            string receipt = null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                FightBattleCheckpoint completed = null;
                FightBattleReport report = null;
                OfflineWeeklyChallengeSession.ChallengeAward award = null;
                WeeklyChallengeBattleRunner runner = null;
                var verifyHome = CaptureHomeRestoration(home);
                runner = home.StartWeeklyChallengeBattle(4800, value =>
                {
                    Require(report != null, "Demon award settlement preceded the final report.");
                    completed = value;
                    award = runner.FinalChallengeAward;
                });
                // Freeze only step submission. Original HUD, phase transitions,
                // confirmation input and the normal PartEnd settlement still run.
                runner.enabled = false;
                var controller = runner.Controller;
                controller.BattleReportReady += value => report = value;
                yield return WaitForLeaveButton(controller, click, true);
                Require(controller.Checkpoint.Counter == 0, "Zero-damage fixture advanced a combat step.");
                yield return ClickBattleLeave();
                yield return new WaitForSecondsRealtime(2);
                yield return Capture(output, "demon-award-confirm-" + attempt);
                yield return Combat.BattleGameViewInput.Click(Active<WorldConfirmDialog>().Single().ConfirmBtn);
                float deadline = Time.realtimeSinceStartup + 30;
                while (completed == null)
                {
                    Require(Time.realtimeSinceStartup < deadline, "Zero-damage settlement did not finish.");
                    yield return null;
                }
                yield return SettleUi();
                verifyHome();
                Require(completed.PartEnded && completed.Counter == 0 && !completed.Outcome.Won
                    && completed.Outcome.CanSettle && report.Units.All(value => value.DamageDealt == 0),
                    "Zero-damage exit fabricated actions, damage or victory.");
                if (attempt == 0)
                {
                    Require(award != null && award.ItemId == 41021 && award.Count == 300,
                        "First zero-damage exit did not grant the original challenge award.");
                    receipt = award.Receipt;
                }
                else Require(award == null, "Same-period second exit granted a duplicate challenge award.");
                Require(home.ItemCount(41021) == initialItems + 300
                    && home.Profile.Save.Player.AppliedInventoryEventIds.Count(value => value == receipt) == 1,
                    "Demon reward inventory and receipt are inconsistent.");
                string weeklyPath = Path.Combine(savesRoot, "saves", slot, "weekly-challenge-save.json");
                var saved = JsonUtility.FromJson<WeeklyChallengeSave>(File.ReadAllText(weeklyPath));
                Require(saved.periods.Sum(value => value.demonKingChallengeAwardsClaimed) == 1
                    && saved.periods.All(value => value.demonKingDamage == 0)
                    && !new OfflineWeeklyChallengeSession(weeklyPath).TryResumeBattle(4800, out _, out _),
                    "Zero-damage settlement did not persist its consumed qualification and cleared checkpoint.");
                home.CloseForNavigation(); yield return null; yield return null;
                home = HomeFeatureRuntime.Begin(slot, savesRoot, null); adoptHome(home);
                home.TutorialAutoStartEnabled = false;
                Require(home.ItemCount(41021) == initialItems + 300
                    && home.Profile.Save.Player.AppliedInventoryEventIds.Count(value => value == receipt) == 1,
                    "Cold reopening lost the item grant or its idempotent receipt.");
            }
            yield return Capture(output, "demon-award-cold-home");
            Debug.Log("[Demon challenge award] simulated zero-damage exit, original 300x41021, same-period repeat and cold inventory receipt passed.");
        }
    }
}
