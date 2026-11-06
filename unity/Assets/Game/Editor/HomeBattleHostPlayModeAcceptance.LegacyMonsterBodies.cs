using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator RunColdWeeklyLegacyBodyRestore(HomeFeatureRuntime home,
            string slot, string root, string output, Action<Selectable> click, Action<HomeFeatureRuntime> adoptHome)
        {
            int callbacks = 0;
            var first = home.StartWeeklyChallengeBattle(4800, _ => callbacks++);
            var controller = first.Controller;
            float deadline = Time.realtimeSinceStartup + 60;
            while (controller.Phase == FightBattlePhase.Preparing)
            {
                Require(Time.realtimeSinceStartup < deadline, "Weekly preparation did not become interactive.");
                var ready = Active<Button>().FirstOrDefault(value => value.name == OfficialBattleHudView.StartButtonPath.Split('/').Last()
                    && value.transform.parent.name == "StartBtn" && value.interactable);
                if (ready != null)
                {
                    // Wait for the original HUD's layout and first rendered frame,
                    // as the ordinary host input acceptance does before clicking.
                    yield return SettleUi();
                    yield return Capture(output, "weekly-legacy-preparation");
                    click(ready);
                }
                yield return null;
            }
            while (controller.Checkpoint.Counter < 4)
            {
                Require(Time.realtimeSinceStartup < deadline && !controller.CanCompleteBattle,
                    "Weekly did not reach a resumable battle step.");
                yield return null;
            }
            controller.SetPaused(true);
            string path = Path.Combine(root, "saves", slot, "weekly-challenge-save.json");
            var session = new OfflineWeeklyChallengeSession(path);
            Require(session.TryResumeBattle(4800, out string persisted, out long period),
                "Weekly did not persist its active battle.");
            Require(persisted == FightBattleCheckpointSerializer.Save(controller.Checkpoint),
                "Weekly save differs from the actual common-module checkpoint.");
            var expected = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(
                FightBattleCheckpointSerializer.Load(persisted)).State;
            Require(expected.Counter >= 4 && !expected.Finished, "Weekly checkpoint is not resumable.");
            var legacy = FightBattleCheckpointSerializer.Load(persisted);
            foreach (var unit in legacy.Units.Where(value => value.MonsterId > 0
                && value.CharacterType != 1 && value.MasterId == 0))
            {
                unit.MonsterTemplateId = 0;
                unit.BodyOffsets = new List<FightGridPoint> { new FightGridPoint(0, 0) };
            }
            yield return Capture(output, "weekly-legacy-before-reload");
            UnityEngine.Object.Destroy(first.gameObject);
            yield return null;
            Require(callbacks == 0, "Disposing unfinished weekly combat committed a result.");
            // Only this isolated test save is degraded to the known old shape.
            session.SaveBattleCheckpoint(4800, period, FightBattleCheckpointSerializer.Save(legacy));
            home.CloseForNavigation();
            yield return null;
            yield return null;
            home = HomeFeatureRuntime.Begin(slot, root, null);
            adoptHome(home);
            home.TutorialAutoStartEnabled = false;
            Action verifyHome = CaptureHomeRestoration(home);
            var resumed = home.StartWeeklyChallengeBattle(4800, _ => callbacks++);
            var restored = resumed.Controller;
            Require(FightBattleCheckpointSerializer.Save(restored.Checkpoint)
                == FightBattleCheckpointSerializer.Save(expected),
                "Weekly legacy restore changed saved turns, HP, skills, statuses, random state or statistics.");
            Require(!restored.IsPaused && restored.Phase == FightBattlePhase.Play,
                "Weekly legacy restore restarted preparation or retained transient pause.");
            yield return Capture(output, "weekly-legacy-after-reload");
            yield return RunBattle<WeeklyChallengeBattleRunner>("weekly", output, click);
            verifyHome();
            Require(callbacks == 1 && !new OfflineWeeklyChallengeSession(path)
                .TryResumeBattle(4800, out _, out _), "Weekly did not settle exactly once and clear its checkpoint.");
            Debug.Log("[Weekly legacy body restore] Real HUD start, persisted advanced checkpoint, cold host reload, original boss body repair, unchanged combat state and resumed settlement passed.");
        }
    }
}
