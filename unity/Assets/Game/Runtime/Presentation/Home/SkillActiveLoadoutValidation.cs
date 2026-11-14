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
    // Explicit diagnostic. Profiles, controlled battle inputs and receipts stay in the supplied workspace.
    public static class SkillActiveLoadoutValidation
    {
        public static IEnumerator Exercise(string output, Func<string, IEnumerator> capture,
            Func<Selectable, IEnumerator> click)
        {
            string mode = Environment.GetEnvironmentVariable("ZJCS_ACTIVE_LOADOUT_MODE") ?? "empty";
            Require(mode == "empty" || mode == "invalid" || mode == "mixed", "Unknown active loadout diagnostic.");
            AudioListener.volume = 0f;
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var rules = assets.LoadSkillRules();
            int foreign = rules.Skills.First(value => value.ItemType == 100
                && !value.AvailableProfessions.Contains(1)).ClassId;
            int unowned = rules.Skills.First(value => value.ItemType == 100
                && value.ClassId != foreign && value.ClassId != 10008).ClassId;
            string saveRoot = Path.Combine(output, "save-" + Guid.NewGuid().ToString("N"));
            var slots = new ProfileSlotCatalog(saveRoot);
            string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave();
            var profile = save.Player;
            profile.CharacterCreated = true; profile.CharacterName = "战技装配验证";
            profile.Level = 50; profile.Rank = 2; profile.SubRank = 5; profile.SeasonRank = 17;
            profile.Experience = assets.Load().CumulativeLevelExperience[50];
            profile.InitialWeaponType = 200; profile.Wardrobe.Profession = 1;
            profile.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10537, Rank = 7 });
            profile.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = foreign, Rank = 7 });
            profile.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10008, Rank = 7 });
            profile.Skills.Plans[0].PassiveSkills[0] = 10537;
            profile.Skills.Plans[6].ActiveSkills[0] = foreign;
            if (mode != "empty") profile.Skills.Plans[0].ActiveSkills = new[] { unowned, foreign, 10537, int.MaxValue };
            if (mode == "mixed") profile.Skills.Plans[0].ActiveSkills[2] = 10008;
            profile.Skills.ActiveSlotLevels = new[] { 2, 7, 20, 30 };
            profile.Skills.PassiveSlotLevels = new[] { 20, 1, 1, 1 };
            profile.Skills.BlessRecords.Add(new OfflineSkillBlessRecord { SeasonId = 17,
                ActiveSlotLevels = new[] { 1, 4, 0, 3 }, PassiveSlotLevels = new[] { 3, 0, 0, 0 } });
            Require(OfflineSkillProgression.IsValid(profile.Skills), "Diagnostic skill plan is structurally invalid.");
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var progression = new OfflineCharacterProgressionService(session, assets.Load());
            progression.InitializeWardrobe("active-loadout-diagnostic-wardrobe");
            progression.AttachSkillRules(rules);
            string originalSkills = JsonUtility.ToJson(progression.Player.Skills);
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
            VerifySkills(player, mode);
            Require(player.Statuses.Any(value => value.ApplyingSkillItemId == 10537 && value.ClassId == 25017),
                "Empty active loadout omitted its actual equipped counterattack secret.");
            // Control only the diagnostic's duration, participant count, properties and positions.
            // Player active/passive graphs and enemy skill definitions remain the production assembly.
            var enemy = checkpoint.Units.First(value => value.Side != player.Side && value.Wave == 0);
            checkpoint.Units.RemoveAll(value => value != player && value != enemy);
            checkpoint.Waves.Clear();
            checkpoint.Waves.Add(new FightBattleWaveState { Index = 0, Spawned = true, UnitIds = new List<int> { enemy.Id } });
            checkpoint.MaxRound = 5; checkpoint.IsPlayerRoundLimit = false; checkpoint.PlayerRoundLimit = 0;
            enemy.X = player.X + 1; enemy.Y = player.Y;
            foreach (var unit in checkpoint.Units)
            {
                var props = new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, 1000000 }, { 4, unit.Id == 1 ? 1000 : 900 } };
                OfficialBattleUnitAssembler.Initialize(combatAssets.LoadDamageFormula(), unit, props, 3, 4);
            }
            var weekly = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
            weekly.StartBattle(4800, weekly.CurrentPeriodStartLocalTicks(DateTime.Now), FightBattleCheckpointSerializer.Save(checkpoint));
            FightBattleCheckpoint settled = null;
            var runner = home.StartWeeklyChallengeBattle(4800, value => settled = value);
            var controller = runner.Controller;
            int playerTurns = 0, ordinary = 0, counters = 0, diskChecks = 0;
            FightBattleCheckpoint pendingDisk = null;
            controller.StepCompleted += step =>
            {
                var currentPlayer = step.Checkpoint.Units.Single(value => value.Id == 1);
                VerifySkills(currentPlayer, mode);
                if (step.Event.ActorId == 1) playerTurns++;
                foreach (var cast in step.Event.SkillCasts.Where(value => value.ActorId == 1))
                {
                    if (cast.Slot >= 0)
                    {
                        Require(mode == "mixed" && cast.SkillItemId == 10008 && cast.Slot == 2,
                            "Rejected/empty active slot cast a skill or valid slot order changed.");
                        ordinary++;
                    }
                    else if (cast.SkillEntityId == 11210)
                    {
                        Require(cast.SkillItemId == 10537 && cast.TargetIds.Contains(enemy.Id), "Counterattack lost its secret/attacker source.");
                        counters++;
                    }
                }
                Require(pendingDisk == null, "A previous active-plan step was not checked on disk.");
                pendingDisk = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(step.Checkpoint));
            };
            Button start = null;
            while (start == null)
            {
                start = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(value => value.isActiveAndEnabled
                    && value.interactable && value.transform.parent.name == "StartBtn");
                if (start == null) yield return null;
            }
            // Capture the original preparation UI after its show animation, before input.
            yield return new WaitForSecondsRealtime(1f);
            yield return capture("loadout-preparation");
            yield return click(start);
            bool captured = false, resultCaptured = false;
            while (runner != null)
            {
                if (pendingDisk != null)
                {
                    var diskSession = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
                    Require(diskSession.TryResumeBattle(4800, out string json, out _), "Active battle was not saved to the isolated weekly store.");
                    var saved = FightBattleCheckpointSerializer.Load(json);
                    Require(saved.Sequence == pendingDisk.Sequence && saved.Counter == pendingDisk.Counter
                        && saved.RandomSeed == pendingDisk.RandomSeed, "Fresh disk checkpoint has a different receipt or random stream.");
                    var savedPlayer = saved.Units.Single(value => value.Id == 1);
                    var expectedPlayer = pendingDisk.Units.Single(value => value.Id == 1);
                    VerifySkills(savedPlayer, mode);
                    Require(savedPlayer.Hp == expectedPlayer.Hp && savedPlayer.SkillState.Round == expectedPlayer.SkillState.Round
                        && savedPlayer.SkillState.Slots.Select(value => value.ReleasedTimes)
                            .SequenceEqual(expectedPlayer.SkillState.Slots.Select(value => value.ReleasedTimes)),
                        "Fresh disk player life, round or release counts differ.");
                    pendingDisk = null; diskChecks++;
                }
                if (!captured && playerTurns > 0 && counters > 0)
                {
                    yield return capture("loadout-round-and-counter"); captured = true;
                }
                var result = UnityEngine.Object.FindObjectOfType<DemonResultWindow>();
                if (result != null && result.IsReady)
                {
                    Require(settled != null && settled.Finished, "Native result appeared without actual settlement.");
                    yield return capture("loadout-result"); resultCaptured = true;
                    yield return click(result.Panel.GetComponent<Zjcs.Official.BattleResultUI.GameEndPanelDemonKing>().LeaveGameBtn);
                }
                yield return null;
            }
            Require(playerTurns > 0 && captured && counters > 0 && resultCaptured && diskChecks > 0,
                "Loadout flow did not exercise ordinary turns, passive counterattack, disk checkpoints and native settlement.");
            Require(mode == "mixed" ? ordinary > 0 : ordinary == 0, "Empty or valid active release behavior differs.");
            var disk = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
            Require(JsonUtility.ToJson(disk.Player.Skills) == originalSkills, "Battle changed an old or selected skill plan.");
            File.WriteAllText(Path.Combine(output, "loadout-final.json"), FightBattleCheckpointSerializer.Save(settled));
            yield return capture("loadout-return");
            Require(UnityEngine.Object.FindObjectOfType<DemonResultWindow>() == null, "Native result did not release after simulated leave.");
            Debug.Log("[Active loadout flow] mode=" + mode + " turns=" + playerTurns + " ordinary=" + ordinary
                + " counters=" + counters + " disk=" + diskChecks + "; original start/result input, assembly, rounds, passive and saved plans passed.");
            home.CloseForNavigation();
        }

        private static void VerifySkills(FightBattleUnitState player, string mode)
        {
            Require(player.Skills.Count == (mode == "mixed" ? 1 : 0), "Production loadout selection differs.");
            Require(player.SkillState.Slots.Count == player.Skills.Count, "Empty loadout retained or injected a slot.");
            if (mode != "mixed") return;
            var skill = player.Skills.Single();
            Require(skill.ItemId == 10008 && skill.Slot == 2 && skill.Rank == 7 && skill.Level == 20 && skill.SubRank == 5,
                "Valid production battle skill lost slot/cultivation provenance.");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
