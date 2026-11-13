using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public static class SkillTerrainScopeProcessValidation
    {
        public static bool PrepareRequested => Environment.GetEnvironmentVariable("ZJCS_TERRAIN_PROCESS_PHASE") == "prepare";
        public static bool ResumeRequested => Environment.GetEnvironmentVariable("ZJCS_TERRAIN_PROCESS_PHASE") == "resume";

        public static IEnumerator Prepare(WeeklyChallengeBattleRunner runner, string saveRoot, string slot,
            string output, OfficialBattleGraphCapabilityValidation probe, Func<string, IEnumerator> capture)
        {
            Require(!Application.isEditor, "Actual application exit requires a Windows Player.");
            runner.enabled = false;
            yield return null;
            var saved = probe.CaptureTerrainProcess(runner.Controller, saveRoot, slot);
            Require(OpenSession(saved).TryResumeBattle(4800, out string disk, out _) && disk == saved.Checkpoint,
                "The ordinary weekly save differs from the actual settled terrain boundary.");
            float deadline = Time.realtimeSinceStartup + 45f;
            while (!probe.TerrainScopeColdFireVisible(runner.Controller))
            {
                Require(Time.realtimeSinceStartup < deadline, "Original pre-exit fire sprites did not render.");
                yield return null;
            }
            File.WriteAllText(Path.Combine(output, "process-handoff.json"), JsonUtility.ToJson(saved, true));
            File.WriteAllText(Path.Combine(output, "process-checkpoint-before.json"), saved.Checkpoint);
            yield return capture("terrain-process-before-exit");
            File.WriteAllText(Path.Combine(output, "process-prepared.json"), "{\"passed\":true}");
            Debug.Log("[Terrain process] Original burning cells visible at their ordinary save boundary; exiting normally.");
            Application.Quit(0);
        }

        public static IEnumerator Resume(string output, Func<string, IEnumerator> capture,
            Func<Selectable, IEnumerator> click)
        {
            Require(!Application.isEditor, "Actual application restart requires a Windows Player.");
            string path = Environment.GetEnvironmentVariable("ZJCS_TERRAIN_PROCESS_HANDOFF");
            Require(!string.IsNullOrEmpty(path) && File.Exists(path), "Terrain process observations are absent.");
            var saved = JsonUtility.FromJson<OfficialBattleGraphCapabilityValidation.TerrainProcessHandoff>(File.ReadAllText(path));
            string parent = Path.GetFullPath(Path.GetDirectoryName(path)).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            Require(Path.GetFullPath(saved.SaveRoot).StartsWith(parent, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(saved.SaveRoot), "Only the prepared isolated profile may be resumed.");
            Require(OpenSession(saved).TryResumeBattle(4800, out string disk, out _) && disk == saved.Checkpoint,
                "Normal application exit changed or lost the original ground checkpoint.");
            Time.captureFramerate = 60;
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>()) camera.enabled = false;
            var home = HomeFeatureRuntime.Begin(saved.Slot, saved.SaveRoot, null);
            home.TutorialAutoStartEnabled = false;
            FightBattleCheckpoint settled = null;
            var runner = home.StartWeeklyChallengeBattle(4800, value => settled = value);
            runner.enabled = false;
            var controller = runner.Controller;
            using (var probe = new OfficialBattleGraphCapabilityValidation.TerrainProcessResumeProbe(controller, saved))
            {
                float deadline = Time.realtimeSinceStartup + 45f;
                while (!SkillDarkStatusColdSceneValidation.HudFullyVisible(home.Hud.CanvasTransform.Find("FightMainPanel"))
                    || !probe.FireVisible())
                {
                    Require(Time.realtimeSinceStartup < deadline, "Resumed original fire sprites/HUD did not render.");
                    yield return null;
                }
                string after = FightBattleCheckpointSerializer.Save(controller.Checkpoint);
                Require(after == saved.Checkpoint, "Held native terrain restoration advanced its ordinary save.");
                File.WriteAllText(Path.Combine(output, "process-checkpoint-after.json"), after);
                yield return capture("terrain-process-restored");
                Debug.Log("[Terrain process] Different Windows process restored the exact ordinary checkpoint and non-fresh original fire views.");
                runner.enabled = true;
                while (!probe.Ended)
                {
                    probe.ObserveFrame();
                    string name = probe.TakeCapture();
                    if (name != null) yield return capture(name);
                    yield return null;
                }
                Debug.Log("[Terrain process] Every saved original ground status/entity/graph retired without replaying the passive.");
            }
            Button close = null;
            float hudDeadline = Time.realtimeSinceStartup + 30f;
            while (!close)
            {
                Require(Time.realtimeSinceStartup < hudDeadline, "The original partial-settlement button did not appear.");
                close = UnityEngine.Object.FindObjectsOfType<Button>().SingleOrDefault(value =>
                    value.name == "CloseBtn_normal" && value.transform.parent.name == "CloseBtn"
                    && value.gameObject.activeInHierarchy && value.IsInteractable());
                yield return null;
            }
            yield return click(close);
            WorldConfirmDialog confirm = null;
            float confirmDeadline = Time.realtimeSinceStartup + 30f;
            while (!confirm)
            {
                Require(Time.realtimeSinceStartup < confirmDeadline, "The original leave confirmation did not appear.");
                confirm = UnityEngine.Object.FindObjectsOfType<WorldConfirmDialog>().SingleOrDefault(value =>
                    value.name == "OfficialBattleLeaveConfirmDialog" && value.gameObject.activeInHierarchy);
                yield return null;
            }
            yield return capture("process-leave-confirm");
            yield return click(confirm.ConfirmBtn);
            bool resultCaptured = false;
            while (runner)
            {
                var result = UnityEngine.Object.FindObjectOfType<DemonResultWindow>();
                if (result && result.IsReady)
                {
                    Require(settled != null && settled.Finished, "Original result preceded settlement.");
                    yield return capture("process-result"); resultCaptured = true;
                    yield return click(result.Panel.GetComponent<GameEndPanelDemonKing>().LeaveGameBtn);
                }
                yield return null;
            }
            Require(resultCaptured && settled != null && settled.Finished
                && !OpenSession(saved).TryResumeBattle(4800, out _, out _),
                "Releasing a settled battle resurrected its saved terrain checkpoint.");
            File.WriteAllText(Path.Combine(output, "process-final.json"), FightBattleCheckpointSerializer.Save(settled));
            yield return new WaitForSecondsRealtime(.4f);
            Require(!UnityEngine.Object.FindObjectOfType<DemonResultWindow>(), "Original result remained after its simulated click.");
            yield return capture("process-return");
            File.WriteAllText(Path.Combine(output, "result.json"), "{\"passed\":true,\"error\":\"\"}");
            UnityEngine.Object.Destroy(home.gameObject); Application.Quit(0);
        }

        private static OfflineWeeklyChallengeSession OpenSession(OfficialBattleGraphCapabilityValidation.TerrainProcessHandoff saved)
            => new OfflineWeeklyChallengeSession(Path.Combine(saved.SaveRoot, "saves", saved.Slot, "weekly-challenge-save.json"));
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Terrain scope process: " + message); }
    }
}
