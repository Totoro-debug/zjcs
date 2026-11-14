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
    public static class SkillPursuitValidation
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
            profile.CharacterCreated = true; profile.CharacterName = "乘胜追击验证";
            profile.Level = 50; profile.Rank = 2; profile.SubRank = 5;
            profile.Experience = assets.Load().CumulativeLevelExperience[50];
            profile.InitialWeaponType = 200; profile.Wardrobe.Profession = 2;
            profile.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10516, Rank = 7 });
            profile.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10008, Rank = 7 });
            profile.Skills.Plans[0].PassiveSkills[0] = 10516;
            profile.Skills.Plans[0].ActiveSkills[0] = 10008;
            profile.Skills.ActiveSlotLevels = new[] { 20, 1, 1, 1 };
            profile.Skills.PassiveSlotLevels = new[] { 30, 1, 1, 1 };
            Require(OfflineSkillProgression.IsValid(profile.Skills), "Invalid diagnostic skill plan.");
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var progression = new OfflineCharacterProgressionService(session, assets.Load());
            progression.InitializeWardrobe("pursuit-diagnostic-wardrobe");
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
            var secret = player.Statuses.Single(value => value.ClassId == 25008 && value.ApplyingSkillItemId == 10516);
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
                        { 4, unit.Id == 1 ? 1000 : 900 } }, 3, 4);
            // The original weekly enemy starts with cooldown-only turns. Select the original
            // ready slash as this diagnostic's incoming skill, without changing its graph.
            enemy.Skills.Clear(); enemy.SkillState.Slots.Clear();
            var incoming = combat.CreateSkill(10600, 0, 10008, 7, 20, 5,
                combat.CalculateSkillProps(10600, 7, 20, 5)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
            enemy.Skills.Add(incoming); enemy.SkillState.Slots.Add(combat.CreateSkillSlot(incoming, 0));
            // Original one-round abnormal state permits first-hit eligibility and
            // natural expiry in the same production battle without inventing a duration.
            enemy.Statuses.Add(combat.CreateStatus(24020, enemy.Id, player.Id, 724020));
            var weekly = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
            weekly.StartBattle(4800, weekly.CurrentPeriodStartLocalTicks(DateTime.Now), FightBattleCheckpointSerializer.Save(checkpoint));
            FightBattleCheckpoint settled = null;
            var runner = home.StartWeeklyChallengeBattle(4800, value => settled = value);
            var controller = runner.Controller;
            bool eligible = false, expired = false, previousCondition = true;
            int diskChecks = 0, releases = 0, previousInvokes = 0;
            FightBattleCheckpoint pendingDisk = null;
            controller.StepCompleted += step => {
                var current = step.Checkpoint.Units.Single(value => value.Id == 1);
                var currentEnemy = step.Checkpoint.Units.Single(value => value.Id == enemy.Id);
                var passive = current.Statuses.Single(value => value.ClassId == 25008);
                int invokes = passive.Components.HitDmgAddPer.InvokeCount;
                if (step.Event.SkillCasts.Any(value => value.ActorId == 1 && value.Slot >= 0)) {
                    releases++;
                    Require(step.Event.DamageEvents.Any(value => value.SourceId == 1 && value.Damage < 0),
                        "Diagnostic player release did not actually hit.");
                    if (previousCondition) {
                        Require(invokes > previousInvokes, "Original abnormal target did not satisfy pursuit.");
                        eligible = true;
                    } else {
                        Require(invokes == previousInvokes, "Expired abnormal state still satisfied pursuit.");
                        expired = true;
                    }
                }
                Require(passive.SourceSkillRank == 7 && passive.SourceSkillLevel == 30 && passive.SourceSubRank == 5
                    && passive.Components.HitDmgAddPer.DurationSkillCount == -1,
                    "Pursuit lost original cultivation or unlimited cast duration.");
                previousInvokes = invokes;
                previousCondition = currentEnemy.Statuses.Any(value => value.StatusType == 2 || value.StatusType == 3);
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
            yield return capture("pursuit-preparation");
            yield return click(start);
            bool eligibleCaptured = false, expiredCaptured = false, resultCaptured = false;
            while (runner != null) {
                if (pendingDisk != null) {
                    var diskSession = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
                    Require(diskSession.TryResumeBattle(4800, out string json, out _), "Missing disk battle checkpoint.");
                    VerifyDisk(pendingDisk, FightBattleCheckpointSerializer.Load(json));
                    pendingDisk = null; diskChecks++;
                }
                if (eligible && !eligibleCaptured) {
                    runner.enabled = false; yield return new WaitForSecondsRealtime(0.6f);
                    yield return capture("pursuit-eligible"); eligibleCaptured = true; runner.enabled = true;
                }
                if (expired && !expiredCaptured) {
                    runner.enabled = false; yield return new WaitForSecondsRealtime(0.6f);
                    yield return capture("pursuit-expired"); expiredCaptured = true; runner.enabled = true;
                }
                var result = UnityEngine.Object.FindObjectOfType<DemonResultWindow>();
                if (result != null && result.IsReady) {
                    Require(settled != null && settled.Finished, "Native result preceded actual settlement.");
                    yield return capture("pursuit-result"); resultCaptured = true;
                    yield return click(result.Panel.GetComponent<Zjcs.Official.BattleResultUI.GameEndPanelDemonKing>().LeaveGameBtn);
                }
                yield return null;
            }
            Require(eligibleCaptured && expiredCaptured && resultCaptured && releases > 0 && diskChecks > 0,
                "Eligible damage, condition expiry, native result or disk checkpoint was not exercised.");
            var saved = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
            Require(JsonUtility.ToJson(saved.Player.Skills) == originalSkills, "Battle changed the player's saved skill plan.");
            File.WriteAllText(Path.Combine(output, "pursuit-final.json"), FightBattleCheckpointSerializer.Save(settled));
            yield return capture("pursuit-return");
            Require(UnityEngine.Object.FindObjectOfType<DemonResultWindow>() == null, "Native result did not close after simulated leave.");
            Debug.Log("[Pursuit flow] eligible=1 expired=1 releases=" + releases + " disk=" + diskChecks
                + "; production assembly, original abnormal-state condition, expiry, damage, input and saved plans passed.");
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
                foreach (int id in new[] { 25008, 24020 }) {
                    var source = unit.Statuses.SingleOrDefault(value => value.ClassId == id);
                    var copy = saved.Statuses.SingleOrDefault(value => value.ClassId == id);
                    Require((source == null) == (copy == null), "Saved secret/buff presence differs.");
                    if (source == null) continue;
                    Require(source.StackCount == copy.StackCount && source.OwnerId == copy.OwnerId
                        && source.CreatorId == copy.CreatorId && source.ApplyingSkillItemId == copy.ApplyingSkillItemId
                        && source.RootSkillId == copy.RootSkillId && source.SourceSkillRank == copy.SourceSkillRank
                        && source.SourceSkillLevel == copy.SourceSkillLevel && source.SourceSubRank == copy.SourceSubRank
                        && source.Components?.HitDmgAddPer?.DurationSkillCount == copy.Components?.HitDmgAddPer?.DurationSkillCount
                        && source.Components?.HitDmgAddPer?.InvokeCount == copy.Components?.HitDmgAddPer?.InvokeCount,
                        "Saved stack, provenance or next-cast duration differs.");
                }
            }
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
