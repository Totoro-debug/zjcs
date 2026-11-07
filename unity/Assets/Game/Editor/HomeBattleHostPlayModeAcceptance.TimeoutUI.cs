using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Official.BattleResultUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator RunTimeoutUiHost(HomeFeatureRuntime home, string slot,
            string savesRoot, string output, Action<Selectable> click)
        {
            Combat.BattleGameViewInput.Focus();
            yield return Combat.BattleGameViewInput.ResizePortrait(450);
            var catalog = OfflineWeeklyChallengeBattleCatalog.LoadDefault();
            Require(catalog.TryGetBattle(4800, out var stage, out var roster, out var scene),
                "Original demon timeout fixture stage is absent.");
            long initialItems = home.ItemCount(41021);
            for (int kind = 0; kind < 2; kind++)
            {
                // Isolated checkpoints shorten the limits, not the executor or
                // result callback. Both outcomes must occur through real turns.
                var state = OfficialMapBattleCheckpointBuilder.Create(new OfficialMapBattleCheckpointRequest {
                    Stage = stage, Roster = roster, Scene = scene, Player = home.Profile.Save.Player,
                    World = home.Profile.Save.World, Progression = home.DailyDungeonProgression, RandomSeed = 43
                });
                state.MaxRound = kind == 0 ? 1 : 100;
                state.IsPlayerRoundLimit = kind == 1;
                state.PlayerRoundLimit = kind == 1 ? 1 : 0;
                var formula = OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula();
                foreach (var unit in state.Units)
                {
                    unit.Hp = unit.MaxHp = unit.LastMaxHp = unit.Turn.Hp = 1000000000L;
                    unit.Turn.Speed = 1000; unit.Turn.RankSpeedScale = 1;
                    foreach (var properties in new[] { unit.Properties, unit.BaseProperties })
                        if (properties != null)
                        {
                            SetPartialFixtureProperty(properties, formula.PropertyId("MaxHp"), unit.MaxHp);
                            SetPartialFixtureProperty(properties, formula.PropertyId("Speed"), 1000);
                        }
                }
                string weeklyPath = Path.Combine(savesRoot, "saves", slot, "weekly-challenge-save.json");
                var session = new OfflineWeeklyChallengeSession(weeklyPath);
                session.StartBattle(4800, session.CurrentPeriodStartLocalTicks(DateTime.Now),
                    FightBattleCheckpointSerializer.Save(state));
                FightBattleCheckpoint completed = null;
                var restoreHome = CaptureHomeRestoration(home);
                var runner = home.StartWeeklyChallengeBattle(4800, value => completed = value);
                yield return WaitForLeaveButton(runner.Controller, click, true);
                float deadline = Time.realtimeSinceStartup + 160;
                while (completed == null)
                {
                    Require(Time.realtimeSinceStartup < deadline, "The real battle did not exhaust its round limit.");
                    yield return null;
                }
                Require(completed.Outcome.Reason == (kind == 0 ? 3 : 4) && !completed.PartEnded
                    && !completed.Outcome.Won, "The real battle did not produce the selected round-limit result.");
                var view = UnityEngine.Object.FindObjectOfType<DemonResultWindow>();
                Require(view && view.TimeoutTip && view.TimeoutTip.Content.activeInHierarchy && !view.Panel.activeSelf,
                    "Round-limit settlement skipped the original visible timeout wait.");
                var tip = view.TimeoutTip;
                Require(tip.StateSwitcher.State == GameTimeoutUI.EStatus.Normal
                    && tip.TipsText.myKey == "game_end_roundlimit" && tip.CountdownText.Text == "2",
                    "The native round-limit hint did not start at its original two-second countdown.");
                foreach (var text in tip.GetComponentsInChildren<TMP_Text>())
                    Require(text.font && text.font.HasCharacters(text.text, out _, false, true),
                        "The original timeout hint is missing localized glyphs.");
                var rect = (RectTransform)tip.Content.transform;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var lower = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var upper = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                Require(upper.x > lower.x && upper.y > lower.y && lower.x >= -1 && upper.x <= Screen.width + 1
                    && lower.y >= 0 && upper.y <= Screen.height,
                    "The original timeout SafeArea/ancestor geometry is outside the Game View.");
                float start = Time.realtimeSinceStartup;
                int notifications = 0;
                tip.OnTimeOut += () => notifications++;
                yield return Capture(output, "timeout-round-" + kind + "-2");
                while (tip.CountdownText.Text == "2")
                {
                    Require(Time.realtimeSinceStartup < start + 5, "Original countdown did not advance in real time.");
                    yield return null;
                }
                Require(tip.CountdownText.Text == "1" && !view.Panel.activeSelf,
                    "The native result appeared before the original final countdown tick.");
                yield return Capture(output, "timeout-round-" + kind + "-1");
                deadline = Time.realtimeSinceStartup + 25;
                while (!view.IsReady)
                {
                    Require(Time.realtimeSinceStartup < deadline, "The result did not follow the original timeout wait.");
                    yield return null;
                }
                Require(notifications == 1 && tip.Content.activeInHierarchy && runner.Controller.HasBattleEnvironment,
                    "The original timeout completion/lifetime differs before native leave.");
                yield return ExerciseDemonResultPanel(home, completed, runner.FinalReport, output, runner.FinalChallengeAward);
                restoreHome();
                Require(!tip && home.ItemCount(41021) == initialItems + 300,
                    "Native leave leaked the timeout UI or duplicated its challenge award.");
            }
            Debug.Log("[Battle timeout UI] Real global/player round limits, native SafeArea and localized 2/1 countdown, one timeout event, original result wait and simulated native leave passed.");
        }
    }
}
