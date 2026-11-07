using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator RunPartialLeaveHosts(HomeFeatureRuntime home, string slot,
            string savesRoot, string output, Action<Selectable> click, Action<HomeFeatureRuntime> adoptHome)
        {
            Combat.BattleGameViewInput.Focus();
            yield return Combat.BattleGameViewInput.ResizePortrait(450);
            Require(home.Profile.ApplyCharacterMutation(player =>
            {
                if (!player.Skills.Skills.Any(value => value.ClassId == 10018))
                    player.Skills.Skills.Add(new Zjcs.Offline.Skills.OfflineOwnedSkill { ClassId = 10018, Rank = 1 });
            }, "partial-leave-offensive-skill-fixture").IsApplied, "Could not save the offensive skill fixture.");
            Require(home.DailyDungeonProgression.EquipSkill(10018, 0).IsApplied,
                "Could not equip the original profession's offensive skill fixture.");
            var dungeon = home.DailyDungeons.PublicDungeons((_, __) => true).First();
            Require(home.DailyDungeons.TryBegin(dungeon.id, 0, _ => true, (_, __) => true,
                (_, __, ___) => true, home.DailyDungeons.Catalog.settings.DailyDungeonBuyCostItemId,
                DateTimeOffset.Now.ToUnixTimeSeconds(), out string error), error);
            // Isolated saved route: two cleared rooms, then an uncleared combat room.
            for (int index = 0; index < 2; index++)
            {
                var room = home.DailyDungeons.FindRoom(home.DailyDungeons.ActiveRun.currentRoomId);
                Require(home.DailyDungeons.RecordRoomCleared(room.id, string.Empty)
                    && home.DailyDungeons.ChooseNextRoom(room.nextRoomIds.First()), "Could not prepare the partial route.");
            }
            int grade = home.DailyDungeons.CurrentGrade();
            int[] cleared = home.DailyDungeons.ActiveRun.clearedRoomIds.ToArray();
            int current = home.DailyDungeons.ActiveRun.currentRoomId;
            int callbacks = 0, reports = 0;
            Action verifyHome = CaptureHomeRestoration(home);
            var root = new GameObject("DailyPartialLeaveAcceptance");
            root.transform.SetParent(home.transform, false);
            var daily = root.AddComponent<DailyDungeonBattleRunner>();
            daily.Begin(home, current, "daily-dungeon:" + current, won =>
            {
                Require(!won && reports == 1, "Daily partial exit did not hand off its report first."); callbacks++;
            });
            daily.enabled = false;
            var controller = root.GetComponent<OfficialBattleRuntimeController>();
            controller.BattleReportReady += _ => reports++;
            yield return WaitForLeaveButton(controller, click);
            yield return ClickBattleLeave();
            yield return new WaitForSecondsRealtime(2);
            var dialog = Active<WorldConfirmDialog>().Single();
            Require(dialog.ContentText.GetComponent<TMPro.TMP_Text>().text.Contains("按已通过行数"),
                "Daily confirmation still says the confirmed grade is forfeited.");
            yield return Capture(output, "daily-partial-confirm");
            // Simulate interruption after the native confirmation saved its partial
            // checkpoint, before the controller's next Update delivers the result.
            controller.enabled = false;
            yield return Combat.BattleGameViewInput.Click(dialog.ConfirmBtn);
            yield return SettleUi();
            Require(callbacks == 0 && FightBattleCheckpointSerializer.Load(
                home.DailyDungeons.ActiveRun.activeBattleCheckpoint).PartEnded,
                "Daily confirmed exit was not durable before result handoff.");
            UnityEngine.Object.Destroy(root);
            yield return null;
            yield return null;
            verifyHome();
            home.CloseForNavigation();
            yield return null;
            yield return null;
            home = HomeFeatureRuntime.Begin(slot, savesRoot, null);
            adoptHome(home);
            home.TutorialAutoStartEnabled = false;
            verifyHome = CaptureHomeRestoration(home);
            root = new GameObject("DailyPartialLeaveColdRestoreAcceptance");
            root.transform.SetParent(home.transform, false);
            daily = root.AddComponent<DailyDungeonBattleRunner>();
            daily.Begin(home, current, "daily-dungeon:" + current, won =>
            {
                Require(!won && reports == 1, "Restored daily partial exit lost report ordering."); callbacks++;
            });
            controller = root.GetComponent<OfficialBattleRuntimeController>();
            controller.BattleReportReady += _ => reports++;
            yield return SettleUi();
            verifyHome();
            var pending = home.DailyDungeons.PendingClaims.Single();
            Require(callbacks == 1 && home.DailyDungeons.ActiveRun == null && pending.run.grade == grade
                && pending.run.clearedRoomIds.SequenceEqual(cleared) && !pending.run.clearedRoomIds.Contains(current),
                "Daily exit lost its current grade or awarded the uncleared room.");
            yield return Capture(output, "daily-partial-returned");

            string weeklyPath = Path.Combine(savesRoot, "saves", slot, "weekly-challenge-save.json");
            FightBattleCheckpoint completed = null;
            FightBattleReport report = null;
            verifyHome = CaptureHomeRestoration(home);
            var weekly = home.StartWeeklyChallengeBattle(4800, checkpoint =>
            {
                Require(report != null, "Chaos partial result arrived before the common report."); completed = checkpoint;
            });
            controller = weekly.Controller;
            controller.BattleReportReady += value => report = value;
            // Keep the original skills, hit definitions and damage formula. Only the
            // isolated roster's HP/attack/defence and level gap are controlled so a
            // nonzero hit can be observed before either participant ends the battle.
            var formula = OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula();
            var main = controller.Checkpoint.Units.Single(value => value.Id == 1);
            foreach (var unit in controller.Checkpoint.Units)
            {
                unit.Level = main.Level;
                unit.SubRank = main.SubRank;
                unit.Turn.Speed = 100;
                unit.Turn.RankSpeedScale = 1;
                unit.Hp = unit.MaxHp = unit.LastMaxHp = unit.Turn.Hp = 1000000000L;
                foreach (var properties in new[] { unit.Properties, unit.BaseProperties })
                {
                    if (properties == null) continue;
                    SetPartialFixtureProperty(properties, formula.PropertyId("MaxHp"), 1000000000L);
                    SetPartialFixtureProperty(properties, formula.PropertyId("Attack"), unit.Side == 1 ? 1000000L : 100L);
                    SetPartialFixtureProperty(properties, formula.PropertyId("Defence"), 0);
                    SetPartialFixtureProperty(properties, formula.PropertyId("Speed"), 100);
                }
            }
            Require(controller.Checkpoint.Counter == 0 && main.Skills.Count > 0,
                "Partial fixture must be initialized before its first real step, with original player skills.");
            var enemy = controller.Checkpoint.Units.First(value => value.Side != 1);
            var occupied = new System.Collections.Generic.HashSet<FightGridPoint>(controller.Checkpoint.Units
                .Where(value => value.Id != main.Id).SelectMany(value => value.BodyOffsets.Select(
                    offset => new FightGridPoint(value.X + offset.X, value.Y + offset.Y))));
            var firingTiles = main.Skills.SelectMany(skill => skill.Range)
                .Select(range => new FightGridPoint(enemy.X - range.X, enemy.Y - range.Y))
                .Where(point => controller.Checkpoint.Grid.CanStand(point, main.MoveMask, occupied)).ToArray();
            Require(firingTiles.Length > 0, "No legal tile in the original player skill range for the partial fixture.");
            main.X = firingTiles[0].X;
            main.Y = firingTiles[0].Y;
            Require(main.Skills.Any(skill => FightBattleTargeting.SelectInSkillRange(main,
                controller.Checkpoint.Units, skill).Count > 0),
                "Original ranged skill has no target from the prepared legal tile.");
            new FightTurnQueue(controller.Checkpoint.Timeline,
                controller.Checkpoint.Units.Select(value => value.Turn)).Initialize();
            Debug.Log("Chaos partial fixture: player=" + main.X + ":" + main.Y + ", enemy=" + enemy.X + ":" + enemy.Y
                + ", player skills=" + string.Join(",", main.Skills.Select(value => value.EntityId + ":target=" + value.TargetType)));
            yield return WaitForLeaveButton(controller, click, true);
            float deadline = Time.realtimeSinceStartup + 60;
            while (!controller.Checkpoint.Statistics.Units.Any(value => value.DamageDealt > 0
                && controller.Checkpoint.Units.Any(unit => unit.Id == value.UnitId && (unit.Id == 1 || unit.MasterId == 1))))
            {
                Require(controller != null && !controller.Checkpoint.Finished && Time.realtimeSinceStartup < deadline,
                    "Chaos fixture did not produce player damage: " + (controller == null ? "disposed" :
                    "finished=" + controller.Checkpoint.Finished + ", counter=" + controller.Checkpoint.Counter
                    + ", player=" + main.X + ":" + main.Y + ", skills=" + main.Skills.Count
                    + ", speed=" + main.Turn.Speed
                    + ", stats=" + JsonUtility.ToJson(controller.Checkpoint.Statistics)));
                yield return null;
            }
            weekly.enabled = false;
            int counter = controller.Checkpoint.Counter;
            var session = new OfflineWeeklyChallengeSession(weeklyPath);
            Require(session.TryResumeBattle(4800, out _, out long period), "Chaos checkpoint was not saved.");
            long previousDamage = session.DemonKingDamage(period);
            yield return ClickBattleLeave();
            yield return new WaitForSecondsRealtime(2);
            yield return Capture(output, "chaos-partial-confirm");
            yield return Combat.BattleGameViewInput.Click(Active<WorldConfirmDialog>().Single().ConfirmBtn);
            deadline = Time.realtimeSinceStartup + 30;
            while (completed == null)
            {
                Require(Time.realtimeSinceStartup < deadline, "Chaos partial presentation did not finish.");
                yield return null;
            }
            yield return SettleUi();
            Require(completed != null && completed.PartEnded && completed.Counter == counter
                && !completed.Outcome.Won && completed.Outcome.CanSettle,
                "Chaos partial exit advanced actions or fabricated victory/invalidity.");
            var groups = FightBattleReportGrouping.Build(report, 1);
            var player = groups.Players.Single(value => value.TargetId == 1);
            long damage = player.Normal.Concat(player.Summon).Concat(player.Pet)
                .Sum(value => Math.Max(0, value.DamageDealt));
            session = new OfflineWeeklyChallengeSession(weeklyPath);
            Require(damage > 0 && session.DemonKingDamage(period) == previousDamage + damage
                && !session.TryResumeBattle(4800, out _, out _),
                "Chaos partial exit lost actual damage or retained a resumable battle.");
            yield return ExerciseDemonResultPanel(home, completed, report, output, weekly.FinalChallengeAward);
            verifyHome();
            yield return Capture(output, "chaos-partial-returned");
            Debug.Log("[Battle partial leave] daily native confirmation/current grade and chaos actual damage/report-before-result/cold persistence passed.");
        }

        private static void SetPartialFixtureProperty(System.Collections.Generic.List<FightPropertyValue> properties,
            int id, long amount)
        {
            Require(id > 0, "Official damage property is absent from the selected catalog.");
            var value = properties.Find(property => property.Id == id);
            if (value == null) properties.Add(new FightPropertyValue { Id = id, Value = amount });
            else value.Value = amount;
        }
    }
}
