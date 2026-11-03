using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialSharedBattleReportAcceptance
    {
        private static IEnumerator ExerciseDropdown(OfficialBattleReportView view, FightBattleCheckpoint checkpoint,
            List<int> populatedSkillUnits, string output, int pass)
        {
            var report = FightBattleReportBuilder.Build(checkpoint);
            var own = FightBattleReportGrouping.Build(report, 1).Players;
            var groups = own.Concat(report.Units.Select(value => value.Side).Distinct()
                .Where(value => value != 1).SelectMany(side => FightBattleReportGrouping.Build(report, side).Players)).ToArray();
            Require(groups.Length >= 2, "The all-player report needs a real opposing player for selection validation.");
            var dropdown = view.Root.GetComponentInChildren<WorldUIDropdown>();
            Require(dropdown != null && dropdown.ItemsKey.Count == groups.Length,
                "The native dropdown lost a completed-battle player.");
            var catalog = OfficialCombatAssetCatalog.LoadDefault().LoadSkillItems();
            for (int index = 0; index < groups.Length; index++)
            {
                Click(dropdown.DropdownButton);
                yield return new WaitForSecondsRealtime(.4f);
                var row = dropdown.MyScrollRect.GetComponentsInChildren<WorldDropdownItem>()
                    .Single(value => value.Texts.Any(text => text.GetComponent<TMPro.TMP_Text>().text.Contains("UI fixture " + groups[index].TargetId + "<")));
                populatedSkillUnits.Clear();
                Click(row.Button);
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture(output, "shared-report-player-selection-" + pass + "-" + index);
                Require(dropdown.SelectedItemIndex == index, "Simulated dropdown row click selected the wrong report owner.");
                var owner = groups[index];
                var unitIds = new HashSet<int>(owner.Normal.Concat(owner.Summon).Select(value => value.UnitId));
                int count = report.Skills.Count(skill => unitIds.Contains(skill.UnitId)
                    && (!catalog.TryGet(skill.SkillItemId, out var item) || item.UserType != 4));
                var scroll = view.Root.GetComponentsInChildren<WorldBattleReportSkillScrollSync>().Single()
                    .GetComponentInChildren<UnityEngine.UI.LoopVerticalScrollRect>();
                Require(scroll.totalCount == count && populatedSkillUnits.All(unitIds.Contains),
                    "Switching players retained another owner's skills or lost the selected owner's statistics.");
                Require(!dropdown.Animator.GetBool(dropdown.AnimationWorkCondition),
                    "Selecting a player did not dismiss the original dropdown.");
            }
            Click(dropdown.DropdownButton);
            yield return new WaitForSecondsRealtime(.4f);
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {
                position = new Vector2(1, 1) };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count != 0, "The original popup mask did not receive outside pointer input.");
            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer,
                UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.4f);
            Require(!dropdown.Animator.GetBool(dropdown.AnimationWorkCondition),
                "Outside pointer input did not dismiss the original player dropdown.");
        }
    }
}
