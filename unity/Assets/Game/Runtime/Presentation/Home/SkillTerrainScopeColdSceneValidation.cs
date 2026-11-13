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
    public static class SkillTerrainScopeColdSceneValidation
    {
        public static bool Requested => Environment.GetEnvironmentVariable("ZJCS_MOVE_SCOPE_COLD_SCENE") == "1"
            || Environment.GetEnvironmentVariable("ZJCS_HIT_SCOPE_COLD_SCENE") == "1";

        public static IEnumerator Rebuild(HomeFeatureRuntime home, WeeklyChallengeBattleRunner runner,
            string slot, string savesRoot, string output, OfficialBattleGraphCapabilityValidation probe,
            Action<FightBattleCheckpoint> completed, Action<HomeFeatureRuntime, WeeklyChallengeBattleRunner> adopt,
            Func<string, IEnumerator> capture)
        {
            runner.enabled = false;
            // Hold the settled turn, then let its final native dispatch and
            // deferred MyStart reach the next frame before inspecting views.
            yield return null;
            var saved = probe.CaptureTerrainScopeColdScene(runner.Controller);
            string path = Path.Combine(savesRoot, "saves", slot, "weekly-challenge-save.json");
            var session = new OfflineWeeklyChallengeSession(path);
            Require(session.TryResumeBattle(4800, out string disk, out _) && disk == saved.Checkpoint,
                "Ordinary disk checkpoint differs from the live ground boundary.");
            File.WriteAllText(Path.Combine(output, probe.TerrainScopeCapturePrefix + "-cold-before.json"), saved.Checkpoint);
            float deadline = Time.realtimeSinceStartup + 45f;
            while (!probe.TerrainScopeColdFireVisible(runner.Controller))
            { Require(Time.realtimeSinceStartup < deadline, "Original pre-restore fire sprites did not render."); yield return null; }
            yield return capture(probe.TerrainScopeCapturePrefix + "-before-cold-scene");
            UnityEngine.Object.Destroy(runner); yield return null;
            home.CloseForNavigation(); yield return null; yield return null;
            home = HomeFeatureRuntime.Begin(slot, savesRoot, null);
            home.TutorialAutoStartEnabled = false;
            runner = home.StartWeeklyChallengeBattle(4800, completed); runner.enabled = false;
            adopt(home, runner);
            probe.BindTerrainScopeColdScene(runner.Controller, saved);
            runner.Controller.StepCompleted += _ => probe.ObserveStep(runner.Controller);
            deadline = Time.realtimeSinceStartup + 45f;
            while (!SkillDarkStatusColdSceneValidation.HudFullyVisible(home.Hud.CanvasTransform.Find("FightMainPanel"))
                || !probe.TerrainScopeColdFireVisible(runner.Controller))
            { Require(Time.realtimeSinceStartup < deadline, "Restored native ground sprites/HUD did not render."); yield return null; }
            string after = FightBattleCheckpointSerializer.Save(runner.Controller.Checkpoint);
            Require(after == saved.Checkpoint, "Held ground restoration advanced or replayed its checkpoint.");
            File.WriteAllText(Path.Combine(output, probe.TerrainScopeCapturePrefix + "-cold-after.json"), after);
            yield return capture(probe.TerrainScopeCapturePrefix + "-after-cold-scene");
            runner.enabled = true;
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Terrain scope cold scene: " + message); }
    }
}
