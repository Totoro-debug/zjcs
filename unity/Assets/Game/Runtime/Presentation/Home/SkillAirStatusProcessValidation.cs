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
    public static class SkillAirStatusProcessValidation
    {
        public static bool PrepareRequested => Environment.GetEnvironmentVariable("ZJCS_AIR_PROCESS_PHASE") == "prepare";
        public static bool ResumeRequested => Environment.GetEnvironmentVariable("ZJCS_AIR_PROCESS_PHASE") == "resume";

        public static IEnumerator Prepare(WeeklyChallengeBattleRunner runner, string saveRoot, string slot,
            string output, OfficialBattleGraphCapabilityValidation probe, Func<string, IEnumerator> capture)
        {
            Require(!Application.isEditor, "Actual application exit requires a Windows Player.");
            runner.enabled = false;
            var saved = probe.CaptureAirProcess(runner.Controller, saveRoot, slot);
            var session = OpenSession(saved);
            Require(session.TryResumeBattle(4800, out string disk, out _) && disk != saved.Checkpoint,
                "The test must distinguish a previous step save from the live partially consumed timer.");
            File.WriteAllText(Path.Combine(output, "process-disk-before-quit.json"), disk);
            File.WriteAllText(Path.Combine(output, "process-handoff.json"), JsonUtility.ToJson(saved, true));
            File.WriteAllText(Path.Combine(output, "process-checkpoint-before.json"), saved.Checkpoint);
            yield return capture("air-process-before-exit");
            File.WriteAllText(Path.Combine(output, "process-prepared.json"), "{\"passed\":true}");
            Debug.Log("[Air process] Live consumed timer observed without writing the weekly save; exiting normally.");
            Application.Quit(0);
        }

        public static IEnumerator Resume(string output, Func<string, IEnumerator> capture,
            Func<Selectable, IEnumerator> click)
        {
            Require(!Application.isEditor, "Actual application restart requires a Windows Player.");
            string path = Environment.GetEnvironmentVariable("ZJCS_AIR_PROCESS_HANDOFF");
            Require(!string.IsNullOrEmpty(path) && File.Exists(path), "Air process handoff is absent.");
            var saved = JsonUtility.FromJson<OfficialBattleGraphCapabilityValidation.AirProcessHandoff>(File.ReadAllText(path));
            string parent = Path.GetFullPath(Path.GetDirectoryName(path)).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            Require(Path.GetFullPath(saved.SaveRoot).StartsWith(parent, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(saved.SaveRoot), "Only the prepared isolated profile may be resumed.");
            Require(OpenSession(saved).TryResumeBattle(4800, out string disk, out _) && disk == saved.Checkpoint,
                "Normal quit did not flush the actual partially consumed timer to the ordinary weekly save.");
            Time.captureFramerate = 60;
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>()) camera.enabled = false;
            var home = HomeFeatureRuntime.Begin(saved.Slot, saved.SaveRoot, null);
            home.TutorialAutoStartEnabled = false;
            FightBattleCheckpoint settled = null;
            var runner = home.StartWeeklyChallengeBattle(4800, value => settled = value);
            runner.enabled = false;
            var controller = runner.Controller;
            using (var probe = new OfficialBattleGraphCapabilityValidation.AirProcessResumeProbe(controller, saved))
            {
                File.WriteAllText(Path.Combine(output, "process-checkpoint-after.json"),
                    FightBattleCheckpointSerializer.Save(controller.Checkpoint));
                // The original Animator and manual playable graph advance from the ordinary
                // battle runner. Observe its real rendered frames after re-enabling it.
                runner.enabled = true;
                float floatingDeadline = Time.realtimeSinceStartup + 30f;
                while (!probe.TryObserveVisibleFloating())
                {
                    Require(probe.AirborneStillActive && Time.realtimeSinceStartup < floatingDeadline,
                        "Resumed airborne expired before its original floating skeleton became visible.");
                    yield return new WaitForEndOfFrame();
                }
                yield return capture("air-process-restored");
                Debug.Log("[Air process] Different Windows process restored the exact quit checkpoint and original non-fresh floating view.");
                float deadline = Time.realtimeSinceStartup + 30f;
                while (!probe.Ended)
                {
                    Require(runner && Time.realtimeSinceStartup < deadline, "Resumed airborne/recovery did not end naturally.");
                    probe.ObserveFrame();
                    string name = probe.TakeCapture();
                    if (name != null) yield return capture(name);
                    yield return null;
                }
                string ended = probe.TakeCapture();
                if (ended != null) yield return capture(ended);
            }
            // Use the ordinary stage-4800 partial-settlement button and result button.
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
                "Releasing a settled battle resurrected its saved checkpoint.");
            File.WriteAllText(Path.Combine(output, "process-final.json"), FightBattleCheckpointSerializer.Save(settled));
            yield return new WaitForSecondsRealtime(.4f);
            Require(!UnityEngine.Object.FindObjectOfType<DemonResultWindow>(), "Original result remained after its simulated click.");
            yield return capture("process-return");
            File.WriteAllText(Path.Combine(output, "result.json"), "{\"passed\":true,\"error\":\"\"}");
            UnityEngine.Object.Destroy(home.gameObject); Application.Quit(0);
        }

        private static OfflineWeeklyChallengeSession OpenSession(OfficialBattleGraphCapabilityValidation.AirProcessHandoff saved)
            => new OfflineWeeklyChallengeSession(Path.Combine(saved.SaveRoot, "saves", saved.Slot, "weekly-challenge-save.json"));
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Air status process: " + message); }
    }
}
