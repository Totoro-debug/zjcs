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
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        // Only the explicit diagnostic supplies materials/profession. Acquisition,
        // equipment, cultivation and combat all use their normal production paths.
        private static IEnumerator ExercisePortableFlame(string slot, string root, string output,
            Action<Selectable> click)
        {
            AudioListener.volume = 0f;
            click = Click;
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var rules = assets.LoadSkillRules();
            var definition = rules.Skills.Single(value => value.ClassId == 10061);
            var slots = new ProfileSlotCatalog(root);
            var save = new OfflineMainlineSave();
            var player = save.Player;
            player.CharacterCreated = true; player.CharacterName = "烈焰流程验证";
            player.Level = 50; player.Rank = 2; player.SubRank = 5; player.MaxProfessionRank = 2;
            player.Experience = assets.Load().CumulativeLevelExperience[50];
            player.InitialWeaponType = 200; player.Wardrobe.Profession = 6;
            int mergeCount = assets.Load().SkillMergePiece(10061);
            Require(mergeCount > 0 && definition.AvailableProfessions.Contains(6), "Original fire-ring acquisition inputs missing.");
            void Supply(int item, long count)
            {
                var stack = player.Inventory.SingleOrDefault(value => value.ItemId == item);
                if (stack == null) player.Inventory.Add(new OfflineItemStack { ItemId = item, Count = count });
                else stack.Count += count;
            }
            Supply(definition.PieceItemId, mergeCount);
            foreach (var cost in definition.RankCosts.Single(value => value.CurrentRank == 1).Items) Supply(cost.ItemId, cost.Count);
            foreach (var cost in rules.SlotLevelCosts.Single(value => value.Level == 2).Items) Supply(cost.ItemId, cost.Count);
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var home = HomeFeatureRuntime.Begin(slot, root, null);
            home.TutorialAutoStartEnabled = false;
            yield return new WaitForSecondsRealtime(1f);
            click(home.Hud.CanvasTransform.GetComponentsInChildren<Selectable>().Single(value =>
                value.name == "UnlockObj" && value.transform.parent.name == "Character"));
            yield return new WaitForSecondsRealtime(.8f);
            Canvas.ForceUpdateCanvases();
            var pageCanvas = FindObjectsOfType<Canvas>().Single(value => value.name == "HomeCharacterPage");
            Require(Mathf.Abs(pageCanvas.pixelRect.width - Screen.width) < 1f
                && Mathf.Abs(pageCanvas.pixelRect.height - Screen.height) < 1f,
                "The native character canvas and rendered input surface have different sizes: " + pageCanvas.pixelRect);
            yield return CaptureFlameFlow(output, "character");
            click(FlameFlowButton(value => value.name == "CharacterPanelSkillBtn"));
            yield return new WaitForSecondsRealtime(.8f);
            var character = FindObjectsOfType<CharacterFeatureRuntime>().Single();
            var progression = character.Progression;
            var panel = character.GetComponentInChildren<CharacterSkillPanel>(true);
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, 10061) == 0,
                "The original merge flow must start without the skill.");
            click(FlameFlowButton(value => value.name.Contains("SkillPieceTab")));
            yield return new WaitForSecondsRealtime(.4f);
            click(FlameFlowButton(value => value.name.StartsWith("CharacterPanelSkillDetailPanelSkillPieceItem", StringComparison.Ordinal)));
            yield return new WaitForSecondsRealtime(.4f);
            yield return CaptureFlameFlow(output, "pieces");
            click(FlameFlowButton(value => value.name == "CommonBtn_12_Merge"));
            yield return new WaitForSecondsRealtime(.4f);
            yield return CaptureFlameFlow(output, "merge-confirm");
            click(FlameFlowButton(value => value.name == "MergeBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, 10061) == 1
                && progression.Player.Inventory.Where(value => value.ItemId == definition.PieceItemId).Sum(value => value.Count)
                    == definition.RankCosts.Single(value => value.CurrentRank == 1).Items
                        .Where(value => value.ItemId == definition.PieceItemId).Sum(value => value.Count),
                "Original merge did not grant one skill at its exact fragment cost.");
            click(FlameFlowButton(value => value.name.Contains("ActiveSkillTab")));
            yield return new WaitForSecondsRealtime(.4f);
            click(FlameFlowButton(value => value.name == "SkillListItem_10061"));
            yield return new WaitForSecondsRealtime(.5f);
            yield return CaptureFlameFlow(output, "acquired-detail");
            click(FlameFlowButton(value => value.name == "EquipBtn"));
            yield return new WaitForSecondsRealtime(.4f);
            var group = FindObjectsOfType<RectTransform>().Single(value =>
                value.name == "ActiveSkill" && value.parent.name == "SkillContentPanel");
            click(group.GetChild(0).Find("Slot").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.4f);
            Require(progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills[0] == 10061,
                "Original slot input did not equip the newly merged skill.");
            yield return CaptureFlameFlow(output, "equipped");
            click(group.GetChild(0).Find("Slot").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.4f);
            click(FlameFlowButton(value => value.name == "UpgradeBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            click(FlameFlowButton(value => value.name == "SkillPromotePanelLevelUpTabGroupItem_11"));
            yield return new WaitForSecondsRealtime(.3f);
            click(FlameFlowButton(value => value.name == "UseItemBtn" && value.GetComponentsInParent<Transform>()
                .Any(parent => parent.name == "LevelUpContent")));
            yield return new WaitForSecondsRealtime(.7f);
            Require(progression.SkillProgression.SlotLevel(progression.Player.Skills, 100, 0,
                progression.SkillContext().CurrentSeasonId) == 2, "Original slot cultivation did not apply.");
            yield return CaptureFlameFlow(output, "level-up");
            click(FlameFlowButton(value => value.name == "SkillPromotePanelRankUpTabGroupItem_11"));
            yield return new WaitForSecondsRealtime(.3f);
            click(FlameFlowButton(value => value.name == "UseItemBtn" && value.GetComponentsInParent<Transform>()
                .Any(parent => parent.name == "RankUpContent")));
            yield return new WaitForSecondsRealtime(.7f);
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, 10061) == 2,
                "Original rank cultivation did not apply.");
            yield return CaptureFlameFlow(output, "rank-up");
            Require(panel.HandleEscape(), "Original cultivation did not close.");
            character.ReloadProfile();
            yield return new WaitForSecondsRealtime(.5f);
            progression = character.Progression;
            var disk = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
            Require(JsonUtility.ToJson(disk.Player.Skills) == JsonUtility.ToJson(progression.Player.Skills)
                && OfflineSkillProgression.OwnedRank(disk.Player.Skills, 10061) == 2
                && disk.Player.Skills.ActiveSlotLevels[0] == 2,
                "Original acquisition, equipment or cultivation changed during disk reload.");
            yield return CaptureFlameFlow(output, "reloaded");
            Debug.Log("[Portable flame UI] Fragment acquisition, original slot input, level/rank cultivation and disk reload passed.");
            string expectedSkills = JsonUtility.ToJson(disk.Player.Skills);
            long[] inventory = disk.Player.Inventory.OrderBy(value => value.ItemId)
                .SelectMany(value => new[] { (long)value.ItemId, value.Count }).ToArray();
            click(FlameFlowButton(value => value.name == "StageDamageTestBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            var settings = FindObjectsOfType<Transform>().Single(value => value.name == "damage_test");
            foreach (string setting in new[] { "Round", "MonsterHp", "MonsterHurt" })
            {
                var choice = settings.GetComponentsInChildren<Toggle>().Single(value =>
                    value.transform.parent.name == "ToggleText" && value.GetComponentsInParent<Transform>()
                        .Any(parent => parent.name == setting));
                if (!choice.isOn) click(choice);
            }
            yield return CaptureFlameFlow(output, "settings");
            click(settings.GetComponentsInChildren<Button>().Single(value => value.name == "StartTest"));
            yield return new WaitForSecondsRealtime(1f);
            var battle = home.GetComponent<SkillDamageTestBattleRunner>();
            Require(battle && battle.Controller && home.Hud.AnyModalOpen, "Original skill-page damage test did not mount its battle.");
            var checkpoint = battle.Controller.Checkpoint;
            var actor = checkpoint.Units.Single(value => value.Id == 1);
            var active = actor.Skills.Single(value => value.Slot == 0);
            Require(active.ItemId == 10061 && active.EntityId == 11200 && active.Rank == 2 && active.Level == 2
                && active.SubRank == progression.Player.SubRank && actor.Profession == 6,
                "Production battle assembly lost the actual saved skill/profession/cultivation.");
            int casts = 0, hits = 0, states = 0;
            battle.Controller.StepCompleted += step => {
                casts += step.Event.SkillCasts.Count(value => value.ActorId == 1 && value.Slot == 0
                    && value.SkillItemId == 10061 && value.SkillEntityId == 11200);
                foreach (var hit in step.Event.DamageEvents.Where(value => value.SkillEntityId == 10860))
                {
                    Require(hit.SourceId == 1 && hit.TargetId != 1 && hit.HitEntityId == 10861
                        && hit.DamageEntityId == 10862 && hit.Damage < 0, "Acquired flame lost its original caster-owned damage.");
                    hits++;
                }
                foreach (var status in step.Checkpoint.Units.Single(value => value.Id == 1).Statuses
                    .Where(value => value.ClassId == 24210))
                {
                    Require(status.CreatorId == 1 && status.ApplyingSkillItemId == 10061
                        && status.SourceSkillRank == 2 && status.SourceSkillLevel == 2
                        && status.SourceSubRank == active.SubRank, "Acquired fire-ring status lost saved cultivation/source.");
                    states++;
                }
            };
            yield return CaptureFlameFlow(output, "preparation");
            click(FlameFlowButton(value => value.name == "Btn" && value.GetComponentsInParent<Transform>()
                .Any(parent => parent.name == "StartBtn")));
            bool captured = false;
            while (battle != null)
            {
                if (!captured && hits > 0)
                {
                    yield return new WaitForSecondsRealtime(.7f);
                    yield return CaptureFlameFlow(output, "battle"); captured = true;
                }
                yield return null;
            }
            Require(checkpoint.Finished && casts > 0 && hits > 0 && states > 0 && captured,
                "The acquired/cultivated skill did not complete its original fire-ring damage battle.");
            File.WriteAllText(Path.Combine(output, "portable-flame-ui-checkpoint.json"), FightBattleCheckpointSerializer.Save(checkpoint));
            yield return new WaitForSecondsRealtime(.8f);
            yield return CaptureFlameFlow(output, "result");
            var result = FindObjectsOfType<Transform>().Single(value => value.name == "damage_test_result");
            var report = result.GetComponent<OfficialBattleResultReport>();
            Require(report != null && report.FinalReport != null && report.FinalReport.TotalDamageDealt > 0,
                "Original damage-test result omitted actual fire-ring damage.");
            click(result.GetComponentsInChildren<Button>().Single(value => value.name == "Setting"));
            yield return new WaitForSecondsRealtime(.5f);
            settings = FindObjectsOfType<Transform>().Single(value => value.name == "damage_test");
            click(settings.GetComponentsInChildren<Button>().Single(value => value.name == "CloseBtn"));
            yield return new WaitForSecondsRealtime(.4f);
            Require(home.Hud.CharacterPageOpen, "Original damage-test return did not restore its character page.");
            disk = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
            Require(JsonUtility.ToJson(disk.Player.Skills) == expectedSkills && inventory.SequenceEqual(disk.Player.Inventory
                .OrderBy(value => value.ItemId).SelectMany(value => new[] { (long)value.ItemId, value.Count })),
                "Damage test changed the persisted cultivation, equipped plan or material counts.");
            yield return CaptureFlameFlow(output, "return");
            Debug.Log("[Portable flame UI] Saved Fighter 10061 assembled with rank=2 level=2, caster-owned status/damage, original result and return passed.");
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result { passed = true }, true));
            Destroy(home.gameObject);
            yield return null;
        }

        private static Button FlameFlowButton(Func<Button, bool> matches) => FindObjectsOfType<Button>()
            .Single(value => value.isActiveAndEnabled && value.interactable && matches(value));

        private static IEnumerator CaptureFlameFlow(string output, string phase)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "portable-flame-ui-" + phase + ".png"));
            yield return null;
        }
    }
}
