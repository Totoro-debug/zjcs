using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator RunArenaResultHost(HomeFeatureRuntime home, string slot, string root,
            string output, Action<Selectable> click)
        {
            Combat.BattleGameViewInput.Focus();
            yield return Combat.BattleGameViewInput.ResizePortrait(450);
            yield return OpenTravel(home, click);
            // Keep the official guard's skills and appearance. Only isolated
            // opponent selection/HP/properties shorten these real UI battles.
            var guard = home.Arena.Catalog.runtime.guards.OrderBy(value => value.id).First();
            home.Arena.Save.opponents[0].guardTemplateId = guard.id;
            home.Arena.Save.opponents[0].rank = Math.Max(1, home.Arena.Rank - 10);
            click(ButtonUnder("ArenaAreaEntry", "EntryBtn")); yield return SettleUi();
            var formula = OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula();
            for (int kind = 0; kind < 4; kind++)
            {
                home.Arena.Save.opponents[0].guardTemplateId = guard.id;
                home.Arena.Save.opponents[0].rank = Math.Max(1, home.Arena.Rank - 10);
                int tickets = home.Arena.Tickets, wins = home.Arena.Wins, losses = home.Arena.Losses;
                long coins = home.ItemCount(home.Arena.Catalog.settings.CoinId);
                Action restored = CaptureHomeRestoration(home);
                click(ButtonUnder("OpponentItem", "FightBtn")); yield return SettleUi();
                if (Root("ArenaConfirm"))
                {
                    var choice = Root("ArenaConfirm").GetComponent<WorldConfirmDialog>();
                    click(kind == 3 ? choice.ConfirmBtn : choice.CancelBtn);
                    yield return SettleUi();
                }
                ArenaBattleRunner runner = null;
                OfficialBattleRuntimeController controller = null;
                if (kind != 3)
                {
                    runner = UnityEngine.Object.FindObjectOfType<ArenaBattleRunner>();
                    Require(runner, "Actual arena challenge did not start its runner.");
                    controller = runner.GetComponent<OfficialBattleRuntimeController>();
                    foreach (var unit in controller.Checkpoint.Units)
                    {
                        bool loser = kind == 0 ? unit.Id == 2 : kind == 1 && unit.Id == 1;
                        unit.Hp = unit.MaxHp = unit.LastMaxHp = unit.Turn.Hp = loser ? 1 : 1000000000L;
                        unit.Turn.Speed = (kind == 1 ? unit.Id == 2 : unit.Id == 1) ? 1000 : 1;
                        unit.Turn.RankSpeedScale = 1;
                        foreach (var properties in new[] { unit.Properties, unit.BaseProperties })
                        {
                            SetPartialFixtureProperty(properties, formula.PropertyId("MaxHp"), unit.MaxHp);
                            SetPartialFixtureProperty(properties, formula.PropertyId("Attack"), 1000000);
                            SetPartialFixtureProperty(properties, formula.PropertyId("Defence"), 0);
                            SetPartialFixtureProperty(properties, formula.PropertyId("Speed"), unit.Turn.Speed);
                        }
                    }
                    yield return WaitForLeaveButton(controller, click, true);
                    if (kind == 2)
                    {
                        float deadline = Time.realtimeSinceStartup + 40;
                        while (!controller.IsPresentingStep)
                        {
                            Require(Time.realtimeSinceStartup < deadline, "Arena did not begin a real action presentation.");
                            yield return null;
                        }
                        yield return ClickBattleLeave(); yield return new WaitForSecondsRealtime(1);
                        var confirm = Active<WorldConfirmDialog>().Single();
                        yield return Combat.BattleGameViewInput.Click(confirm.CancelBtn);
                        yield return new WaitForSecondsRealtime(1);
                        Require(!controller.Checkpoint.Finished && home.Arena.HasActiveChallenge,
                            "Cancelling arena leave settled or ended the real battle.");
                        yield return ClickBattleLeave(); yield return new WaitForSecondsRealtime(1);
                        yield return Combat.BattleGameViewInput.Click(Active<WorldConfirmDialog>().Single().ConfirmBtn);
                    }
                }
                float resultDeadline = Time.realtimeSinceStartup + 160;
                ArenaResultWindow window;
                while (!(window = UnityEngine.Object.FindObjectOfType<ArenaResultWindow>()) || !window.IsReady)
                {
                    Require(Time.realtimeSinceStartup < resultDeadline, "Actual arena completion did not show the native result.");
                    yield return null;
                }
                bool won = kind == 0 || kind == 3;
                var native = window.Panel.GetComponent<GameEndPanelArena>();
                var actions = window.Panel.GetComponent<GameEndPanelBase>();
                var settled = home.Arena.LastResult;
                Require(!string.IsNullOrWhiteSpace(window.Opponent.Name)
                    && !window.Opponent.Name.StartsWith("arena_guard_name_", StringComparison.Ordinal),
                    "The original guard name was displayed as an unresolved language key.");
                Require(settled != null && settled.won == won && settled.fastVictory == (kind == 3)
                    && !home.Arena.HasActiveChallenge && home.Arena.Tickets == tickets - 1
                    && home.Arena.Wins == wins + (won ? 1 : 0) && home.Arena.Losses == losses + (won ? 0 : 1),
                    "Arena native result differs from the one committed challenge/ticket transaction.");
                Require(native.WinEffect.activeSelf == won && native.LoseEffect.activeSelf != won,
                    "The original arena win/lose branches are reversed.");
                Require((won ? native.WinTitleText : native.LoseTitleText).myKey == (won ? "ui_arena_win_text" : "ui_arena_lose_text"),
                    "The arena title did not use the official own-attacker branch.");
                Require(window.Settlement.PreviousRank == settled.previousRank && window.Settlement.CurrentRank == settled.currentRank
                    && window.Settlement.AwardCount == settled.awardCount
                    && home.ItemCount(settled.awardItemId) == coins + settled.awardCount,
                    "Arena rank/award display was inferred instead of using the committed result.");
                var disk = OfflineArenaSession.Open(new ProfileSlotCatalog(root), slot);
                Require(JsonUtility.ToJson(disk.LastResult) == JsonUtility.ToJson(settled)
                    && !disk.CompleteChallenge(won, home.GrantBattleReward), "Arena disk result was lost or granted twice.");
                foreach (var text in window.Panel.GetComponentsInChildren<TMP_Text>())
                    Require(text.font && text.font.HasCharacters(text.text, out _, false, true),
                        "Original arena result is missing displayed glyphs: " + text.name);
                if (won)
                {
                    var portraits = window.Panel.GetComponentsInChildren<PlayerInfoUI>();
                    Require(portraits.Length == 2 && portraits.Any(value => value.PlayerInfoData.Id == window.Opponent.Id)
                        && portraits.All(value => value.PlayerAppearance), "Original result lost its native player/guard appearance bindings.");
                }
                yield return Capture(output, "arena-result-" + kind);
                if (kind != 3)
                {
                    Require(runner && controller.HasBattleEnvironment && runner.FinalReport != null
                        && runner.FinalCheckpoint.Finished, "Arena disposed its battle before native leave/report.");
                    if (kind == 2) Require(!runner.FinalCheckpoint.PartEnded && runner.FinalCheckpoint.Outcome.Reason == 2
                        && runner.FinalCheckpoint.Units.All(value => value.Hp > 0), "Arena forfeit fabricated dead units or a partial reward result.");
                    var common = window.Panel.GetComponent<OfficialBattleResultReport>();
                    Require(common && JsonUtility.ToJson(common.FinalReport) == JsonUtility.ToJson(runner.FinalReport),
                        "Native arena result replaced the actual final statistics.");
                    string snapshot = FightBattleCheckpointSerializer.Save(runner.FinalCheckpoint);
                    for (int open = 0; open < 2; open++)
                    {
                        yield return Combat.BattleGameViewInput.Click(actions.ReportButton);
                        yield return new WaitForSecondsRealtime(2);
                        var report = Root("FightEndReportPanel") ?? Root("FightEndAllPlayerReportPanel");
                        Require(report && common.IsOpen, "Native arena statistics button did not open its report.");
                        yield return Capture(output, "arena-result-report-" + kind + "-" + open);
                        yield return Combat.BattleGameViewInput.Click(report.GetComponentsInChildren<Button>()
                            .Single(value => value.name == "CloseBtn"));
                        yield return new WaitForSecondsRealtime(1);
                    }
                    Require(FightBattleCheckpointSerializer.Save(runner.FinalCheckpoint) == snapshot
                        && home.ItemCount(settled.awardItemId) == coins + settled.awardCount,
                        "Viewing arena statistics advanced the battle or granted another reward.");
                }
                else Require(!actions.ReportButton.gameObject.activeSelf && !runner,
                    "Direct arena victory fabricated combat statistics or a battle runner.");
                yield return Combat.BattleGameViewInput.Click(native.LeaveBtn);
                yield return new WaitForSecondsRealtime(1);
                Require(!window && !runner && !Root("BattleResultReportLifetime"), "Native arena leave leaked its retained battle/window.");
                restored();
                yield return Capture(output, "arena-result-return-" + kind);
            }
            Debug.Log("[Arena result] Actual UI challenge, victory/defeat/confirmed forfeit/direct win, native portraits/rank/reward, durable settlement, twice-opened statistics, simulated native leave and retained battle passed.");
        }
    }
}
