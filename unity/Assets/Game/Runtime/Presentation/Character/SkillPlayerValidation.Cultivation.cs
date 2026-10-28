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
            Click(ButtonNamed("SkillPromotePanelLevelUpTabGroupItem_11")); yield return null;
            Button CostButton(string group) => FindPointerTarget(value => value.name == "UseItemBtn"
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == group), group + " cost action");
            Click(CostButton("LevelUpContent"));
            yield return new WaitForSecondsRealtime(.6f);
            Require(progression.SkillProgression.SlotLevel(progression.Player.Skills, 100, 0, progression.SkillContext().CurrentSeasonId) == oldLevel + 1,
                "The Player slot cultivation click did not apply its original cost and level.");
            yield return new WaitForEndOfFrame(); Capture("skills-slot-cultivation");
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
            yield return new WaitForEndOfFrame(); Capture("skills-rank-result");
            Require(panel.HandleEscape(), "The Player rank result did not close."); yield return null;
            var restored = OfflineMainlineProgressSession.InspectCharacter(slots, slot).Save.Player.Skills;
            Require(OfflineSkillProgression.OwnedRank(restored, rankSkill) == oldRank + 1
                && restored.Plans[restored.SelectedPlan].ActiveSkills.SequenceEqual(progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills),
                "The Player promotion and dragged plan did not survive disk reload.");
        }
    }
}
