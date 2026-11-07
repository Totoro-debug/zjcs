using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator RunBodyRangeHost(HomeFeatureRuntime home, string slot,
            string savesRoot, string output, Action<Selectable> click, Action<HomeFeatureRuntime> adoptHome)
        {
            Combat.BattleGameViewInput.Focus();
            yield return Combat.BattleGameViewInput.ResizePortrait(450);
            Require(home.Profile.ApplyCharacterMutation(player =>
            {
                if (!player.Skills.Skills.Any(value => value.ClassId == 10011))
                    player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10011, Rank = 1 });
            }, "body-range-melee-fixture").IsApplied, "Could not save the melee fixture.");
            Require(home.DailyDungeonProgression.EquipSkill(10011, 0).IsApplied,
                "Could not equip the original melee skill.");
            var catalog = OfflineWeeklyChallengeBattleCatalog.LoadDefault();
            Require(catalog.TryGetBattle(4800, out var stage, out var roster, out var scene),
                "Original demon king stage is absent.");
            var checkpoint = OfficialMapBattleCheckpointBuilder.Create(new OfficialMapBattleCheckpointRequest
            {
                Stage = stage, Roster = roster, Scene = scene,
                Player = home.Profile.Save.Player, World = home.Profile.Save.World,
                Progression = home.DailyDungeonProgression, RandomSeed = 43
            });
            var main = checkpoint.Units.Single(value => value.Id == 1);
            var boss = checkpoint.Units.First(value => value.Side != main.Side);
            var melee = main.Skills.Single(value => value.ItemId == 10011);
            Require(boss.BodyOffsets.Count > 1 && main.MoveDist > 0,
                "Original battle lacks a large body or player movement.");
            var formula = OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula();
            foreach (var unit in checkpoint.Units)
            {
                unit.Level = main.Level; unit.SubRank = main.SubRank;
                unit.Hp = unit.MaxHp = unit.LastMaxHp = unit.Turn.Hp = 1000000000L;
                unit.Turn.Speed = unit.Id == 1 ? 1000 : 1;
                unit.Turn.RankSpeedScale = 1;
                foreach (var properties in new[] { unit.Properties, unit.BaseProperties })
                {
                    if (properties == null) continue;
                    SetPartialFixtureProperty(properties, formula.PropertyId("MaxHp"), 1000000000L);
                    SetPartialFixtureProperty(properties, formula.PropertyId("Attack"), unit.Id == 1 ? 1000000L : 100L);
                    SetPartialFixtureProperty(properties, formula.PropertyId("Defence"), 0);
                    SetPartialFixtureProperty(properties, formula.PropertyId("Speed"), unit.Id == 1 ? 1000L : 1L);
                }
            }
            var occupied = new HashSet<FightGridPoint>(checkpoint.Units.Where(value => value.Id != 1)
                .SelectMany(value => value.BodyOffsets.Select(offset => new FightGridPoint(value.X, value.Y) + offset)));
            var firing = FightBattleTargeting.CastingPositions(boss, melee.Range)
                .Where(point => main.BodyOffsets.All(offset => checkpoint.Grid.CanStand(point + offset, main.MoveMask, occupied)))
                .Where(point => !melee.Range.Any(offset => (point + offset).Equals(new FightGridPoint(boss.X, boss.Y))))
                .ToArray();
            List<FightGridPoint> selectedPath = null;
            FightGridPoint start = default;
            for (int x = 0; x < checkpoint.Grid.Width && selectedPath == null; x++)
                for (int y = 0; y < checkpoint.Grid.Height && selectedPath == null; y++)
                {
                    var point = new FightGridPoint(x, y);
                    if (!main.BodyOffsets.All(offset => checkpoint.Grid.CanStand(point + offset, main.MoveMask, occupied))) continue;
                    if (firing.Contains(point)) continue;
                    foreach (var destination in firing)
                    {
                        var path = checkpoint.Grid.FindPath(point, destination, main.MoveMask,
                            main.MoveDist + 2, 0, true, main.BodyOffsets, occupied);
                        if (path == null || path.Count < 2 || path.Count - 1 > main.MoveDist
                            || !path[path.Count - 1].Equals(destination)) continue;
                        selectedPath = path; start = point; break;
                    }
                }
            Require(selectedPath != null, "No legal original-grid approach to a boss body edge.");
            main.X = start.X; main.Y = start.Y;
            File.WriteAllText(Path.Combine(output, "body-range-initial.json"),
                FightBattleCheckpointSerializer.Save(checkpoint));
            string weeklyPath = Path.Combine(savesRoot, "saves", slot, "weekly-challenge-save.json");
            var session = new OfflineWeeklyChallengeSession(weeklyPath);
            session.StartBattle(4800, session.CurrentPeriodStartLocalTicks(DateTime.Now),
                FightBattleCheckpointSerializer.Save(checkpoint));
            var weekly = home.StartWeeklyChallengeBattle(4800, _ => { });
            var controller = weekly.Controller;
            FightBattleExecutionResult hit = null;
            controller.StepCompleted += value =>
            {
                if (value.Event.ActorId != 1) return;
                File.WriteAllText(Path.Combine(output, "body-range-step.json"), JsonUtility.ToJson(value.Event, true));
                File.WriteAllText(Path.Combine(output, "body-range-after.json"),
                    FightBattleCheckpointSerializer.Save(value.Checkpoint));
                Require(value.Event.SkillCasts.Any(cast => cast.SkillItemId == 10011), "Player skipped the original melee skill.");
                Require(value.Event.DamageEvents.Any(damage => damage.SourceId == 1 && damage.TargetId == boss.Id
                    && damage.BeforeHp > damage.AfterHp), "Original melee did not damage the boss body edge.");
                Require(value.Event.Movements.Any(movement => movement.UnitId == 1), "Original melee did not approach the body edge.");
                hit = value;
                weekly.enabled = false;
            };
            yield return Capture(output, "body-range-preparation");
            yield return WaitForLeaveButton(controller, click, true);
            float deadline = Time.realtimeSinceStartup + 60;
            while (hit == null)
            {
                Require(Time.realtimeSinceStartup < deadline && !controller.Checkpoint.Finished,
                    "Original melee body-edge action did not run.");
                yield return null;
            }
            Require(hit.Event.DamageEvents.Count(value => value.SourceId == 1 && value.TargetId == boss.Id
                && value.SkillItemId == 10011)
                == FightBattleHitCollector.CollectAll(melee).Count(value => value.DamageId > 0),
                "Boss body cells multiplied the original hit count.");
            var model = controller.TryGetUnitTransform(1);
            Require(model != null, "Player presentation transform is absent.");
            var approach = hit.Event.Movements.First(value => value.UnitId == 1);
            float approachSeconds = (approach.Path.Count - 1) * approach.MoveDuration;
            Require(approach.MoveDuration > 0f && approachSeconds > 0f,
                "Original role movement lost its configured per-cell duration.");
            var meleeCast = hit.Event.SkillCasts.Single(value => value.ActorId == 1 && value.SkillItemId == 10011);
            Require(meleeCast.Delay + .0001f >= approach.Delay + approachSeconds,
                "Original melee starts before the approach lands.");
            Require(hit.Event.DamageEvents.Where(value => value.SourceId == 1 && value.SkillItemId == 10011)
                .All(value => value.Delay + .0001f >= meleeCast.Delay),
                "Original melee damage precedes the landed cast.");
            var samples = new List<Vector3>();
            Vector3? castStartPosition = null;
            deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsPresentingStep)
            {
                Require(Time.realtimeSinceStartup < deadline, "Original movement/skill presentation did not complete.");
                samples.Add(model.position);
                if (samples.Count == 3) yield return Capture(output, "body-range-moving");
                if (!castStartPosition.HasValue && GameObject.Find(melee.EntityId.ToString()) != null)
                {
                    castStartPosition = model.position;
                    yield return new WaitForSeconds(.15f);
                    yield return Capture(output, "body-range-casting");
                }
                yield return null;
            }
            Require(samples.Count > 2 && samples.Any(position => Vector3.Distance(position, model.position) > .05f),
                "Approach changed logic coordinates without playing movement.");
            Require(castStartPosition.HasValue && Vector3.Distance(castStartPosition.Value, model.position) < .01f,
                "The original skill entity was created before the player reached its landing position.");
            main = controller.Checkpoint.Units.Single(value => value.Id == 1);
            Require(!melee.Range.Any(offset => (new FightGridPoint(main.X, main.Y) + offset)
                .Equals(new FightGridPoint(boss.X, boss.Y))), "Fixture reached the boss centre instead of testing its edge.");
            yield return Capture(output, "body-range-melee-hit");
            int counter = controller.Checkpoint.Counter;
            long hp = controller.Checkpoint.Units.Single(value => value.Id == boss.Id).Hp;
            string skillState = JsonUtility.ToJson(main.SkillState);
            UnityEngine.Object.Destroy(weekly);
            yield return null;
            home.CloseForNavigation(); yield return null; yield return null;
            home = HomeFeatureRuntime.Begin(slot, savesRoot, null); adoptHome(home);
            home.TutorialAutoStartEnabled = false;
            FightBattleCheckpoint settled = null;
            FightBattleReport finalReport = null;
            OfflineWeeklyChallengeSession.ChallengeAward award = null;
            weekly = home.StartWeeklyChallengeBattle(4800, value => {
                settled = value; finalReport = weekly.FinalReport; award = weekly.FinalChallengeAward;
            }); weekly.enabled = false;
            controller = weekly.Controller;
            main = controller.Checkpoint.Units.Single(value => value.Id == 1);
            Require(controller.Checkpoint.Counter == counter && controller.Checkpoint.Units.Single(value => value.Id == boss.Id).Hp == hp
                && JsonUtility.ToJson(main.SkillState) == skillState,
                "Cold body-edge resume changed actions, damage or cooldown state.");
            yield return WaitForLeaveButton(controller, click);
            // The original control can receive input before its Show animation reaches full alpha.
            yield return new WaitForSecondsRealtime(2f);
            var leave = Active<Button>().Single(value => value.name == "CloseBtn_normal"
                && value.transform.parent.name == "CloseBtn" && value.interactable);
            Require(leave.GetComponentsInParent<CanvasGroup>().All(group => group.alpha >= .99f),
                "Restored battle controls did not finish their original Show animation.");
            Require(controller.Checkpoint.Counter == counter, "Waiting for the restored HUD advanced combat.");
            yield return Capture(output, "body-range-cold-resume");
            if (Environment.GetEnvironmentVariable("ZJCS_DEMON_RESULT_PANEL") == "1")
            {
                yield return ClickBattleLeave();
                yield return new WaitForSecondsRealtime(2);
                yield return Combat.BattleGameViewInput.Click(Active<Zjcs.Official.WorldUI.WorldConfirmDialog>().Single().ConfirmBtn);
                deadline = Time.realtimeSinceStartup + 30;
                while (settled == null)
                {
                    Require(Time.realtimeSinceStartup < deadline, "The restored melee battle did not settle.");
                    yield return null;
                }
                Require(finalReport != null && finalReport.Units.Any(value => value.Side == 1 && value.DamageDealt > 0),
                    "The resumed melee settlement lost its actual damage.");
                yield return ExerciseDemonResultPanel(home, settled, finalReport, output, award);
            }
            else { UnityEngine.Object.Destroy(weekly); yield return null; }
            Debug.Log("[Battle body range] original 10011/4800 body-edge approach, movement playback, melee damage and cold resume passed.");
        }
    }
}
