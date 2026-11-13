using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public static class SkillArcShieldColdSceneValidation
    {
        public static IEnumerator Rebuild(HomeFeatureRuntime home, WeeklyChallengeBattleRunner runner,
            string slot, string savesRoot, string output, OfficialBattleGraphCapabilityValidation probe,
            Action<FightBattleCheckpoint> completed, Action<HomeFeatureRuntime, WeeklyChallengeBattleRunner> adopt,
            Func<string, IEnumerator> capture)
        {
            runner.enabled = false;
            var saved = probe.CaptureArcColdScene(runner.Controller);
            File.WriteAllText(Path.Combine(output, "arc-shield-cold-before.json"), saved.Checkpoint);
            var session = new OfflineWeeklyChallengeSession(Path.Combine(savesRoot, "saves", slot, "weekly-challenge-save.json"));
            Require(session.TryResumeBattle(4800, out string disk, out _) && disk == saved.Checkpoint,
                "The ordinary weekly save did not persist the active arc shield exactly.");
            yield return capture("arc-shield-before-cold-scene");
            UnityEngine.Object.Destroy(runner); yield return null;
            home.CloseForNavigation(); yield return null; yield return null;
            home = HomeFeatureRuntime.Begin(slot, savesRoot, null);
            home.TutorialAutoStartEnabled = false;
            runner = home.StartWeeklyChallengeBattle(4800, completed); runner.enabled = false;
            adopt(home, runner);
            probe.BindArcColdScene(runner.Controller, saved);
            runner.Controller.StepCompleted += _ => probe.ObserveStep(runner.Controller);
            float deadline = Time.realtimeSinceStartup + 180f;
            while (!SkillDarkStatusColdSceneValidation.HudFullyVisible(home.Hud.CanvasTransform.Find("FightMainPanel"))
                || !probe.ArcColdSceneVisible(runner.Controller))
            {
                Require(Time.realtimeSinceStartup < deadline, "Restored original HUD or arc-shield particles did not render.");
                yield return null;
            }
            yield return new WaitForSecondsRealtime(.2f);
            Require(SkillDarkStatusColdSceneValidation.HudFullyVisible(home.Hud.CanvasTransform.Find("FightMainPanel"))
                && probe.ArcColdSceneVisible(runner.Controller), "Restored original HUD or arc shield became invisible.");
            string restored = FightBattleCheckpointSerializer.Save(runner.Controller.Checkpoint);
            Require(restored == saved.Checkpoint, "The resumed arc-shield battle advanced during inspection.");
            File.WriteAllText(Path.Combine(output, "arc-shield-cold-after.json"), restored);
            yield return capture("arc-shield-after-cold-scene");
            runner.enabled = true;
            Debug.Log("[Arc shield cold scene] Original weekly checkpoint, destroyed home/battle hosts, new original body/status/HUD, unchanged properties and no receipt replay passed.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Arc shield cold scene: " + message); }
    }
}
