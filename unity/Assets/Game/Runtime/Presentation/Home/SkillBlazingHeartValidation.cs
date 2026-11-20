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
        private sealed class Evidence { public bool equipped; public long affinity; public long secondary; public long damage; public double effectRate; public int diskChecks; public int firstActor; }

        public static IEnumerator Exercise(string output, Func<string, IEnumerator> capture,
            Func<Selectable, IEnumerator> click)
        {
            Debug.Log("[Blazing heart battle] validation started");
            string sourcePath = Environment.GetEnvironmentVariable("ZJCS_BLAZING_HEART_PROFILE");
            bool equipped = Environment.GetEnvironmentVariable("ZJCS_BLAZING_HEART_EQUIPPED") == "1";
            string requestedSecret = Environment.GetEnvironmentVariable("ZJCS_AFFINITY_SECRET_ID");
            int secretId = string.IsNullOrEmpty(requestedSecret) ? 10520 : int.Parse(requestedSecret);
            Require(secretId == 10520 || secretId == 10521 || secretId == 10522 || secretId == 10550 || secretId == 10555 || secretId == 10583 || secretId == 10559 || secretId == 10554 || secretId == 10578 || secretId == 10524 || secretId == 10535 || secretId == 10505 || secretId == 10510 || secretId == 10501 || secretId == 10500, "Unexpected property secret.");
            bool warrior = secretId == 10524 || secretId == 10535 || secretId == 10505 || secretId == 10510 || secretId == 10501 || secretId == 10500;
            bool criticalSecret = secretId == 10554 || secretId == 10500;
            int activeId = warrior ? 10008 : secretId == 10521 ? 10000 : secretId == 10522 ? 10001 : secretId == 10559 ? 10005 : 10028;
            string affinityName = secretId == 10520 ? "FireDamageAdd" : secretId == 10521 ? "WaterDamageAdd" : secretId == 10522 ? "WindDamageAdd" : secretId == 10550 || secretId == 10505 ? "MaxHp" : secretId == 10555 ? "ElementMaster" : secretId == 10583 ? "ElementResistance" : secretId == 10559 ? "EffectRate" : criticalSecret ? "CritRatePercent" : secretId == 10524 ? "Attack" : secretId == 10535 ? "KongFuMaster" : secretId == 10510 ? "Speed" : secretId == 10501 ? "Defence" : "StatusDmgAddPer";
            Require(!string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath), "Missing acquired secret profile.");
            var save = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(sourcePath));
            var profile = save.Player;
            OfflineItemAutoIncrease.InitializeNewCharacter(profile, DateTime.UtcNow);
            Require(OfflineSkillProgression.OwnedRank(profile.Skills, secretId) > 0
                && profile.Skills.Plans[profile.Skills.SelectedPlan].PassiveSkills.Contains(secretId),
                "The source profile did not acquire and equip the selected secret through the original UI.");
            if (!equipped)
            {
                foreach (int skillId in new[] { 10520, 10521, 10522, 10550, 10555, 10583, 10559, 10554, 10578 })
                    VerifyOriginalWish(output, save, skillId);
                foreach (int skillId in new[] { 10513, 10561 })
                    VerifyOriginalWish(output, save, skillId);
                foreach (int skillId in new[] { 10502, 10556, 10569, 10576, 10602 })
                    VerifyOriginalWish(output, save, skillId, 11, 2);
                foreach (int skillId in new[] { 10500, 10501, 10505, 10507, 10510, 10524, 10535,
                    10537, 10539, 10544, 10546, 10571 })
                    VerifyOriginalWish(output, save, skillId, 1);
            }
            if (!equipped)
                profile.Skills.Plans[profile.Skills.SelectedPlan].PassiveSkills = new int[4];
            if (!profile.Skills.Skills.Any(value => value.ClassId == activeId))
                profile.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = activeId, Rank = 1 });
            profile.Skills.Plans[profile.Skills.SelectedPlan].ActiveSkills = new[] { activeId, 0, 0, 0 };
            profile.InitialWeaponType = warrior
                ? Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.SwordInitialWeaponType
                : Zjcs.Offline.CharacterCreation.CharacterCreationOfflineService.WandInitialWeaponType;
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
            Require(player.Skills.Any(value => value.ItemId == activeId), "The actual battle omitted the saved elemental skill.");
            int elementAffinity = secretId == 10524 ? 1 : combat.PropertyType(affinityName);
            long affinity = PropertyOrResonance(player, elementAffinity, secretId);
            int critValueProperty = combat.PropertyType("CritRatePercentValue");
            long secondary = criticalSecret ? player.Properties.Where(value => value.Id == critValueProperty).Sum(value => value.Value) : 0;
            if (secretId == 10550 || secretId == 10505)
                Require(affinity > 0 && player.MaxHp == affinity && player.Hp == affinity,
                    "The actual battle did not initialize health from the equipped secret.");
            var enemy = checkpoint.Units.First(value => value.Side != player.Side && value.Wave == 0);
            long enemySpeed = 900;
            if (secretId == 10510)
            {
                var item = combatAssets.LoadSkillItems().Required(secretId);
                int rank = OfflineSkillProgression.OwnedRank(profile.Skills, secretId);
                int level = progression.SkillProgression.SlotLevel(profile.Skills, 101, 0, profile.SeasonRank);
                var additions = combat.CalculatePassiveSkillProps(item.PassiveRankPropId, item.PassiveGroupLevelPropId,
                    item.PassiveRankFactorId, item.PassivePropFactors, rank, level, profile.SubRank);
                Require(additions.TryGetValue(elementAffinity, out long speedBonus) && speedBonus >= 2,
                    "The original speed secret cannot create a turn-order boundary.");
                long baseline = affinity - (equipped ? speedBonus : 0);
                enemySpeed = baseline + speedBonus / 2;
                player.Turn.RankSpeedScale = 1;
                enemy.Turn.RankSpeedScale = 1;
            }
            checkpoint.Units.RemoveAll(value => value != player && value != enemy);
            checkpoint.Waves.Clear();
            checkpoint.Waves.Add(new FightBattleWaveState { Index = 0, Spawned = true, UnitIds = new List<int> { enemy.Id } });
            checkpoint.MaxRound = 5; checkpoint.IsPlayerRoundLimit = false; checkpoint.PlayerRoundLimit = 0;
            // The original boss occupies five cells across; keep its body clear of
            // the player while retaining the real stage and a target within the original skill range.
            enemy.X = player.X + (warrior ? 3 : 5); enemy.Y = player.Y;
            if (secretId == 10578)
            {
                foreach (var value in new[] { new[] { 24020, 900001 }, new[] { 24215, 900002 } })
                {
                    var status = combat.CreateStatus(value[0], enemy.Id, player.Id, value[1]);
                    status.DurationRound = 5;
                    enemy.Statuses.Add(status);
                }
            }
            if (secretId == 10583 || secretId == 10501)
            {
                // Exercise the original elemental formula against an incoming fireball
                // while retaining the native weekly stage and battle flow.
                enemy.Skills.Clear(); enemy.SkillState.Slots.Clear();
                var incoming = combat.CreateSkill(10800, 0, 10028, 2, 2, enemy.SubRank,
                    combat.CalculateSkillProps(10800, 2, 2, enemy.SubRank)
                        .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                enemy.Skills.Add(incoming); enemy.SkillState.Slots.Add(combat.CreateSkillSlot(incoming, 0));
            }
            foreach (var unit in checkpoint.Units)
            {
                var props = new Dictionary<int, long> { { 1, secretId == 10501 && unit.Id == enemy.Id ? 10000 : 1000 }, { 2, 10 }, { 3, 1000000 },
                    { 4, unit.Id == 1 ? 1000 : enemySpeed }, { combat.PropertyType("BaseElementAdd"), 10000 },
                    { combat.PropertyType("BaseElementReduce"), 10000 }, { combat.PropertyType("BaseElementMaster"), 10000 },
                    { combat.PropertyType("BaseElementResistance"), 10000 }, { combat.PropertyType("BaseKongFuMaster"), 10000 },
                    { combat.PropertyType("BaseKongFuResistance"), 10000 }, { combat.PropertyType("BaseEffectRate"), 10000 },
                    { combat.PropertyType("BaseEffectDodge"), 10000 }, { combat.PropertyType("BaseCritRatePercentValue"), 10000 },
                    { combat.PropertyType("BaseCritAvoidPercentValue"), 10000 }, { combat.PropertyType("BaseBlockPercentValue"), 10000 },
                    { combat.PropertyType("BaseBlockAvoidPercentValue"), 10000 } };
                if (unit.Id == 1 && secretId != 10578 && affinity != 0) props[elementAffinity] = affinity;
                if (unit.Id == 1 && criticalSecret && secondary != 0) props[critValueProperty] = secondary;
                OfficialBattleUnitAssembler.Initialize(combatAssets.LoadDamageFormula(), unit, props, 3, 4);
            }
            double effectRate = secretId == 10559
                ? combat.CalculateStatusApplicationRate(player, enemy, 24020, .3, true, combatAssets.LoadDamageFormula()) : 0d;
            var weekly = new OfflineWeeklyChallengeSession(Path.Combine(saveRoot, "saves", slot, "weekly-challenge-save.json"));
            weekly.StartBattle(4800, weekly.CurrentPeriodStartLocalTicks(DateTime.Now), FightBattleCheckpointSerializer.Save(checkpoint));
            FightBattleCheckpoint settled = null;
            var runner = home.StartWeeklyChallengeBattle(4800, value => settled = value);
            var controller = runner.Controller;
            if (secretId == 10578)
                affinity = PropertyOrResonance(controller.Checkpoint.Units.Single(value => value.Id == player.Id), elementAffinity, secretId);
            long damage = 0;
            int firstActor = 0;
            int diskChecks = 0;
            FightBattleCheckpoint pendingDisk = null;
            controller.StepCompleted += step =>
            {
                if (secretId == 10510 && firstActor == 0 && step.Event.ActorRound > 0)
                    firstActor = step.Event.ActorId;
                var hit = step.Event.DamageEvents.FirstOrDefault(value => !value.IsCure && value.Damage < 0
                    && (secretId == 10583 || secretId == 10501
                        ? value.SourceId == enemy.Id && value.TargetId == player.Id && value.SkillItemId == 10028
                        : value.SourceId == player.Id && value.TargetId == enemy.Id && value.SkillItemId == activeId));
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
                    long expectedAffinity = PropertyOrResonance(pendingDisk.Units.Single(value => value.Id == player.Id), elementAffinity, secretId);
                    long savedAffinity = PropertyOrResonance(restored.Units.Single(value => value.Id == player.Id), elementAffinity, secretId);
                    long savedSecondary = criticalSecret ? restored.Units.Single(value => value.Id == player.Id).Properties
                        .Where(value => value.Id == critValueProperty).Sum(value => value.Value) : 0;
                    Require(restored.Sequence == pendingDisk.Sequence && restored.Counter == pendingDisk.Counter
                        && savedAffinity == expectedAffinity && (diskChecks != 0 || savedAffinity == affinity)
                        && (!criticalSecret || savedSecondary == secondary),
                        "Battle checkpoint lost its receipt or elemental affinity.");
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
            if (secretId == 10510)
                Require(firstActor == (equipped ? player.Id : enemy.Id), "The speed secret did not change the first turn.");
            var reloaded = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
            Require(JsonUtility.ToJson(reloaded.Player.Skills) == originalSkills, "Battle changed the saved skill plan.");
            File.WriteAllText(Path.Combine(output, "blazing-heart-battle.json"),
                JsonUtility.ToJson(new Evidence { equipped = equipped, affinity = affinity, secondary = secondary, damage = damage, effectRate = effectRate, diskChecks = diskChecks, firstActor = firstActor }, true));
            yield return capture("blazing-heart-return");
            home.CloseForNavigation();
            Debug.Log("[Blazing heart battle] equipped=" + equipped + " affinity=" + affinity
                + " skill=" + activeId + " damage=" + damage + " disk checks=" + diskChecks);
        }

        private static long PropertyOrResonance(FightBattleUnitState unit, int propertyId, int secretId)
        {
            if (secretId != 10578) return unit.Properties.Where(value => value.Id == propertyId).Sum(value => value.Value);
            return unit.Statuses.Where(value => value.ClassId == 25047 && value.StatusProperties != null)
                .SelectMany(value => value.StatusProperties).Where(value => value.Id == propertyId).Sum(value => value.Value);
        }

        private static void VerifyOriginalWish(string output, OfflineMainlineSave source, int skillId,
            int profession = 10, int professionRank = 1)
        {
            var rules = Resources.Load<TextAsset>("Wishes/wishes-runtime");
            Require(rules != null, "The original skill wish catalog is missing.");
            var catalog = WishRuntimeCatalog.Parse(rules.text);
            Require(catalog.SkillAvailable(skillId, profession), "The original profession wish pool excludes secret " + skillId + ".");
            var save = JsonUtility.FromJson<OfflineMainlineSave>(JsonUtility.ToJson(source));
            save.Player.MaxProfessionRank = professionRank;
            save.Player.Skills.Skills.RemoveAll(value => value.ClassId == skillId);
            foreach (var plan in save.Player.Skills.Plans)
                for (int index = 0; index < plan.PassiveSkills.Length; index++)
                    if (plan.PassiveSkills[index] == skillId) plan.PassiveSkills[index] = 0;
            save.Player.Inventory.RemoveAll(value => value.ItemId == 110);
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 110, Count = 10000 });
            save.Player.Wishes.RandomState = 123456789;
            string root = Path.Combine(output, "wish-save-" + Guid.NewGuid().ToString("N"));
            var slots = new ProfileSlotCatalog(root);
            string slot = slots.CreateSlot();
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var wish = new OfflineWishService(session, catalog);
            Require(wish.AvailableDrawProfessions(0, profession).Contains(profession), "The original profession wish pool is not selectable.");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int draws = 0;
            bool received = false;
            while (!received && draws < 2000)
            {
                var result = wish.Draw(0, 100, profession, "passive-affinity-wish-" + skillId + "-" + draws, now, true);
                Require(result.IsApplied && result.Awards.Count == 100, "The original skill wish transaction failed: " + result.Message);
                draws += 100;
                received = result.Awards.Any(value => value.OriginItemId == skillId && value.ConvertedItemId == 0);
            }
            Require(received && OfflineSkillProgression.OwnedRank(session.Save.Player.Skills, skillId) == 1
                && session.InventoryCount(110) == 10000 - draws, "The original wish pool did not grant the unowned secret and consume its cost.");
            var restored = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(OfflineSkillProgression.OwnedRank(restored.Save.Player.Skills, skillId) == 1
                && restored.InventoryCount(110) == 10000 - draws, "The wish result did not survive profile reload.");
            Debug.Log("[Passive affinity wish] item=" + skillId + " granted after " + draws + " draws.");
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
