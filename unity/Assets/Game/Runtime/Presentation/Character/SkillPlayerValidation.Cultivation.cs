using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Character
{
    public sealed partial class SkillPlayerValidation
    {
        private IEnumerator ValidateCultivationAndInput(OfflineCharacterProgressionService progression,
            CharacterSkillPanel panel, ProfileSlotCatalog slots, string slot)
        {
            var input = FindObjectsOfType<TMP_InputField>().Single(value => value.name == "SkillPlanName");
            string accepted = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].Name;
            foreach (string candidate in new[] { "中文六字方案", "中文七字方案名", "abc def", "abcdefghijkl", accepted }) {
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, input.transform.position), button = PointerEventData.InputButton.Left };
                var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Require(hits.Count > 0 && (hits[0].gameObject == input.gameObject || hits[0].gameObject.transform.IsChildOf(input.transform)), "The Player plan input is obstructed.");
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                yield return null;
                Require(input.isFocused, "The Player plan input did not focus after its pointer click.");
                input.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, modifiers = EventModifiers.Control });
                foreach (char character in candidate) input.ProcessEvent(new Event { type = EventType.KeyDown, character = character });
                Click(ButtonNamed("NextPlanBtn")); yield return null;
                Click(ButtonNamed("PrevPlanBtn")); yield return null;
                if (progression.SkillProgression.CheckPlanName(candidate) == 0) accepted = candidate;
                var disk = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
                Require(input.text == accepted && disk.Player.Skills.Plans[disk.Player.Skills.SelectedPlan].Name == accepted,
                    "The Player keyboard plan name validation or disk restore differs from the original rules.");
            }
            yield return new WaitForEndOfFrame(); Capture("skills-plan-input");
            RectTransform[] ActiveSlots() => FindObjectsOfType<RectTransform>().Single(value => value.name == "ActiveSkill")
                .Cast<Transform>().OfType<RectTransform>().Where(value => value.name.StartsWith("SkillContentPanelSkillGroupItem", StringComparison.Ordinal)).ToArray();
            var plan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            int first = Array.FindIndex(plan.ActiveSkills, value => value > 0);
            int second = Array.FindIndex(plan.ActiveSkills, first + 1, value => value > 0);
            Require(first >= 0 && second >= 0, "The Player recommendation has fewer than two equipped active skills to drag.");
            int firstId = plan.ActiveSkills[first], secondId = plan.ActiveSkills[second];
            var cells = ActiveSlots();
            var source = cells[first].gameObject; var target = cells[second].gameObject;
            var drag = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, cells[first].Find("Slot").position) };
            var dragHits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(drag, dragHits);
            Require(dragHits.Count > 0 && dragHits[0].gameObject.transform.IsChildOf(source.transform), "The Player drag source is obstructed.");
            ExecuteEvents.ExecuteHierarchy(dragHits[0].gameObject, drag, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(source, drag, ExecuteEvents.beginDragHandler);
            Require(FindObjectsOfType<RectTransform>().Any(value => value.name == "SkillDragGhost"), "The Player slot drag did not create the original icon ghost.");
            drag.position = RectTransformUtility.WorldToScreenPoint(null, target.transform.position);
            ExecuteEvents.Execute(source, drag, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(source, drag, ExecuteEvents.endDragHandler);
            yield return null;
            plan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            Require(plan.ActiveSkills[first] == secondId && plan.ActiveSkills[second] == firstId, "The Player simulated slot drag did not swap skills.");

            void Supply(OfflineSkillItemCount[] costs) {
                foreach (var cost in costs) {
                    var stack = progression.Player.Inventory.FirstOrDefault(value => value.ItemId == cost.ItemId);
                    if (stack == null) progression.Player.Inventory.Add(new OfflineItemStack { ItemId = cost.ItemId, Count = cost.Count });
                    else stack.Count = Math.Max(stack.Count, cost.Count);
                }
            }
            int oldLevel = progression.SkillProgression.SlotLevel(progression.Player.Skills, 100, 0, progression.SkillContext().CurrentSeasonId);
            Supply(progression.SkillRules.SlotLevelCosts.First(value => value.Level == oldLevel + 1).Items);
            Click(ActiveSlots()[0].Find("Slot").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.4f);
            Click(ButtonNamed("UpgradeBtn")); yield return null;
            Button CultivationSlot(int index) => FindPointerTarget(value => value.name == "Slot"
                && value.transform.parent.name == "SkillContentPanelSkillGroupItem" + (index == 0 ? "" : " (" + index + ")")
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "SkillPromotePanel"), "cultivation slot " + index);
            int unchangedLevel = progression.SkillProgression.SlotLevel(progression.Player.Skills, 100, 1, progression.SkillContext().CurrentSeasonId);
            Click(CultivationSlot(1)); yield return new WaitForSecondsRealtime(.4f);
            Require(progression.SkillProgression.SlotLevel(progression.Player.Skills, 100, 1, progression.SkillContext().CurrentSeasonId) == unchangedLevel,
                "Selecting a cultivation slot spent resources or upgraded it.");
            Click(CultivationSlot(0)); yield return new WaitForSecondsRealtime(.4f);
            Click(ButtonNamed("SkillPromotePanelLevelUpTabGroupItem_11")); yield return null;
            Button CostButton(string group) => FindPointerTarget(value => value.name == "UseItemBtn"
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == group), group + " cost action");
            var material = FindPointerTarget(value => value.transform.parent != null && value.transform.parent.name == "MaterialGrid"
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "LevelUpContent"), "cultivation currency material");
            Click(material); yield return new WaitForSecondsRealtime(.5f);
            Require(FindObjectsOfType<RectTransform>().Any(value => value.name == "ItemDetailDialog"),
                "Clicking the original cultivation currency did not open its item detail.");
            yield return new WaitForEndOfFrame(); Capture("skills-cultivation-material");
            Require(panel.HandleEscape(), "The cultivation material detail did not close."); yield return null;
            Click(CostButton("LevelUpContent"));
            yield return new WaitForSecondsRealtime(.6f);
            Require(progression.SkillProgression.SlotLevel(progression.Player.Skills, 100, 0, progression.SkillContext().CurrentSeasonId) == oldLevel + 1,
                "The Player slot cultivation click did not apply its original cost and level.");
            yield return new WaitForEndOfFrame(); Capture("skills-slot-cultivation");
            string selectedName = FindObjectsOfType<TMP_Text>().Single(value => value.name == "NameText"
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "SkillPromotePanel")).text;
            Click(ButtonNamed("NextBtn")); yield return new WaitForSecondsRealtime(.4f);
            Require(FindObjectsOfType<TMP_Text>().Single(value => value.name == "NameText"
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "SkillPromotePanel")).text != selectedName,
                "Cultivation group navigation did not switch from active to passive skills.");
            Click(ButtonNamed("PrevBtn")); yield return new WaitForSecondsRealtime(.4f);
            Require(FindObjectsOfType<TMP_Text>().Single(value => value.name == "NameText"
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "SkillPromotePanel")).text == selectedName,
                "Cultivation group navigation did not restore the selected active skill.");
            Require(panel.HandleEscape(), "The Player cultivation window did not close.");
            yield return null;
            int rankSkill = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills[0];
            int oldRank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, rankSkill);
            Supply(progression.SkillDefinition(rankSkill).RankCosts.First(value => value.CurrentRank == oldRank).Items);
            Click(ActiveSlots()[0].Find("Slot").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.4f);
            Click(ButtonNamed("UpgradeBtn")); yield return null;
            Click(ButtonNamed("SkillPromotePanelRankUpTabGroupItem_11"));
            yield return new WaitForSecondsRealtime(.6f);
            Click(CostButton("RankUpContent")); yield return new WaitForSecondsRealtime(.8f);
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, rankSkill) == oldRank + 1,
                "The Player rank promotion click did not update its equipped skill.");
            int finalRank = oldRank + 1;
            int Quality(int rank) => progression.SkillRules.RankDisplays.First(value => value.Rank == rank).Quality;
            while (Quality(finalRank) == Quality(oldRank))
            {
                Require(!FindObjectsOfType<RectTransform>().Any(value => value.name == "SkillRankUpNotifyDialog"),
                    "Same-quality star promotion opened a quality notification.");
                yield return new WaitForEndOfFrame(); Capture("skills-rank-star");
                Supply(progression.SkillDefinition(rankSkill).RankCosts.First(value => value.CurrentRank == finalRank).Items);
                Click(ButtonNamed("SkillPromotePanelRankUpTabGroupItem_11"));
                yield return new WaitForSecondsRealtime(.4f);
                Click(CostButton("RankUpContent")); yield return new WaitForSecondsRealtime(.8f);
                finalRank++;
                Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, rankSkill) == finalRank,
                    "The Player consecutive rank promotion did not consume its configured cost.");
            }
            var notification = FindObjectsOfType<RectTransform>().Single(value => value.name == "SkillRankUpNotifyDialog");
            var labels = notification.GetComponentsInChildren<TMP_Text>(true);
            Require(labels.Single(value => value.name == "FromRankText").text != labels.Single(value => value.name == "ToRankText").text,
                "The quality notification did not bind its actual previous and next quality.");
            Require(!labels.Single(value => value.name == "SkillNameText").text.Contains(" +", StringComparison.Ordinal)
                && notification.GetComponentsInChildren<RectTransform>().Count(value => value.name == "PlayerPropItemVariant") > 0,
                "The rank notification retained its name or property template placeholders.");
            Require(!FindObjectsOfType<Text>().Any(value => value.isActiveAndEnabled && value.text.Contains("<nobr>", StringComparison.Ordinal)
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "SkillPromotePanel")),
                "The cultivation material retained a TMP template tag in a legacy label.");
            yield return new WaitForEndOfFrame(); Capture("skills-rank-result");
            Require(panel.HandleEscape(), "The Player rank result did not close."); yield return null;
            Require(panel.HandleEscape(), "The Player cultivation window beneath the result did not close."); yield return null;
            var restored = OfflineMainlineProgressSession.InspectCharacter(slots, slot).Save.Player.Skills;
            Require(OfflineSkillProgression.OwnedRank(restored, rankSkill) == finalRank
                && restored.Plans[restored.SelectedPlan].ActiveSkills.SequenceEqual(progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills),
                "The Player promotion and dragged plan did not survive disk reload.");
            int rainbowRank = progression.SkillRules.RankDisplays.First(value => value.Quality == 7).Rank;
            Require(progression.SkillDefinition(rankSkill).MaximumRank >= rainbowRank, "The rainbow cultivation fixture is unavailable.");
            progression.Player.Skills.Skills.Single(value => value.ClassId == rankSkill).Rank = rainbowRank - 1;
            Supply(progression.SkillDefinition(rankSkill).RankCosts.First(value => value.CurrentRank == rainbowRank - 1).Items);
            panel.ReloadProfile(); yield return null;
            Click(ActiveSlots()[0].Find("Slot").GetComponent<Button>()); yield return new WaitForSecondsRealtime(.4f);
            Click(ButtonNamed("UpgradeBtn")); yield return null;
            Click(ButtonNamed("SkillPromotePanelRankUpTabGroupItem_11")); yield return new WaitForSecondsRealtime(.6f);
            var rainbow = FindObjectsOfType<Zjcs.Presentation.Skills.OfficialSkillQualityTmpGradient>().Single(value => value.isActiveAndEnabled);
            var rainbowText = rainbow.GetComponent<TMP_Text>(); rainbowText.ForceMeshUpdate();
            Require(rainbowText.textInfo.meshInfo[0].colors32.Distinct().Count() > 2, "The native rainbow preview did not retain its vertex gradient.");
            yield return new WaitForEndOfFrame(); Capture("skills-rainbow-preview");
            Click(CostButton("RankUpContent")); yield return new WaitForSecondsRealtime(.8f);
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, rankSkill) == rainbowRank,
                "The rainbow quality promotion did not apply its configured cost and rank.");
            yield return new WaitForEndOfFrame(); Capture("skills-rainbow-result");
            Require(panel.HandleEscape(), "The rainbow notification did not close."); yield return null;
            Require(panel.HandleEscape(), "The rainbow cultivation did not close."); yield return null;
            yield return ValidateUnequippedCultivation(progression, panel);
            yield return ValidateOriginalInstall(progression, panel);
            int retainedLevel = progression.Player.Skills.ActiveSlotLevels[0];
            var context = progression.SkillContext();
            int playerCap = progression.SkillRules.LevelLimits.First(value => value.SubRank == context.SubRank
                && value.PlayerLevel == context.BaseLevel && value.PlayerBlessLevel == context.PlayerBlessLevel).SkillLevel;
            progression.Player.Skills.ActiveSlotLevels[0] = playerCap;
            panel.ReloadProfile(); yield return null;
            Click(ActiveSlots()[0].Find("Slot").GetComponent<Button>()); yield return new WaitForSecondsRealtime(.4f);
            Click(ButtonNamed("UpgradeBtn")); yield return new WaitForSecondsRealtime(.5f);
            var nextPlayerLimit = progression.SkillProgression.NextSkillLevelLimit(context, playerCap);
            Require(nextPlayerLimit != null && FindObjectsOfType<TMP_Text>().Any(value => value.name == "TxtText"
                && value.transform.parent.name == "CannotMakeObj" && value.isActiveAndEnabled
                && value.text.Contains((nextPlayerLimit.PlayerLevel + nextPlayerLimit.PlayerBlessLevel).ToString())),
                "The original cultivation player level limit message was not bound.");
            yield return new WaitForEndOfFrame(); Capture("skills-cultivation-level-limit");
            Require(panel.HandleEscape(), "The limited cultivation window did not close."); yield return null;
            progression.Player.Skills.ActiveSlotLevels[0] = retainedLevel;
            panel.ReloadProfile(); yield return null;
            yield return ValidateOriginalBlessing(progression, panel);
        }

        private IEnumerator ValidateUnequippedCultivation(OfflineCharacterProgressionService progression, CharacterSkillPanel panel)
        {
            var plan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            var extra = progression.SkillRules.Skills.Where(value => value.ItemType == 100
                && OfflineSkillProgression.IsAvailableFor(value, progression.SkillContext())
                && !plan.ActiveSkills.Contains(value.ClassId)).Take(9).ToArray();
            Require(extra.Length == 9, "The unequipped cultivation fixture needs a full scroll list.");
            foreach (var definition in extra)
                if (!progression.Player.Skills.Skills.Any(value => value.ClassId == definition.ClassId))
                    progression.Player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = definition.ClassId, Rank = 1 });
            int selected = extra.Last().ClassId;
            foreach (var cost in extra.First().RankCosts.First(value => value.CurrentRank == 1).Items)
            {
                var stack = progression.Player.Inventory.FirstOrDefault(value => value.ItemId == cost.ItemId);
                if (stack == null) progression.Player.Inventory.Add(new OfflineItemStack { ItemId = cost.ItemId, Count = cost.Count });
                else stack.Count = Math.Max(stack.Count, cost.Count);
            }
            panel.ReloadProfile(); yield return null;
            var button = FindObjectsOfType<Button>().Single(value => value.name == "SkillListItem_" + selected);
            var mainScroll = button.GetComponentInParent<ScrollRect>();
            for (int step = 0; step <= 20; step++)
            {
                mainScroll.verticalNormalizedPosition = 1f - step / 20f; Canvas.ForceUpdateCanvases();
                var position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                if (RectTransformUtility.RectangleContainsScreenPoint(mainScroll.viewport, position)) break;
            }
            Click(ButtonNamed("SkillListItem_" + selected)); yield return new WaitForSecondsRealtime(.5f);
            Click(ButtonNamed("UpgradeBtn")); yield return new WaitForSecondsRealtime(.6f);
            var native = FindObjectsOfType<LoopHorizontalScrollRect>().Single(value => value.isActiveAndEnabled);
            Require(native.totalCount >= 9 && native.content.name == "LeftSkillGrid", "Unequipped cultivation did not use the original horizontal virtual list.");
            var title = FindObjectsOfType<TMP_Text>().Single(value => value.name == "NameText"
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "SkillPromotePanel"));
            string previousName = title.text;
            Click(ButtonNamed("LeftPopTipBtn")); yield return new WaitForSecondsRealtime(.6f);
            Require(title.text != previousName && native.ItemTypeStart == 0,
                "The offscreen promotion hint did not scroll and select its target skill.");
            yield return new WaitForEndOfFrame(); Capture("skills-unequipped-cultivation");
            var drag = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, native.viewport.TransformPoint(native.viewport.rect.center)) };
            ExecuteEvents.Execute(native.gameObject, drag, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(native.gameObject, drag, ExecuteEvents.beginDragHandler);
            for (int step = 0; step < 8; step++)
            {
                drag.position += new Vector2(-Screen.width / 10f, 0f);
                ExecuteEvents.Execute(native.gameObject, drag, ExecuteEvents.dragHandler); yield return null;
            }
            ExecuteEvents.Execute(native.gameObject, drag, ExecuteEvents.endDragHandler);
            yield return new WaitForSecondsRealtime(.6f);
            Require(native.ItemTypeStart > 0 || native.content.anchoredPosition.x < -100f,
                "Simulated dragging did not move the original horizontal cultivation list.");
            yield return new WaitForEndOfFrame(); Capture("skills-unequipped-cultivation-drag");
            Require(panel.HandleEscape(), "The unequipped cultivation window did not close."); yield return null;
        }
    }
}
