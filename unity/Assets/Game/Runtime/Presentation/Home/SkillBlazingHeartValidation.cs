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
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    // Explicit Player diagnostic: reuse the profile produced by the original fragment UI.
    public static class SkillBlazingHeartValidation
    {
        [Serializable]
        private sealed class Evidence { public bool equipped; public long affinity; public long damage; public int diskChecks; }

        public static IEnumerator Exercise(string output, Func<string, IEnumerator> capture,
            Func<Selectable, IEnumerator> click)
        {
            Debug.Log("[Blazing heart battle] validation started");
            string sourcePath = Environment.GetEnvironmentVariable("ZJCS_BLAZING_HEART_PROFILE");
            bool equipped = Environment.GetEnvironmentVariable("ZJCS_BLAZING_HEART_EQUIPPED") == "1";
            Require(!string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath), "Missing acquired secret profile.");
            var save = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(sourcePath));
            var profile = save.Player;
            OfflineItemAutoIncrease.InitializeNewCharacter(profile, DateTime.UtcNow);
            Require(OfflineSkillProgression.OwnedRank(profile.Skills, 10520) > 0
                && profile.Skills.Plans[profile.Skills.SelectedPlan].PassiveSkills.Contains(10520),
                "The source profile did not acquire and equip 炽热之心 through the original UI.");
            if (!equipped)
                profile.Skills.Plans[profile.Skills.SelectedPlan].PassiveSkills = new int[4];
            if (!profile.Skills.Skills.Any(value => value.ClassId == 10028))
                profile.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10028, Rank = 1 });
            profile.Skills.Plans[profile.Skills.SelectedPlan].ActiveSkills = new[] { 10028, 0, 0, 0 };
            profile.InitialWeaponType = Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.WandInitialWeaponType;
            profile.SeasonRank = 0;
            string originalSkills = JsonUtility.ToJson(profile.Skills);
            string saveRoot = Path.Combine(output, "save-" + Guid.NewGuid().ToString("N"));
            var slots = new ProfileSlotCatalog(saveRoot);
            string slot = slots.CreateSlot();
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var progression = new OfflineCharacterProgressionService(session, assets.Load());
            progression.InitializeWardrobe("blazing-heart-validation-wardrobe");
            progression.AttachSkillRules(assets.LoadSkillRules());
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>()) camera.enabled = false;
            var home = HomeFeatureRuntime.Begin(slot, saveRoot, null);
            home.TutorialAutoStartEnabled = false;
            yield return null;

            var combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            var combat = combatAssets.Load();
            var catalog = OfflineWeeklyChallengeBattleCatalog.LoadDefault();
            Require(catalog.TryGetBattle(4800, out var stage, out var roster, out var scene), "Original weekly stage missing.");
            var checkpoint = OfficialMapBattleCheckpointBuilder.Create(new OfficialMapBattleCheckpointRequest
            {
                Stage = stage, Roster = roster, Scene = scene, Player = home.Profile.Save.Player,
                World = home.Profile.Save.World, Progression = home.DailyDungeonProgression, RandomSeed = 17
            });
            var player = checkpoint.Units.Single(value => value.Id == 1);
            Require(player.Skills.Any(value => value.ItemId == 10028), "The actual battle omitted the saved fireball.");
            int fireAffinity = combat.PropertyType("FireDamageAdd");
            long affinity = player.Properties.Where(value => value.Id == fireAffinity).Sum(value => value.Value);
            var enemy = checkpoint.Units.First(value => value.Side != player.Side && value.Wave == 0);
            checkpoint.Units.RemoveAll(value => value != player && value != enemy);
            checkpoint.Waves.Clear();
            checkpoint.Waves.Add(new FightBattleWaveState { Index = 0, Spawned = true, UnitIds = new List<int> { enemy.Id } });
            checkpoint.MaxRound = 5; checkpoint.IsPlayerRoundLimit = false; checkpoint.PlayerRoundLimit = 0;
            // The original boss occupies five cells across; keep its body clear of
            // the player while retaining the real stage and a fireball-range target.
            enemy.X = player.X + 5; enemy.Y = player.Y;
            foreach (var unit in checkpoint.Units)
            {
                var props = new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, 1000000 },
                    { 4, unit.Id == 1 ? 1000 : 900 }, { combat.PropertyType("BaseElementAdd"), 10000 },
                    { combat.PropertyType("BaseElementReduce"), 10000 } };
                if (unit.Id == 1 && affinity != 0) props[fireAffinity] = affinity;
                OfficialBattleUnitAssembler.Initialize(combatAssets.LoadDamageFormula(), unit, props, 3, 4);
            }
            var weekly = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
            weekly.StartBattle(4800, weekly.CurrentPeriodStartLocalTicks(DateTime.Now), FightBattleCheckpointSerializer.Save(checkpoint));
            FightBattleCheckpoint settled = null;
            var runner = home.StartWeeklyChallengeBattle(4800, value => settled = value);
            var controller = runner.Controller;
            long damage = 0;
            int diskChecks = 0;
            FightBattleCheckpoint pendingDisk = null;
            controller.StepCompleted += step =>
            {
                var hit = step.Event.DamageEvents.FirstOrDefault(value => value.SourceId == player.Id
                    && value.TargetId == enemy.Id && value.SkillItemId == 10028 && !value.IsCure && value.Damage < 0);
                if (hit != null && damage == 0) damage = Math.Abs(hit.Damage);
                Require(pendingDisk == null, "The previous battle step was not checked on disk.");
                pendingDisk = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(step.Checkpoint));
            };
            Button start = null;
            while (start == null)
            {
                start = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(value => value.isActiveAndEnabled
                    && value.interactable && value.transform.parent.name == "StartBtn");
                if (start == null) yield return null;
            }
            yield return new WaitForSecondsRealtime(1f);
            yield return capture("blazing-heart-preparation");
            yield return click(start);
            bool captured = false, resultCaptured = false;
            while (runner != null)
            {
                if (pendingDisk != null)
                {
                    var disk = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
                    Require(disk.TryResumeBattle(4800, out string json, out _), "The actual battle checkpoint was not saved.");
                    var restored = FightBattleCheckpointSerializer.Load(json);
                    long expectedAffinity = pendingDisk.Units.Single(value => value.Id == player.Id).Properties
                        .Where(value => value.Id == fireAffinity).Sum(value => value.Value);
                    long savedAffinity = restored.Units.Single(value => value.Id == player.Id).Properties
                        .Where(value => value.Id == fireAffinity).Sum(value => value.Value);
                    Require(restored.Sequence == pendingDisk.Sequence && restored.Counter == pendingDisk.Counter
                        && savedAffinity == expectedAffinity && (diskChecks != 0 || savedAffinity == affinity),
                        "Battle checkpoint lost its receipt or fire affinity.");
                    pendingDisk = null;
                    diskChecks++;
                }
                if (!captured && damage > 0)
                {
                    yield return capture("blazing-heart-battle");
                    captured = true;
                }
                var result = UnityEngine.Object.FindObjectOfType<DemonResultWindow>();
                if (result != null && result.IsReady)
                {
                    Require(settled != null && settled.Finished, "Original result appeared before settlement.");
                    yield return capture("blazing-heart-result");
                    resultCaptured = true;
                    yield return click(result.Panel.GetComponent<Zjcs.Official.BattleResultUI.GameEndPanelDemonKing>().LeaveGameBtn);
                }
                yield return null;
            }
            Require(damage > 0 && captured && resultCaptured && diskChecks > 0,
                "The actual battle did not cast, save, render and finish.");
            var reloaded = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
            Require(JsonUtility.ToJson(reloaded.Player.Skills) == originalSkills, "Battle changed the saved skill plan.");
            File.WriteAllText(Path.Combine(output, "blazing-heart-battle.json"),
                JsonUtility.ToJson(new Evidence { equipped = equipped, affinity = affinity, damage = damage, diskChecks = diskChecks }, true));
            yield return capture("blazing-heart-return");
            home.CloseForNavigation();
            Debug.Log("[Blazing heart battle] equipped=" + equipped + " affinity=" + affinity
                + " fire damage=" + damage + " disk checks=" + diskChecks);
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
