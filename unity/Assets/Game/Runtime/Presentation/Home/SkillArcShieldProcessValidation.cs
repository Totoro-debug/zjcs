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
    public static class SkillArcShieldProcessValidation
    {
        public static bool PrepareRequested => Environment.GetEnvironmentVariable("ZJCS_ARC_SHIELD_PROCESS_PHASE") == "prepare";
        public static bool ResumeRequested => Environment.GetEnvironmentVariable("ZJCS_ARC_SHIELD_PROCESS_PHASE") == "resume";

        public static IEnumerator Prepare(WeeklyChallengeBattleRunner runner, string saveRoot, string slot,
            string output, OfficialBattleGraphCapabilityValidation probe, Func<string, IEnumerator> capture)
        {
            runner.enabled = false;
            var saved = probe.CaptureArcProcess(runner.Controller, saveRoot, slot);
            RequireDiskCheckpoint(saved);
            File.WriteAllText(Path.Combine(output, "process-handoff.json"), JsonUtility.ToJson(saved, true));
            File.WriteAllText(Path.Combine(output, "process-checkpoint-before.json"), saved.Checkpoint);
            yield return capture("arc-shield-before-process-exit");
            File.WriteAllText(Path.Combine(output, "process-prepared.json"), "{\"passed\":true}");
            Debug.Log("[Arc shield process] Original active 24030 and ordinary weekly checkpoint prepared; exiting process.");
            Application.Quit(0);
        }

        public static IEnumerator Resume(string output, Func<string, IEnumerator> capture,
            Func<Selectable, IEnumerator> click)
        {
            string path = Environment.GetEnvironmentVariable("ZJCS_ARC_SHIELD_PROCESS_HANDOFF");
            Require(!string.IsNullOrEmpty(path) && File.Exists(path), "Arc process handoff is missing.");
            var saved = JsonUtility.FromJson<OfficialBattleGraphCapabilityValidation.ArcProcessHandoff>(File.ReadAllText(path));
            string parent = Path.GetFullPath(Path.GetDirectoryName(path)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            Require(Path.GetFullPath(saved.SaveRoot).StartsWith(parent, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(saved.SaveRoot), "Arc resume may only use the prepared isolated profile.");
            RequireDiskCheckpoint(saved);
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>()) camera.enabled = false;
            var home = HomeFeatureRuntime.Begin(saved.Slot, saved.SaveRoot, null);
            home.TutorialAutoStartEnabled = false;
            FightBattleCheckpoint settled = null;
            var runner = home.StartWeeklyChallengeBattle(4800, value => settled = value);
            runner.enabled = false;
            var controller = runner.Controller;
            var probe = OfficialBattleGraphCapabilityValidation.ResumeArcProcess(controller, saved);
            controller.StepCompleted += _ => probe.ObserveStep(controller);
            yield return SkillArcShieldColdSceneValidation.WaitForPresentation(home, runner, probe, saved.Checkpoint);
            File.WriteAllText(Path.Combine(output, "process-checkpoint-after.json"),
                FightBattleCheckpointSerializer.Save(controller.Checkpoint));
            yield return capture("arc-shield-after-process-resume");
            Debug.Log("[Arc shield process] Different process, original weekly checkpoint, non-fresh original body/status/HUD, source and block properties passed without receipt replay.");
            bool resultCaptured = false;
            runner.enabled = true;
            while (runner)
            {
                var result = UnityEngine.Object.FindObjectOfType<DemonResultWindow>();
                if (result && result.IsReady)
                {
                    Require(settled != null && settled.Finished, "Arc resumed result preceded settlement.");
                    yield return capture("process-result"); resultCaptured = true;
                    yield return click(result.Panel.GetComponent<GameEndPanelDemonKing>().LeaveGameBtn);
                    continue;
                }
                probe.ObserveFrame(controller);
                if (probe.CaptureName != null) yield return capture(probe.CaptureName);
                yield return null;
            }
            Require(resultCaptured && settled != null && settled.Finished, "Arc resumed battle did not settle and leave.");
            probe.Verify(settled);
            File.WriteAllText(Path.Combine(output, "process-final.json"), FightBattleCheckpointSerializer.Save(settled));
            yield return new WaitForSecondsRealtime(.4f);
            Require(!UnityEngine.Object.FindObjectOfType<DemonResultWindow>(), "Arc result remained after its simulated click.");
            yield return capture("process-return");
            File.WriteAllText(Path.Combine(output, "result.json"), "{\"passed\":true,\"error\":\"\"}");
            UnityEngine.Object.Destroy(home.gameObject); Application.Quit(0);
        }

        private static void RequireDiskCheckpoint(OfficialBattleGraphCapabilityValidation.ArcProcessHandoff saved)
        {
            var session = new OfflineWeeklyChallengeSession(Path.Combine(saved.SaveRoot, "saves", saved.Slot, "weekly-challenge-save.json"));
            Require(session.TryResumeBattle(4800, out string disk, out _) && disk == saved.Checkpoint,
                "The persisted weekly arc-shield checkpoint changed across process exit.");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Arc shield process: " + message); }
    }
}
