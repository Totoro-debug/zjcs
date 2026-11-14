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
    public static class SkillSwordCounterattackProcessValidation
    {
        public static bool PrepareRequested => Environment.GetEnvironmentVariable("ZJCS_SWORD_PROCESS_PHASE") == "prepare";
        public static bool ResumeRequested => Environment.GetEnvironmentVariable("ZJCS_SWORD_PROCESS_PHASE") == "resume";

        public static IEnumerator Prepare(WeeklyChallengeBattleRunner runner, string saveRoot, string slot,
            string output, OfficialBattleGraphCapabilityValidation probe, Func<string, IEnumerator> capture)
        {
            Require(!Application.isEditor, "Actual application exit requires a Windows Player.");
            runner.enabled = false;
            var saved = probe.CaptureSwordProcess(runner.Controller, saveRoot, slot);
            RequireDiskCheckpoint(saved);
            File.WriteAllText(Path.Combine(output, "process-handoff.json"), JsonUtility.ToJson(saved, true));
            File.WriteAllText(Path.Combine(output, "process-checkpoint-before.json"), saved.Checkpoint);
            yield return capture("sword-process-before-exit");
            File.WriteAllText(Path.Combine(output, "process-prepared.json"), "{\"passed\":true}");
            Debug.Log("[Sword process] First original enemy hit and unfired secret saved; exiting normally.");
            Application.Quit(0);
        }

        public static IEnumerator Resume(string output, Func<string, IEnumerator> capture, Func<Selectable, IEnumerator> click)
        {
            Require(!Application.isEditor, "Actual application restart requires a Windows Player.");
            string path = Environment.GetEnvironmentVariable("ZJCS_SWORD_PROCESS_HANDOFF");
            Require(!string.IsNullOrEmpty(path) && File.Exists(path), "Sword process handoff is absent.");
            var saved = JsonUtility.FromJson<OfficialBattleGraphCapabilityValidation.SwordProcessHandoff>(File.ReadAllText(path));
            string parent = Path.GetFullPath(Path.GetDirectoryName(path)).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            Require(Path.GetFullPath(saved.SaveRoot).StartsWith(parent, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(saved.SaveRoot), "Only the prepared isolated profile may be resumed.");
            RequireDiskCheckpoint(saved);
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>()) camera.enabled = false;
            var home = HomeFeatureRuntime.Begin(saved.Slot, saved.SaveRoot, null);
            home.TutorialAutoStartEnabled = false;
            FightBattleCheckpoint settled = null;
            var runner = home.StartWeeklyChallengeBattle(4800, value => settled = value); runner.enabled = false;
            var controller = runner.Controller;
            var probe = OfficialBattleGraphCapabilityValidation.ResumeSwordProcess(controller, saved);
            controller.StepCompleted += _ => probe.ObserveStep(controller);
            float deadline = Time.realtimeSinceStartup + 180f;
            while (!SkillDarkStatusColdSceneValidation.HudFullyVisible(home.Hud.CanvasTransform.Find("FightMainPanel"))
                || !probe.SwordProcessBodyVisible(controller, saved))
            {
                Require(Time.realtimeSinceStartup < deadline, "Resumed original body, non-fresh secret or HUD did not render.");
                yield return null;
            }
            Require(FightBattleCheckpointSerializer.Save(controller.Checkpoint) == saved.Checkpoint,
                "Sword battle advanced or replayed a receipt during paused inspection.");
            File.WriteAllText(Path.Combine(output, "process-checkpoint-after.json"), saved.Checkpoint);
            yield return capture("sword-process-after-resume");
            Debug.Log("[Sword process] Different process restored the exact first-hit checkpoint, unfired secret, original body and HUD without receipt replay.");
            bool resultCaptured = false; runner.enabled = true;
            while (runner)
            {
                var result = UnityEngine.Object.FindObjectOfType<DemonResultWindow>();
                if (result && result.IsReady)
                {
                    Require(settled != null && settled.Finished, "Sword result preceded settlement.");
                    yield return capture("process-result"); resultCaptured = true;
                    yield return click(result.Panel.GetComponent<GameEndPanelDemonKing>().LeaveGameBtn);
                    continue;
                }
                probe.ObserveFrame(controller);
                if (probe.CaptureName != null) yield return capture(probe.CaptureName);
                yield return null;
            }
            Require(resultCaptured && settled != null && settled.Finished, "Sword resumed battle did not settle and leave.");
            probe.Verify(settled);
            File.WriteAllText(Path.Combine(output, "process-final.json"), FightBattleCheckpointSerializer.Save(settled));
            yield return new WaitForSecondsRealtime(.4f);
            Require(!UnityEngine.Object.FindObjectOfType<DemonResultWindow>(), "Sword result remained after its simulated click.");
            yield return capture("process-return");
            File.WriteAllText(Path.Combine(output, "result.json"), "{\"passed\":true,\"error\":\"\"}");
            UnityEngine.Object.Destroy(home.gameObject); Application.Quit(0);
        }

        private static void RequireDiskCheckpoint(OfficialBattleGraphCapabilityValidation.SwordProcessHandoff saved)
        {
            var session = new OfflineWeeklyChallengeSession(Path.Combine(saved.SaveRoot, "saves", saved.Slot, "weekly-challenge-save.json"));
            Require(session.TryResumeBattle(4800, out string disk, out _) && disk == saved.Checkpoint,
                "The ordinary weekly first-hit checkpoint changed across process exit.");
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Sword counterattack process: " + message); }
    }
}
