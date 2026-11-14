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
    public static class SkillShieldAttackValidation
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
            profile.CharacterCreated = true; profile.CharacterName = "以守为攻验证";
            profile.Level = 50; profile.Rank = 2; profile.SubRank = 5;
            profile.Experience = assets.Load().CumulativeLevelExperience[50];
            profile.InitialWeaponType = 200; profile.Wardrobe.Profession = 2;
            profile.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10540, Rank = 7 });
            profile.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10046, Rank = 7 });
            profile.Skills.Plans[0].PassiveSkills[0] = 10540;
            profile.Skills.Plans[0].ActiveSkills[0] = 10046;
            profile.Skills.ActiveSlotLevels = new[] { 20, 1, 1, 1 };
            profile.Skills.PassiveSlotLevels = new[] { 30, 1, 1, 1 };
            Require(OfflineSkillProgression.IsValid(profile.Skills), "Invalid diagnostic skill plan.");
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var progression = new OfflineCharacterProgressionService(session, assets.Load());
            progression.InitializeWardrobe("shield-attack-diagnostic-wardrobe");
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
            var secret = player.Statuses.Single(value => value.ClassId == 25020 && value.ApplyingSkillItemId == 10540);
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
            // Original persistent shield starts at one point to observe an actual break.
            // The equipped original 10046/11160 then creates its own shield normally.
            var initial = combat.CreateStatus(24435, 1, 1, 724435, 11160, 10046, 7, 20, 5, player.Properties);
            initial.ShieldUsesCustomValue = true; initial.CustomShieldValue = 1;
            player.Statuses.Add(initial);
            var weekly = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
            weekly.StartBattle(4800, weekly.CurrentPeriodStartLocalTicks(DateTime.Now), FightBattleCheckpointSerializer.Save(checkpoint));
            FightBattleCheckpoint settled = null;
            var runner = home.StartWeeklyChallengeBattle(4800, value => settled = value);
            var controller = runner.Controller;
            bool lost = false, gained = false;
            int diskChecks = 0, releases = 0;
            FightBattleCheckpoint pendingDisk = null;
            controller.StepCompleted += step => {
                var current = step.Checkpoint.Units.Single(value => value.Id == 1);
                bool hasShield = current.Statuses.Any(value => value.ActionType == 60);
                var buff = current.Statuses.SingleOrDefault(value => value.ClassId == 24212);
                Require(hasShield == (buff != null), "Shield presence and maintained bonus differ after an actual step.");
                long attack = current.Properties.Single(value => value.Id == 1).Value;
                Require(hasShield ? attack > 1000 : attack == 1000, "Actual step retained/lost the wrong attack contribution.");
                if (buff != null)
                    Require(buff.SourceSkillRank == 7 && buff.SourceSkillLevel == 30 && buff.SourceSubRank == 5
                        && buff.RootSkillId == 25020 && buff.ApplyingSkillItemId == 10540
                        && buff.IsActionExistGenerated, "Maintained bonus lost its secret cultivation.");
                if (step.Event.DamageEvents.Any(value => value.TargetId == 1 && value.ShieldDamage == 1) && !hasShield)
                    lost = true;
                if (step.Event.SkillCasts.Any(value => value.ActorId == 1 && value.Slot >= 0)) {
                    releases++;
                    if (hasShield) gained = true;
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
            yield return capture("shield-attack-preparation");
            yield return click(start);
            bool lostCaptured = false, gainedCaptured = false, resultCaptured = false;
            while (runner != null) {
                if (pendingDisk != null) {
                    var diskSession = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
                    Require(diskSession.TryResumeBattle(4800, out string json, out _), "Missing disk battle checkpoint.");
                    VerifyDisk(pendingDisk, FightBattleCheckpointSerializer.Load(json));
                    pendingDisk = null; diskChecks++;
                }
                if (lost && !lostCaptured) {
                    runner.enabled = false; yield return new WaitForSecondsRealtime(0.6f);
                    yield return capture("shield-attack-lost"); lostCaptured = true; runner.enabled = true;
                }
                if (gained && !gainedCaptured) {
                    runner.enabled = false; yield return new WaitForSecondsRealtime(0.6f);
                    yield return capture("shield-attack-gained"); gainedCaptured = true; runner.enabled = true;
                }
                var result = UnityEngine.Object.FindObjectOfType<DemonResultWindow>();
                if (result != null && result.IsReady) {
                    Require(settled != null && settled.Finished, "Native result preceded actual settlement.");
                    yield return capture("shield-attack-result"); resultCaptured = true;
                    yield return click(result.Panel.GetComponent<Zjcs.Official.BattleResultUI.GameEndPanelDemonKing>().LeaveGameBtn);
                }
                yield return null;
            }
            Require(lostCaptured && gainedCaptured && resultCaptured && releases > 0 && diskChecks > 0,
                "Shield break, original shield reapplication, native result or disk checkpoint was not exercised.");
            var saved = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
            Require(JsonUtility.ToJson(saved.Player.Skills) == originalSkills, "Battle changed the player's saved skill plan.");
            File.WriteAllText(Path.Combine(output, "shield-attack-final.json"), FightBattleCheckpointSerializer.Save(settled));
            yield return capture("shield-attack-return");
            Require(UnityEngine.Object.FindObjectOfType<DemonResultWindow>() == null, "Native result did not close after simulated leave.");
            Debug.Log("[Shield attack flow] lost=1 gained=1 releases=" + releases + " disk=" + diskChecks
                + "; production assembly, actual shield break, original guard reapplication, maintained attack, input and saved plans passed.");
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
                var shields = unit.Statuses.Where(value => value.ActionType == 60).ToArray();
                var copies = saved.Statuses.Where(value => value.ActionType == 60).ToArray();
                Require(shields.Length == copies.Length, "Saved shield presence differs.");
                foreach (var shield in shields) {
                    var copy = copies.Single(value => value.InstanceId == shield.InstanceId);
                    Require(copy.ClassId == shield.ClassId && copy.ForbiddenType == shield.ForbiddenType
                        && copy.ShieldRemainValue == shield.ShieldRemainValue
                        && copy.ShieldRemainCount == shield.ShieldRemainCount, "Saved shield state differs.");
                }
                foreach (int id in new[] { 25020, 24212 }) {
                    var source = unit.Statuses.SingleOrDefault(value => value.ClassId == id);
                    var copy = saved.Statuses.SingleOrDefault(value => value.ClassId == id);
                    Require((source == null) == (copy == null), "Saved secret/buff presence differs.");
                    if (source == null) continue;
                    Require(source.StackCount == copy.StackCount && source.OwnerId == copy.OwnerId
                        && source.CreatorId == copy.CreatorId && source.ApplyingSkillItemId == copy.ApplyingSkillItemId
                        && source.RootSkillId == copy.RootSkillId && source.SourceSkillRank == copy.SourceSkillRank
                        && source.SourceSkillLevel == copy.SourceSkillLevel && source.SourceSubRank == copy.SourceSubRank
                        && source.IsActionExistGenerated == copy.IsActionExistGenerated
                        && source.ActionExistSourceInstanceId == copy.ActionExistSourceInstanceId
                        && source.ActionExistAppliedStatusInstanceId == copy.ActionExistAppliedStatusInstanceId,
                        "Saved maintained bonus, provenance or source instance differs.");
                }
            }
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
