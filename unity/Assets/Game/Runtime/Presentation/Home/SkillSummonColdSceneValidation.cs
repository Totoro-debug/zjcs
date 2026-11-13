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
    // Explicit diagnostic using the existing weekly save, not an injected replacement battle.
    public static class SkillSummonColdSceneValidation
    {
        public static bool Requested => Environment.GetEnvironmentVariable("ZJCS_SUMMON_COLD_SCENE_ACCEPTANCE") == "1";

        public static IEnumerator Rebuild(HomeFeatureRuntime home, WeeklyChallengeBattleRunner runner,
            string slot, string savesRoot, string output, OfficialBattleGraphCapabilityValidation probe,
            Action<FightBattleCheckpoint> completed, Action<HomeFeatureRuntime, WeeklyChallengeBattleRunner> adopt,
            Func<string, IEnumerator> capture)
        {
            runner.enabled = false;
            var saved = probe.CaptureSummonColdScene(runner.Controller);
            File.WriteAllText(Path.Combine(output, "summon-cold-scene-before.json"), saved.Checkpoint);
            var session = new OfflineWeeklyChallengeSession(Path.Combine(savesRoot, "saves", slot, "weekly-challenge-save.json"));
            Require(session.TryResumeBattle(4800, out string disk, out _) && disk == saved.Checkpoint,
                "The ordinary weekly disk checkpoint differs from the live summon boundary.");
            yield return capture("summon-before-cold-scene");
            UnityEngine.Object.Destroy(runner); yield return null;
            home.CloseForNavigation(); yield return null; yield return null;
            home = HomeFeatureRuntime.Begin(slot, savesRoot, null);
            home.TutorialAutoStartEnabled = false;
            runner = home.StartWeeklyChallengeBattle(4800, completed); runner.enabled = false;
            adopt(home, runner);
            probe.BindSummonColdScene(runner.Controller, saved);
            runner.Controller.StepCompleted += _ => probe.ObserveStep(runner.Controller);
            float deadline = Time.realtimeSinceStartup + 45f;
            while (!SkillDarkStatusColdSceneValidation.HudFullyVisible(home.Hud.CanvasTransform.Find("FightMainPanel"))
                || !probe.SummonColdSceneModelsVisible(runner.Controller))
            {
                Require(Time.realtimeSinceStartup < deadline, "Restored original summon meshes/HUD did not render.");
                yield return null;
            }
            yield return new WaitForSecondsRealtime(.2f);
            Require(probe.SummonColdScenePortraitsVisible(runner.Controller),
                "Restored summon original timeline portrait did not render.");
            string after = FightBattleCheckpointSerializer.Save(runner.Controller.Checkpoint);
            Require(after == saved.Checkpoint, "Restored summon battle advanced while held for visual inspection.");
            File.WriteAllText(Path.Combine(output, "summon-cold-scene-after.json"), after);
            yield return capture("summon-after-cold-scene");
            runner.enabled = true;
            Debug.Log("[Summon cold scene] Original weekly disk checkpoint, destroyed hosts, non-fresh original status/body, unchanged identities/lifespans and visible native meshes/HUD passed without receipt replay.");
            Debug.Log("[Summon portraits] Original timeline sprites, tight geometry, packed UVs and hurt-image bindings rendered after complete scene restore.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Summon cold scene: " + message); }
    }
}
