using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    public static class SkillGridBattleValidation
    {
        public static IEnumerator Exercise(HomeFeatureRuntime home, string slot, string savesRoot,
            string output, Func<Selectable, IEnumerator> click, Func<string, IEnumerator> capture,
            Action<HomeFeatureRuntime> adoptHome)
        {
            var catalog = OfflineWeeklyChallengeBattleCatalog.LoadDefault();
            Require(catalog.TryGetBattle(4800, out var stage, out var roster, out var scene),
                "Original weekly stage 4800 is absent.");
            var checkpoint = OfficialMapBattleCheckpointBuilder.Create(new OfficialMapBattleCheckpointRequest
            {
                Stage = stage, Roster = roster, Scene = scene,
                Player = home.Profile.Save.Player, World = home.Profile.Save.World,
                Progression = home.DailyDungeonProgression, RandomSeed = 43
            });
            var finish = OfficialBattleGridLifecycleValidation.Configure(checkpoint);
            int bossId = checkpoint.Units.First(value => value.Side != 1).Id;
            Require(checkpoint.Units.Single(value => value.Id == bossId).BodyOffsets.Count > 1,
                "Original weekly boss does not have a large body.");
            string weeklyPath = Path.Combine(savesRoot, "saves", slot, "weekly-challenge-save.json");
            var session = new OfflineWeeklyChallengeSession(weeklyPath);
            session.StartBattle(4800, session.CurrentPeriodStartLocalTicks(DateTime.Now),
                FightBattleCheckpointSerializer.Save(checkpoint));
            var runner = home.StartWeeklyChallengeBattle(4800, _ => { });
            var controller = runner.Controller;
            FightBattleExecutionResult creation = null;
            controller.StepCompleted += value =>
            {
                if (!value.Event.GridViewEvents.Any(e => e.Kind == FightBattleGridViewEventKind.Create)) return;
                creation = value; runner.enabled = false;
            };
            yield return WaitForHud(home);
            yield return capture("grid-preparation");
            yield return Start(controller, click);
            float deadline = Time.realtimeSinceStartup + 60f;
            while (creation == null)
            {
                Require(Time.realtimeSinceStartup < deadline && !controller.Checkpoint.Finished,
                    "Original 13050 did not run through the public weekly battle.");
                yield return null;
            }
            var created = creation.Event.GridViewEvents.Where(e => e.Kind == FightBattleGridViewEventKind.Create).ToArray();
            int[] ids = created.Select(e => e.GridId).ToArray();
            Require(ids.Distinct().Count() == 9 && creation.Event.DamageEvents.Count(d => ids.Contains(d.SourceId)
                && d.TargetId == bossId && d.DamageValue < 0) == 9,
                "Nine burning cells did not separately damage the same large boss.");
            Require(created.All(e => e.Delay >= .779f) && ids.All(id => controller.TryGetUnitTransform(id) == null),
                "Native grids appeared before their configured creation delay.");
            File.WriteAllText(Path.Combine(output, "grid-created-step.json"), JsonUtility.ToJson(creation.Event, true));
            yield return capture("grid-before-creation");
            deadline = Time.realtimeSinceStartup + 30f;
            while (ids.Any(id => controller.TryGetUnitTransform(id) == null))
            { Require(Time.realtimeSinceStartup < deadline, "Native grid creation timed out."); yield return null; }
            yield return new WaitForSeconds(.15f);
            OfficialBattleGridLifecycleValidation.RequireActive(controller, ids);
            yield return capture("grid-nine-fires");
            while (controller.IsPresentingStep)
            { Require(Time.realtimeSinceStartup < deadline, "Original grid step presentation did not finish."); yield return null; }

            int counter = controller.Checkpoint.Counter;
            string before = FightBattleCheckpointSerializer.Save(controller.Checkpoint);
            File.WriteAllText(Path.Combine(output, "grid-active-save.json"), before);
            UnityEngine.Object.Destroy(runner); yield return null;
            home.CloseForNavigation(); yield return null; yield return null;
            home = HomeFeatureRuntime.Begin(slot, savesRoot, null); adoptHome(home);
            home.TutorialAutoStartEnabled = false;
            FightBattleCheckpoint settled = null;
            runner = home.StartWeeklyChallengeBattle(4800, value => settled = value); runner.enabled = false;
            controller = runner.Controller;
            Require(controller.Checkpoint.Counter == counter
                && FightBattleCheckpointSerializer.Save(controller.Checkpoint) == before,
                "Cold grid resume changed state or replayed its creation/damage receipt.");
            OfficialBattleGridLifecycleValidation.RequireActive(controller, ids);
            yield return WaitForHud(home);
            yield return new WaitForSeconds(.15f);
            yield return capture("grid-cold-resume");
            FightBattleStepEvent expiry = null;
            controller.StepCompleted += value =>
            {
                if (!value.Event.GridViewEvents.Any(e => e.Kind == FightBattleGridViewEventKind.Remove)) return;
                expiry = value.Event; runner.enabled = false;
            };
            runner.enabled = true;
            deadline = Time.realtimeSinceStartup + 120f;
            while (expiry == null)
            { Require(Time.realtimeSinceStartup < deadline && runner != null, "Restored burning grids did not expire."); yield return null; }
            Require(expiry.GridViewEvents.Count(e => e.Kind == FightBattleGridViewEventKind.StatusEnd) == 9
                && expiry.GridViewEvents.Count(e => e.Kind == FightBattleGridViewEventKind.Remove) == 9,
                "Grid expiry omitted per-cell status/entity ends.");
            while (controller.IsPresentingStep)
            { Require(Time.realtimeSinceStartup < deadline, "Grid expiry presentation did not finish."); yield return null; }
            yield return null;
            Require(ids.All(id => controller.TryGetUnitTransform(id) == null)
                && controller.Checkpoint.GridItems.All(item => item.Ended && item.Runtime.Statuses.Count == 0),
                "Expired native grids or their statuses remained alive.");
            yield return capture("grid-expired");
            OfficialBattleGridLifecycleValidation.ArmFinish(controller.Checkpoint, finish);
            runner.enabled = true;
            while (runner != null)
            { Require(Time.realtimeSinceStartup < deadline, "Grid battle did not finish through normal settlement."); yield return null; }
            Require(settled != null && settled.Finished && settled.Outcome.Won,
                "Original grid battle did not reach a normal victory result.");
            string final = FightBattleCheckpointSerializer.Save(settled);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(final)) == final,
                "Grid lifecycle receipts changed after save/load.");
            File.WriteAllText(Path.Combine(output, "grid-final.json"), final);
            yield return new WaitForSecondsRealtime(.4f);
            yield return capture("grid-result");
            Debug.Log("[Native grid battle] original 13050/13052/13055: delayed nine-cell native fire, nine hits on one large body, cold resume without replay, per-cell expiry and saved normal victory passed.");
        }

        private static IEnumerator WaitForHud(HomeFeatureRuntime home)
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            Transform hud;
            while ((hud = home.Hud.CanvasTransform.Find("FightMainPanel")) == null || !hud.gameObject.activeInHierarchy)
            {
                Require(Time.realtimeSinceStartup < deadline, "Original delayed battle HUD did not mount.");
                yield return null;
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator Start(OfficialBattleRuntimeController controller, Func<Selectable, IEnumerator> click)
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            while (controller.Phase != FightBattlePhase.Play)
            {
                Require(Time.realtimeSinceStartup < deadline, "Original grid battle start control is unavailable.");
                var start = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(value => value.isActiveAndEnabled
                    && value.interactable && value.transform.parent.name == "StartBtn");
                if (start != null)
                {
                    bool clicked = false;
                    UnityEngine.Events.UnityAction observeClick = () => clicked = true;
                    start.onClick.AddListener(observeClick);
                    try { yield return click(start); }
                    finally { if (start != null) start.onClick.RemoveListener(observeClick); }
                    Require(clicked, "The native start button did not receive the simulated pointer click.");
                    Debug.Log("[Native grid battle] Original start button received the simulated pointer click.");
                }
                yield return null;
            }
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Native grid battle: " + message); }
    }
}
