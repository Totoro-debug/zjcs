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
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    // Explicit isolated diagnostic; uses production assembly, battle, native buttons and disk checkpoints.
    public static class SkillVectorReflectionValidation
    {
        public static IEnumerator Exercise(string output, Func<string, IEnumerator> capture,
            Func<Selectable, IEnumerator> click)
        {
            AudioListener.volume = 0f;
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            string saveRoot = Path.Combine(output, "save-" + Guid.NewGuid().ToString("N"));
            var slots = new ProfileSlotCatalog(saveRoot);
            string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave();
            var profile = save.Player;
            profile.CharacterCreated = true; profile.CharacterName = "矢量回弹验证";
            profile.Level = 50; profile.Rank = 2; profile.SubRank = 5;
            profile.Experience = assets.Load().CumulativeLevelExperience[50];
            profile.InitialWeaponType = 200; profile.Wardrobe.Profession = 2;
            profile.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10541, Rank = 7 });
            profile.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10008, Rank = 7 });
            profile.Skills.Plans[0].PassiveSkills[0] = 10541;
            profile.Skills.Plans[0].ActiveSkills[0] = 10008;
            profile.Skills.ActiveSlotLevels = new[] { 20, 1, 1, 1 };
            profile.Skills.PassiveSlotLevels = new[] { 30, 1, 1, 1 };
            Require(OfflineSkillProgression.IsValid(profile.Skills), "Invalid diagnostic skill plan.");
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var progression = new OfflineCharacterProgressionService(session, assets.Load());
            progression.InitializeWardrobe("vector-reflection-diagnostic-wardrobe");
            progression.AttachSkillRules(assets.LoadSkillRules());
            string originalSkills = JsonUtility.ToJson(progression.Player.Skills);
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>()) camera.enabled = false;
            var home = HomeFeatureRuntime.Begin(slot, saveRoot, null);
            home.TutorialAutoStartEnabled = false;
            yield return null;
            var combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            var combat = combatAssets.Load();
            var battles = OfflineWeeklyChallengeBattleCatalog.LoadDefault();
            Require(battles.TryGetBattle(4800, out var stage, out var roster, out var scene), "Original weekly stage missing.");
            var checkpoint = OfficialMapBattleCheckpointBuilder.Create(new OfficialMapBattleCheckpointRequest {
                Stage = stage, Roster = roster, Scene = scene, Player = home.Profile.Save.Player,
                World = home.Profile.Save.World, Progression = home.DailyDungeonProgression, RandomSeed = 17 });
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var secret = player.Statuses.Single(value => value.ClassId == 25021 && value.ApplyingSkillItemId == 10541);
            Require(secret.SourceSkillRank == 7 && secret.SourceSkillLevel == 30 && secret.SourceSubRank == 5,
                "Production secret assembly lost cultivation provenance.");
            var enemy = checkpoint.Units.First(value => value.Side != player.Side && value.Wave == 0);
            checkpoint.Units.RemoveAll(value => value != player && value != enemy);
            checkpoint.Waves.Clear();
            checkpoint.Waves.Add(new FightBattleWaveState { Index = 0, Spawned = true, UnitIds = new List<int> { enemy.Id } });
            checkpoint.MaxRound = 5; checkpoint.IsPlayerRoundLimit = false; checkpoint.PlayerRoundLimit = 0;
            enemy.X = player.X + 1; enemy.Y = player.Y;
            foreach (var unit in checkpoint.Units)
                OfficialBattleUnitAssembler.Initialize(combatAssets.LoadDamageFormula(), unit,
                    new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, 1000000 },
                        { 4, unit.Id == 1 ? 900 : 1000 } }, 3, 4);
            // The original weekly enemy starts with cooldown-only turns. Select the original
            // ready slash as this diagnostic's incoming skill, without changing its graph.
            enemy.Skills.Clear(); enemy.SkillState.Slots.Clear();
            var incoming = combat.CreateSkill(10600, 0, 10008, 7, 20, 5,
                combat.CalculateSkillProps(10600, 7, 20, 5)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            enemy.Skills.Add(incoming); enemy.SkillState.Slots.Add(combat.CreateSkillSlot(incoming, 0));
            var formula = combatAssets.LoadDamageFormula();
            player.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("BlockPercent"), Value = 10000 });
            foreach (var unit in checkpoint.Units)
                foreach (var key in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue",
                    "BaseCritRatePercentValue", "BaseCritAvoidPercentValue" })
                    unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(key), Value = 10000 });
            var shield = combat.CreateStatus(24435, player.Id, player.Id, 724435);
            shield.ShieldUsesCustomValue = true; shield.CustomShieldValue = 1000000;
            player.Statuses.Add(shield);
            var weekly = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
            weekly.StartBattle(4800, weekly.CurrentPeriodStartLocalTicks(DateTime.Now), FightBattleCheckpointSerializer.Save(checkpoint));
            FightBattleCheckpoint settled = null;
            var runner = home.StartWeeklyChallengeBattle(4800, value => settled = value);
            var controller = runner.Controller;
            bool shieldCounter = false, lifeCounter = false;
            int diskChecks = 0, releases = 0;
            FightBattleCheckpoint pendingDisk = null;
            controller.StepCompleted += step => {
                var current = step.Checkpoint.Units.Single(value => value.Id == 1);
                var incomingDamage = step.Event.DamageEvents.SingleOrDefault(value => value.SkillEntityId == 10600);
                if (step.Event.ActorId == enemy.Id && incomingDamage != null) {
                    Require(incomingDamage.Block && !incomingDamage.Dodge && !incomingDamage.Blinding && !incomingDamage.Immunity,
                        "Controlled incoming skill did not produce a successful block.");
                    var counter = step.Event.SkillCasts.SingleOrDefault(value => value.SkillEntityId == 11230);
                    var response = step.Event.DamageEvents.SingleOrDefault(value => value.SkillEntityId == 11230);
                    Require(counter != null && counter.ActorId == player.Id && counter.SkillItemId == 10541
                        && counter.TargetIds.SequenceEqual(new[] { enemy.Id }) && counter.Slot == -1
                        && response != null && response.SourceId == player.Id && response.TargetId == enemy.Id
                        && response.Damage < 0, "Blocked hit did not produce the original holder-to-attacker counterattack.");
                    if (incomingDamage.Damage == 0 && incomingDamage.ShieldDamage > 0) shieldCounter = true;
                    if (incomingDamage.Damage < 0) lifeCounter = true;
                    releases++;
                }
                // StepCompleted precedes the mode adapter's save. Compare an immutable
                // snapshot on the next coroutine frame, after that production write.
                Require(pendingDisk == null, "A previous step's disk checkpoint was not checked.");
                pendingDisk = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(step.Checkpoint));
            };
            Button start = null;
            while (start == null) {
                start = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(value => value.isActiveAndEnabled
                    && value.interactable && value.transform.parent.name == "StartBtn");
                if (start == null) yield return null;
            }
            yield return new WaitForSecondsRealtime(1f);
            yield return capture("vector-reflection-preparation");
            yield return click(start);
            bool shieldCaptured = false, lifeCaptured = false, resultCaptured = false;
            while (runner != null) {
                if (pendingDisk != null) {
                    var diskSession = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
                    Require(diskSession.TryResumeBattle(4800, out string json, out _), "Missing disk battle checkpoint.");
                    VerifyDisk(pendingDisk, FightBattleCheckpointSerializer.Load(json));
                    pendingDisk = null; diskChecks++;
                }
                if (shieldCounter && !shieldCaptured) {
                    runner.enabled = false; yield return new WaitForSecondsRealtime(0.6f);
                    yield return capture("vector-reflection-shield"); shieldCaptured = true; runner.enabled = true;
                }
                if (lifeCounter && !lifeCaptured) {
                    runner.enabled = false; yield return new WaitForSecondsRealtime(0.6f);
                    yield return capture("vector-reflection-life"); lifeCaptured = true; runner.enabled = true;
                }
                var result = UnityEngine.Object.FindObjectOfType<DemonResultWindow>();
                if (result != null && result.IsReady) {
                    Require(settled != null && settled.Finished, "Native result preceded actual settlement.");
                    yield return capture("vector-reflection-result"); resultCaptured = true;
                    yield return click(result.Panel.GetComponent<Zjcs.Official.BattleResultUI.GameEndPanelDemonKing>().LeaveGameBtn);
                }
                yield return null;
            }
            Require(shieldCaptured && lifeCaptured && resultCaptured && releases > 0 && diskChecks > 0,
                "Full-shield and life-damage blocks, native result or disk checkpoint were not exercised.");
            var saved = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
            Require(JsonUtility.ToJson(saved.Player.Skills) == originalSkills, "Battle changed the player's saved skill plan.");
            File.WriteAllText(Path.Combine(output, "vector-reflection-final.json"), FightBattleCheckpointSerializer.Save(settled));
            yield return capture("vector-reflection-return");
            Require(UnityEngine.Object.FindObjectOfType<DemonResultWindow>() == null, "Native result did not close after simulated leave.");
            Debug.Log("[Vector reflection flow] shield=1 life=1 counters=" + releases + " disk=" + diskChecks
                + "; production assembly, original incoming/block/11230, native input and saved plans passed.");
            home.CloseForNavigation();
        }

        private static void VerifyDisk(FightBattleCheckpoint expected, FightBattleCheckpoint disk)
        {
            Require(disk.Sequence == expected.Sequence && disk.Counter == expected.Counter
                && disk.RandomSeed == expected.RandomSeed, "Saved receipt counter or random stream differs: expected "
                    + expected.Sequence + "/" + expected.Counter + "/" + expected.RandomSeed + ", actual "
                    + disk.Sequence + "/" + disk.Counter + "/" + disk.RandomSeed);
            foreach (var unit in expected.Units) {
                var saved = disk.Units.Single(value => value.Id == unit.Id);
                Require(saved.Hp == unit.Hp && saved.MaxHp == unit.MaxHp
                    && saved.SkillState.Round == unit.SkillState.Round
                    && saved.SkillState.Slots.Select(value => value.ReleasedTimes)
                        .SequenceEqual(unit.SkillState.Slots.Select(value => value.ReleasedTimes)),
                    "Saved life, personal round or release count differs.");
                foreach (int id in new[] { 25021, 24435 }) {
                    var source = unit.Statuses.SingleOrDefault(value => value.ClassId == id);
                    var copy = saved.Statuses.SingleOrDefault(value => value.ClassId == id);
                    Require((source == null) == (copy == null), "Saved secret/buff presence differs.");
                    if (source == null) continue;
                    Require(source.StackCount == copy.StackCount && source.OwnerId == copy.OwnerId
                        && source.CreatorId == copy.CreatorId && source.ApplyingSkillItemId == copy.ApplyingSkillItemId
                        && source.RootSkillId == copy.RootSkillId && source.SourceSkillRank == copy.SourceSkillRank
                        && source.SourceSkillLevel == copy.SourceSkillLevel && source.SourceSubRank == copy.SourceSubRank
                        && source.LogicTriggeredCount == copy.LogicTriggeredCount
                        && source.DurationRound == copy.DurationRound && source.CustomShieldValue == copy.CustomShieldValue,
                        "Saved stack, provenance or next-cast duration differs.");
                }
            }
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
