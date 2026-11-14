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
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Character
{
    public sealed partial class SkillPlayerValidation
    {
        private IEnumerator RunPassivePropertyFlow()
        {
            deadline = Time.realtimeSinceStartup + 240f;
            AudioListener.volume = 0f;
            int itemId = int.Parse(Environment.GetEnvironmentVariable("ZJCS_SKILLS_PASSIVE_ITEM") ?? "10500");
            int profession = int.Parse(Environment.GetEnvironmentVariable("ZJCS_SKILLS_PASSIVE_PROFESSION") ?? "1");
            Require(new[] { 1, 2, 6, 10, 11, 15 }.Contains(profession), "Passive fixture profession is outside the requested scope.");
            while (GameObject.Find("OfficialSaveSelectionPanel") == null) yield return null;
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var skillRules = assets.LoadSkillRules();
            var definition = skillRules.Skills.Single(value => value.ClassId == itemId);
            Require(definition.ItemType == 101, "Passive fixture must use an original secret item.");
            int rank = definition.RankCosts.Select(value => value.CurrentRank).DefaultIfEmpty(definition.MaximumRank).Min();
            var slots = new ProfileSlotCatalog(saveRoot);
            string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave();
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "秘法验证";
            save.Player.Level = 50;
            save.Player.Rank = 2;
            save.Player.SubRank = 5;
            save.Player.MaxProfessionRank = 2;
            save.Player.Wardrobe.Profession = profession;
            save.Player.Experience = assets.Load().CumulativeLevelExperience[50];
            save.Player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = itemId, Rank = rank });
            foreach (var cost in skillRules.SlotLevelCosts.Single(value => value.Level == 2).Items)
                save.Player.Inventory.Add(new OfflineItemStack { ItemId = cost.ItemId, Count = cost.Count });
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            foreach (Canvas other in FindObjectsOfType<Canvas>()) other.gameObject.SetActive(false);
            foreach (Camera other in FindObjectsOfType<Camera>()) other.enabled = false;
            var page = new GameObject("PassivePropertyCharacterEntry", typeof(RectTransform));
            var runtime = page.AddComponent<CharacterFeatureRuntime>();
            runtime.OpenProfile(slot, saveRoot);
            var progression = runtime.Progression;
            Require(OfflineSkillProgression.IsAvailableFor(definition, progression.SkillContext()), "Original profession rejects the fixture secret.");
            var panel = page.GetComponentInChildren<CharacterSkillPanel>(true);
            yield return null;
            Debug.Log("[Passive property viewport] screen=" + Screen.width + "x" + Screen.height
                + " safeArea=" + Screen.safeArea + " canvas=" + page.GetComponent<Canvas>().pixelRect
                + " root=" + ((RectTransform)page.transform).rect);
            Rect viewport = page.GetComponent<Canvas>().pixelRect;
            Require(Mathf.Abs(viewport.width - Screen.width) < .5f && Mathf.Abs(viewport.height - Screen.height) < .5f,
                "The passive property canvas does not cover the game render surface.");
            Click(ButtonNamed("CharacterPanelSkillBtn"));
            yield return new WaitForSecondsRealtime(.8f);
            Click(ButtonContaining("PassiveSkillTab"));
            yield return new WaitForSecondsRealtime(.5f);
            Require(progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].PassiveSkills.All(id => id == 0), "Fresh fixture has equipped secrets.");
            Click(ButtonNamed("SkillListItem_" + itemId));
            yield return new WaitForSecondsRealtime(.6f);
            yield return new WaitForEndOfFrame(); Capture("passive-detail");
            Click(ButtonNamed("EquipBtn"));
            yield return new WaitForSecondsRealtime(.4f);
            var group = FindObjectsOfType<RectTransform>().Single(value => value.name == "PassiveSkill" && value.parent.name == "SkillContentPanel");
            Click(group.GetChild(0).Find("Slot").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.4f);
            VerifyPassivePropertyFlow(progression, slots, slot, itemId, rank, 1);
            yield return new WaitForEndOfFrame(); Capture("passive-equipped");
            Click(group.GetChild(0).Find("Slot").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.4f);
            Click(ButtonNamed("UpgradeBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            Click(ButtonNamed("SkillPromotePanelLevelUpTabGroupItem_11"));
            yield return new WaitForSecondsRealtime(.3f);
            Click(FindPointerTarget(value => value.name == "UseItemBtn"
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "LevelUpContent"), "passive cultivation cost"));
            yield return new WaitForSecondsRealtime(.7f);
            VerifyPassivePropertyFlow(progression, slots, slot, itemId, rank, 2);
            yield return new WaitForEndOfFrame(); Capture("passive-cultivated");
            Require(panel.HandleEscape(), "Original cultivation window did not close.");
            yield return null;
            runtime.ReloadProfile();
            yield return new WaitForSecondsRealtime(.4f);
            VerifyPassivePropertyFlow(runtime.Progression, slots, slot, itemId, rank, 2);
            yield return new WaitForEndOfFrame(); Capture("passive-reloaded");
            File.WriteAllText(Path.Combine(output, "passive-save.json"), File.ReadAllText(slots.GetProgressPath(slot)));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result { passed = true }, true));
            Debug.Log("[Passive property flow] Original secret " + itemId + " installed, cultivated and reloaded through pointer input; battle property wiring passed.");
            Application.logMessageReceived -= CaptureFailure;
            Destroy(page);
            yield return null;
            Application.Quit(0);
        }

        private void VerifyPassivePropertyFlow(OfflineCharacterProgressionService progression, ProfileSlotCatalog slots,
            string slot, int itemId, int rank, int level)
        {
            var player = progression.Player;
            int season = progression.SkillContext().CurrentSeasonId;
            var plan = player.Skills.Plans[player.Skills.SelectedPlan];
            Require(plan.PassiveSkills[0] == itemId && plan.PassiveSkills.Count(id => id > 0) == 1,
                "Original slot input did not equip exactly the selected secret.");
            Require(progression.SkillProgression.SlotLevel(player.Skills, 101, 0, season) == level,
                "Original passive cultivation did not persist the expected level.");
            var disk = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot))).Player;
            Require(disk.Skills.Plans[disk.Skills.SelectedPlan].PassiveSkills.SequenceEqual(plan.PassiveSkills)
                && OfflineSkillProgression.OwnedRank(disk.Skills, itemId) == rank
                && progression.SkillProgression.SlotLevel(disk.Skills, 101, 0, season) == level,
                "Original equipped secret or cultivation differs on disk.");
            var combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            var combat = combatAssets.Load();
            var items = combatAssets.LoadSkillItems();
            var item = items.Required(itemId);
            var expected = combat.CalculatePassiveSkillProps(item.PassiveRankPropId, item.PassiveGroupLevelPropId,
                item.PassiveRankFactorId, item.PassivePropFactors, rank, level, player.SubRank);
            Require(expected.Count > 0, "Selected original secret has no property contribution.");
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var snapshot = progression.PropertyCalculator.Evaluate(player, now);
            var unequipped = JsonUtility.FromJson<OfflinePlayerProgress>(JsonUtility.ToJson(player));
            unequipped.Skills.Plans[unequipped.Skills.SelectedPlan].PassiveSkills[0] = 0;
            var withoutSecret = progression.PropertyCalculator.Evaluate(unequipped, now).OutOfFightBattleProps;
            var baseProps = new Dictionary<int, long>(snapshot.OutOfFightBattleProps);
            Require(baseProps.Count == withoutSecret.Count && withoutSecret.All(pair => baseProps.TryGetValue(pair.Key, out long value) && value == pair.Value),
                "The out-of-fight properties already include the secret before battle assembly.");
            var actual = new Dictionary<int, long>(baseProps);
            OfficialBattlePassivePropertyAssembler.ApplyEquippedPlayerPassives(combat, items, actual, player, progression);
            var merged = new Dictionary<int, long>(baseProps);
            foreach (var pair in expected) merged[pair.Key] = (merged.TryGetValue(pair.Key, out long old) ? old : 0) + pair.Value;
            Require(actual.Count == merged.Count && merged.All(pair => actual.TryGetValue(pair.Key, out long value) && value == pair.Value),
                "Production battle assembler omitted or duplicated the equipped secret.");
            var display = new OfflineSkillPropSource(progression.Catalog, progression.SkillRules).CalculateProps(player);
            Require(display.Count == expected.Count && expected.All(pair => display.TryGetValue(pair.Key, out long value) && value == pair.Value),
                "Character property source differs from battle secret cultivation.");
            var registered = progression.EvaluateProps(now)
                .Chunks[CharacterPropHandleType.Skill];
            Require(registered.Count == expected.Count && expected.All(pair => registered.TryGetValue(pair.Key, out long value) && value == pair.Value),
                "The registered character property pipeline omitted the equipped secret.");
            File.WriteAllText(Path.Combine(output, "passive-properties-level-" + level + ".json"),
                "{" + string.Join(",", actual.OrderBy(pair => pair.Key).Select(pair => "\"" + pair.Key + "\":" + pair.Value)) + "}");
        }
    }
}
