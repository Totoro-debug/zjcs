using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator RunLeaveHosts(HomeFeatureRuntime home, string slot, string savesRoot,
            string output, Action<Selectable> click, Action<HomeFeatureRuntime> adoptHome)
        {
            Combat.BattleGameViewInput.Focus();
            yield return Combat.BattleGameViewInput.ResizePortrait(450);
            int tickets = home.Arena.Tickets, wins = home.Arena.Wins, losses = home.Arena.Losses;
            Require(home.Arena.TryBeginChallenge(0, out string error), "Arena leave fixture failed: " + error);
            Action verifyHome = CaptureHomeRestoration(home);
            var root = new GameObject("ArenaLeaveAcceptance");
            root.transform.SetParent(home.transform, false);
            int callbacks = 0;
            root.AddComponent<ArenaBattleRunner>().Begin(home, won => {
                Require(!won, "Abandoning arena awarded a victory."); callbacks++;
                root.GetComponent<ArenaBattleRunner>().LeaveResult();
            });
            var controller = root.GetComponent<OfficialBattleRuntimeController>();
            // Keep both isolated participants alive while the native confirmation is cancelled/reopened.
            foreach (var unit in controller.Checkpoint.Units)
            {
                unit.Hp = unit.MaxHp = unit.LastMaxHp = unit.Turn.Hp = 1000000000L;
            }
            yield return WaitForLeaveButton(controller, click, true);
            // Hold only the isolated battle producer while testing cancel/reopen.
            // The controller, native popup animations and EventSystem keep updating.
            root.GetComponent<ArenaBattleRunner>().enabled = false;
            yield return new WaitForSecondsRealtime(1);
            yield return Capture(output, "arena-before-leave");
            yield return ClickBattleLeave();
            yield return new WaitForSecondsRealtime(2);
            var dialog = Active<WorldConfirmDialog>().Single();
            Require(callbacks == 0 && home.Arena.HasActiveChallenge, "Arena left before confirmation.");
            Require(dialog.ContentText.GetComponent<TMPro.TMP_Text>().text == "现在退出会导致战斗失败，确认要退出战斗吗？",
                "Arena did not use the verified official leave wording.");
            yield return Capture(output, "arena-leave-confirm");
            yield return Combat.BattleGameViewInput.Click(dialog.CancelBtn);
            yield return new WaitForSecondsRealtime(2);
            Require(callbacks == 0 && home.Arena.Losses == losses && home.Arena.Tickets == tickets - 1,
                $"Cancelling arena leave changed state: callbacks={callbacks}, losses={home.Arena.Losses}/{losses}, tickets={home.Arena.Tickets}/{tickets - 1}.");
            Require(!Active<WorldConfirmDialog>().Any(), "Cancelled native dialog did not finish hiding.");
            yield return ClickBattleLeave();
            yield return new WaitForSecondsRealtime(2);
            yield return Combat.BattleGameViewInput.Click(Active<WorldConfirmDialog>().Single().ConfirmBtn);
            yield return SettleUi();
            verifyHome();
            Require(callbacks == 1 && !home.Arena.HasActiveChallenge && home.Arena.Tickets == tickets - 1
                && home.Arena.Wins == wins && home.Arena.Losses == losses + 1
                && string.IsNullOrEmpty(home.Arena.ActiveBattleCheckpoint),
                "Confirmed arena leave did not commit exactly one loss/ticket and clear its checkpoint.");
            Require(!Active<WorldConfirmDialog>().Any() && Root("OfficialBattleLeaveInputMask") == null,
                "Leaving arena leaked its native confirmation or mask.");
            yield return Capture(output, "arena-left");

            Require(home.FantasyTower.SetClearedLayer(1, 3) == FantasyTowerResult.Applied,
                "Could not unlock the isolated tower auto-push fixture.");
            var tower = new GameObject("TowerLeaveAcceptance");
            tower.transform.SetParent(home.transform, false);
            verifyHome = CaptureHomeRestoration(home);
            var runner = tower.AddComponent<FantasyTowerBattleRunner>();
            int layers = 0;
            runner.LayerCompleted += (_, __, won) => { Require(!won, "Tower abandonment won a layer."); layers++; };
            runner.Begin(home, 1);
            Require(runner.SetAutoPush(true) == FantasyTowerResult.Applied, "Could not enable tower auto push.");
            controller = tower.GetComponent<OfficialBattleRuntimeController>();
            yield return WaitForLeaveButton(controller, click);
            Require(controller.Checkpoint.StageType == 3, "Tower's official leave handler changed.");
            yield return ClickBattleLeave();
            yield return SettleUi();
            verifyHome();
            var entry = home.FantasyTower.GetEntry(1);
            Require(layers == 1 && entry.clearedLayer == 3 && !entry.autoPushEnabled
                && string.IsNullOrEmpty(entry.foregroundCheckpoint)
                && string.IsNullOrEmpty(entry.backgroundCheckpoint),
                "Tower abandonment retained its checkpoint, advanced a layer, or continued auto push.");
            Require(!Active<WorldConfirmDialog>().Any(), "Demon battlefield unexpectedly asked for confirmation.");
            yield return Capture(output, "tower-abandoned");
            home.Hud.CloseDialog();
            yield return SettleUi();

            tower = new GameObject("TowerBackgroundFailureAcceptance");
            tower.transform.SetParent(home.transform, false);
            verifyHome = CaptureHomeRestoration(home);
            runner = tower.AddComponent<FantasyTowerBattleRunner>();
            runner.Begin(home, 1);
            Require(runner.SetAutoPush(true) == FantasyTowerResult.Applied, "Could not restart auto push after abandonment.");
            controller = tower.GetComponent<OfficialBattleRuntimeController>();
            yield return WaitForLeaveButton(controller, click, true);
            // A wounded isolated fixture lets the real executor produce the background result.
            foreach (var unit in controller.Checkpoint.Units)
            {
                if (unit.Side == 1) { unit.Hp = 1; unit.Skills.Clear(); }
                else { unit.Hp = unit.MaxHp = unit.LastMaxHp = 1000000000L; }
                unit.Turn.Hp = unit.Hp;
            }
            int actions = 0;
            bool backgroundLost = false;
            controller.StepCompleted += _ => actions++;
            controller.BattleCompleted += value => backgroundLost = value.Outcome != null
                && !value.Outcome.Invalid && !value.Outcome.Won;
            Require(runner.SetBackgroundExecution(true), "Tower did not enter background execution.");
            Require(!string.IsNullOrEmpty(home.FantasyTower.GetEntry(1).backgroundCheckpoint)
                && string.IsNullOrEmpty(home.FantasyTower.GetEntry(1).foregroundCheckpoint),
                "Background transition did not transfer the checkpoint.");
            Time.timeScale = 5;
            float deadline = Time.realtimeSinceStartup + 120;
            while (home.FantasyTower.GetEntry(1).autoPushEnabled)
            {
                Require(Time.realtimeSinceStartup < deadline, "Background push did not stop after losing.");
                yield return null;
            }
            yield return SettleUi();
            verifyHome();
            entry = home.FantasyTower.GetEntry(1);
            Require(actions > 0 && backgroundLost && entry.clearedLayer == 3
                && string.IsNullOrEmpty(entry.backgroundCheckpoint)
                && entry.pendingResults.Count == 1 && !entry.pendingResults[0].won,
                "Background loss did not stop, retain the cleared layer and save one losing batch.");
            yield return Capture(output, "tower-background-failed");
            home.CloseForNavigation();
            yield return null;
            yield return null;
            home = HomeFeatureRuntime.Begin(slot, savesRoot, null);
            adoptHome(home);
            home.TutorialAutoStartEnabled = false;
            entry = home.FantasyTower.GetEntry(1);
            Require(home.Arena.Tickets == tickets - 1 && home.Arena.Losses == losses + 1
                && home.Arena.Wins == wins && !home.Arena.HasActiveChallenge
                && entry.clearedLayer == 3 && !entry.autoPushEnabled && entry.pendingResults.Count == 1
                && !entry.pendingResults[0].won && string.IsNullOrEmpty(entry.foregroundCheckpoint)
                && string.IsNullOrEmpty(entry.backgroundCheckpoint),
                "Cold reopening did not preserve arena abandonment and the stopped background batch.");
            Debug.Log("[Battle leave] native confirmation cancel/reopen, arena loss/ticket, tower abandonment, actual background failure and host restoration passed.");
        }

        private static Button BattleLeaveButton() => Active<Button>().Single(value => value.name == "CloseBtn_normal"
            && value.transform.parent.name == "CloseBtn" && value.interactable);

        private static IEnumerator ClickBattleLeave()
        {
            yield return Combat.BattleGameViewInput.Click(BattleLeaveButton());
        }

        private static IEnumerator WaitForLeaveButton(OfficialBattleRuntimeController controller,
            Action<Selectable> click, bool startBattle = false)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (true)
            {
                Require(controller != null && Time.realtimeSinceStartup < deadline, "Battle leave button did not become interactive.");
                if (Active<Button>().Any(value => value.name == "CloseBtn_normal"
                    && value.transform.parent.name == "CloseBtn" && value.interactable)
                    && (!startBattle || controller.Phase == FightBattlePhase.Play)) yield break;
                var start = Active<Button>().FirstOrDefault(value => value.name == "Btn"
                    && value.transform.parent.name == "StartBtn" && value.interactable);
                if (start != null) yield return Combat.BattleGameViewInput.Click(start);
                yield return null;
            }
        }
    }
}
