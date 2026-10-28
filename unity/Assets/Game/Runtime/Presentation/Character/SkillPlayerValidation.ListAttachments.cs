using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;

namespace Zjcs.Presentation.Character
{
    public sealed partial class SkillPlayerValidation
    {
        private IEnumerator ValidateListAttachments(OfflineCharacterProgressionService progression, CharacterSkillPanel panel)
        {
            Transform Row(int id) => FindObjectsOfType<Transform>().Single(value => value.name == "SkillListItem_" + id);
            bool Attachment(int id, int type) => Row(id).GetComponentsInChildren<Transform>()
                .Any(value => value.name == "OriginalSkillAttachment_" + type);
            Require(!progression.IsNewSessionItem(10015) && !Attachment(10015, 4), "An existing Player login skill is marked new.");
            Require(Attachment(10000, 13) && Attachment(10000, 8)
                && Row(10000).GetComponentsInChildren<TMP_Text>().Any(value => value.text.Contains("合成")),
                "The Player merge row lost its original localized hint or red dot.");
            yield return new WaitForEndOfFrame(); Capture("skills-list-merge-attachment");
            Click(ButtonNamed("SkillListItem_10000"));
            yield return new WaitForSecondsRealtime(.6f);
            Click(ButtonNamed("MergeBtn"));
            yield return new WaitForSecondsRealtime(.4f);
            Require(progression.IsNewSessionItem(10000) && Attachment(10000, 4) && !Attachment(10000, 13),
                "The Player merge click did not replace its hint with the original new-item mark.");
            yield return new WaitForEndOfFrame(); Capture("skills-list-new-attachment");
            Click(ButtonNamed("SkillListItem_10000"));
            yield return new WaitForSecondsRealtime(.6f);
            Require(!progression.IsNewSessionItem(10000) && !Attachment(10000, 4), "The Player skill click did not clear its new mark.");
            Click(FindObjectsOfType<Button>().Single(value => value.name == "BlurMask" && value.isActiveAndEnabled), new Vector2(8f, Screen.height * .5f));
            yield return new WaitForSecondsRealtime(.3f);
            // Isolated fixture only: add an incompatible owned skill to inspect
            // the original all-skills filter and profession attachment in Player.
            int mismatch = progression.SkillRules.Skills.First(value => value.ItemType == 100
                && !OfflineSkillProgression.IsAvailableFor(value, progression.SkillContext())).ClassId;
            progression.Player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = mismatch, Rank = 1 });
            panel.ReloadProfile();
            yield return null;
            var all = FindObjectsOfType<Toggle>().Single(value => value.name == "ShowAll");
            if (!all.isOn) Click(all);
            yield return null;
            var grid = FindObjectsOfType<RectTransform>().First(value => value.name == "skillGrid" && value.gameObject.activeInHierarchy);
            var scroll = grid.GetComponentInParent<ScrollRect>();
            var row = Row(mismatch);
            bool visible = false;
            for (int step = 0; step <= 20 && !visible; step++) {
                scroll.verticalNormalizedPosition = 1f - step / 20f;
                Canvas.ForceUpdateCanvases();
                Vector2 position = RectTransformUtility.WorldToScreenPoint(null, row.position);
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
                visible = RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, position)
                    && hits.Count > 0 && (hits[0].gameObject == row.gameObject || hits[0].gameObject.transform.IsChildOf(row));
            }
            Require(visible && Attachment(mismatch, 10) && Row(mismatch).GetComponentsInChildren<TMP_Text>().Any(value => value.text.Contains("职业")),
                "The Player all-skills scroll did not display its original localized profession attachment.");
            yield return new WaitForEndOfFrame(); Capture("skills-list-profession-attachment");
            if (all.isOn) Click(all);
            yield return null;
            yield return new WaitForEndOfFrame(); Capture("skills-list-filter-restored");
        }

        private static void Click(Toggle control)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)control.transform;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && (hits[0].gameObject == control.gameObject || hits[0].gameObject.transform.IsChildOf(control.transform)),
                "The Player toggle is obstructed: " + control.name);
            ExecuteEvents.Execute(control.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(control.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(control.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
