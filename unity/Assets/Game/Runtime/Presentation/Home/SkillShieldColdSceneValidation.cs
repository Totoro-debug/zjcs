using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    // Explicit diagnostic: rebuild both hosts using the ordinary weekly save/resume path.
    public static class SkillShieldColdSceneValidation
    {
        public static IEnumerator Rebuild(HomeFeatureRuntime home, WeeklyChallengeBattleRunner runner,
            string slot, string savesRoot, string output, OfficialBattleGraphCapabilityValidation probe,
            Action<FightBattleCheckpoint> completed, Action<HomeFeatureRuntime, WeeklyChallengeBattleRunner> adopt,
            Func<string, IEnumerator> capture)
        {
            runner.enabled = false;
            var saved = probe.CaptureShieldColdScene(runner.Controller);
            File.WriteAllText(Path.Combine(output, "shield-cold-scene-before.json"), saved.Checkpoint);
            var session = new OfflineWeeklyChallengeSession(Path.Combine(savesRoot, "saves", slot, "weekly-challenge-save.json"));
            Require(session.TryResumeBattle(4800, out string disk, out _) && disk == saved.Checkpoint,
                "The ordinary weekly checkpoint did not persist the active shield exactly.");
            yield return capture("shield-before-cold-scene");
            UnityEngine.Object.Destroy(runner); yield return null;
            home.CloseForNavigation(); yield return null; yield return null;
            home = HomeFeatureRuntime.Begin(slot, savesRoot, null);
            home.TutorialAutoStartEnabled = false;
            runner = home.StartWeeklyChallengeBattle(4800, completed); runner.enabled = false;
            adopt(home, runner);
            probe.BindShieldColdScene(runner.Controller, saved);
            runner.Controller.StepCompleted += _ => probe.ObserveStep(runner.Controller);
            yield return WaitForPresentation(home, runner, probe, saved.Checkpoint);
            File.WriteAllText(Path.Combine(output, "shield-cold-scene-after.json"),
                FightBattleCheckpointSerializer.Save(runner.Controller.Checkpoint));
            yield return capture("shield-after-cold-scene");
            probe.ArmShieldExpiryFilter(runner.Controller);
            runner.enabled = true;
            Debug.Log("[Shield cold scene] Original weekly disk checkpoint, destroyed home/battle hosts, new original status/body/HUD, unchanged identity/count and no receipt replay passed.");
        }

        public static IEnumerator WaitForPresentation(HomeFeatureRuntime home, WeeklyChallengeBattleRunner runner,
            OfficialBattleGraphCapabilityValidation probe, string checkpoint)
        {
            float deadline = Time.realtimeSinceStartup + 180f;
            while (true)
            {
                var hud = home.Hud.CanvasTransform.Find("FightMainPanel");
                if (SkillDarkStatusColdSceneValidation.HudFullyVisible(hud) && probe.ShieldColdSceneVisible(runner.Controller)) break;
                Require(Time.realtimeSinceStartup < deadline, "Restored original HUD/body-bound shield did not render.");
                yield return null;
            }
            // Preserve the native entrance animation; do not capture its first
            // active frame while the bottom controls are still outside the screen.
            yield return new WaitForSecondsRealtime(.2f);
            Require(SkillDarkStatusColdSceneValidation.HudFullyVisible(home.Hud.CanvasTransform.Find("FightMainPanel")),
                "Restored original bottom controls did not remain fully on screen.");
            Require(FightBattleCheckpointSerializer.Save(runner.Controller.Checkpoint) == checkpoint,
                "Restored battle advanced while its runner was held for visual inspection.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Shield cold scene: " + message); }

    }
}
