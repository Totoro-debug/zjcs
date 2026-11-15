using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Official.BattleResultUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public static class SkillPortableFlameRestoreValidation
    {
        public static bool PrepareRequested => Environment.GetEnvironmentVariable("ZJCS_PORTABLE_FLAME_PROCESS_PHASE") == "prepare";
        public static bool ResumeRequested => Environment.GetEnvironmentVariable("ZJCS_PORTABLE_FLAME_PROCESS_PHASE") == "resume";

        private static void RequireDiskCheckpoint(string saveRoot, string slot, string checkpoint)
        {
            var session = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
            Require(session.TryResumeBattle(4800, out string disk, out _) && disk == checkpoint,
                "The ordinary weekly save did not preserve the four fire rings exactly.");
        }

        public static IEnumerator Rebuild(HomeFeatureRuntime home, WeeklyChallengeBattleRunner runner,
            string slot, string saveRoot, string output, OfficialBattleGraphCapabilityValidation probe,
            Action<FightBattleCheckpoint> completed, Action<HomeFeatureRuntime, WeeklyChallengeBattleRunner> adopt,
            Func<string, IEnumerator> capture)
        {
            runner.enabled = false;
            var saved = probe.CapturePortableFlameColdScene(runner.Controller);
            RequireDiskCheckpoint(saveRoot, slot, saved.Checkpoint);
            File.WriteAllText(Path.Combine(output, "portable-flame-cold-before.json"), saved.Checkpoint);
            yield return capture("portable-flame-before-cold-scene");
            UnityEngine.Object.Destroy(runner); yield return null;
            home.CloseForNavigation(); yield return null; yield return null;
            home = HomeFeatureRuntime.Begin(slot, saveRoot, null);
            home.TutorialAutoStartEnabled = false;
            runner = home.StartWeeklyChallengeBattle(4800, completed); runner.enabled = false;
            adopt(home, runner);
            probe.BindPortableFlameColdScene(runner.Controller, saved);
            runner.Controller.StepCompleted += _ => probe.ObserveStep(runner.Controller);
            yield return WaitForPresentation(home, runner, probe, saved.Checkpoint);
            File.WriteAllText(Path.Combine(output, "portable-flame-cold-after.json"),
                FightBattleCheckpointSerializer.Save(runner.Controller.Checkpoint));
            yield return capture("portable-flame-after-cold-scene");
            runner.enabled = true;
            Debug.Log("[Portable flame cold scene] Destroyed home/battle hosts, ordinary weekly disk checkpoint, four new original bodies/statuses, identical state and no cast-sound replay passed.");
        }

        public static IEnumerator WaitForPresentation(HomeFeatureRuntime home, WeeklyChallengeBattleRunner runner,
            OfficialBattleGraphCapabilityValidation probe, string checkpoint)
        {
            float deadline = Time.realtimeSinceStartup + 180f;
            while (true)
            {
                bool fireVisible = probe.PortableFlameColdSceneVisible(runner.Controller);
                if (fireVisible && SkillDarkStatusColdSceneValidation.HudFullyVisible(home.Hud.CanvasTransform.Find("FightMainPanel"))) break;
                Require(Time.realtimeSinceStartup < deadline, "Four restored original fire rings or the original HUD did not render.");
                yield return null;
            }
            yield return new WaitForSecondsRealtime(.2f);
            Require(probe.PortableFlameColdSceneVisible(runner.Controller)
                && SkillDarkStatusColdSceneValidation.HudFullyVisible(home.Hud.CanvasTransform.Find("FightMainPanel")),
                "Restored friendly fire rings or original bottom controls became invisible.");
            Require(FightBattleCheckpointSerializer.Save(runner.Controller.Checkpoint) == checkpoint,
                "The restored fire-ring battle advanced or replayed a receipt while held for inspection.");
        }

        public static IEnumerator Prepare(WeeklyChallengeBattleRunner runner, string saveRoot, string slot,
            string output, OfficialBattleGraphCapabilityValidation probe, Func<string, IEnumerator> capture)
        {
            runner.enabled = false;
            var saved = probe.CapturePortableFlameProcess(runner.Controller, saveRoot, slot);
            RequireDiskCheckpoint(saveRoot, slot, saved.Checkpoint);
            File.WriteAllText(Path.Combine(output, "process-handoff.json"), JsonUtility.ToJson(saved, true));
            File.WriteAllText(Path.Combine(output, "process-checkpoint-before.json"), saved.Checkpoint);
            yield return capture("portable-flame-before-process-exit");
            File.WriteAllText(Path.Combine(output, "process-prepared.json"), "{\"passed\":true}");
            Debug.Log("[Portable flame process] Four active 24210 statuses and the ordinary weekly disk checkpoint prepared; exiting process.");
            Application.Quit(0);
        }

        public static IEnumerator Resume(string output, Func<string, IEnumerator> capture,
            Func<Selectable, IEnumerator> click)
        {
            string path = Environment.GetEnvironmentVariable("ZJCS_PORTABLE_FLAME_PROCESS_HANDOFF");
            Require(!string.IsNullOrEmpty(path) && File.Exists(path), "The isolated fire-ring process handoff is missing.");
            var saved = JsonUtility.FromJson<OfficialBattleGraphCapabilityValidation.PortableFlameProcessHandoff>(File.ReadAllText(path));
            string parent = Path.GetFullPath(Path.GetDirectoryName(path)).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Require(Path.GetFullPath(saved.SaveRoot).StartsWith(parent, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(saved.SaveRoot), "Fire-ring resume may only use its prepared isolated save.");
            RequireDiskCheckpoint(saved.SaveRoot, saved.Slot, saved.Checkpoint);
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>()) camera.enabled = false;
            var home = HomeFeatureRuntime.Begin(saved.Slot, saved.SaveRoot, null);
            home.TutorialAutoStartEnabled = false;
            FightBattleCheckpoint settled = null;
            var runner = home.StartWeeklyChallengeBattle(4800, value => settled = value); runner.enabled = false;
            var controller = runner.Controller;
            var probe = OfficialBattleGraphCapabilityValidation.ResumePortableFlameProcess(controller, saved);
            controller.StepCompleted += _ => probe.ObserveStep(controller);
            yield return WaitForPresentation(home, runner, probe, saved.Checkpoint);
            File.WriteAllText(Path.Combine(output, "process-checkpoint-after.json"), FightBattleCheckpointSerializer.Save(controller.Checkpoint));
            yield return capture("portable-flame-after-process-resume");
            Debug.Log("[Portable flame process] Different process, identical ordinary weekly checkpoint, four non-fresh original status views, original HUD and no cast-sound replay passed.");
            bool resultCaptured = false;
            runner.enabled = true;
            while (runner)
            {
                var result = UnityEngine.Object.FindObjectOfType<DemonResultWindow>();
                if (result && result.IsReady)
                {
                    Require(settled != null && settled.Finished, "The resumed fire-ring result preceded settlement.");
                    yield return capture("process-result"); resultCaptured = true;
                    yield return click(result.Panel.GetComponent<GameEndPanelDemonKing>().LeaveGameBtn);
                    continue;
                }
                probe.ObserveFrame(controller);
                if (probe.CaptureName != null) yield return capture(probe.CaptureName);
                yield return null;
            }
            Require(resultCaptured && settled != null && settled.Finished, "The resumed fire-ring battle did not settle and leave.");
            probe.Verify(settled);
            File.WriteAllText(Path.Combine(output, "process-final.json"), FightBattleCheckpointSerializer.Save(settled));
            yield return new WaitForSecondsRealtime(.4f);
            Require(!UnityEngine.Object.FindObjectOfType<DemonResultWindow>(), "The resumed result remained after its simulated click.");
            yield return capture("process-return");
            File.WriteAllText(Path.Combine(output, "result.json"), "{\"passed\":true,\"error\":\"\"}");
            UnityEngine.Object.Destroy(home.gameObject); Application.Quit(0);
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Portable flame restore: " + message); }
    }
}
