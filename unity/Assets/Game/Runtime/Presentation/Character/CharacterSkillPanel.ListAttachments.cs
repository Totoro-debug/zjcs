using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private readonly HashSet<int> originalListedNewMarkItems = new HashSet<int>();
        private int originalListBuiltFrame = -1;

        // CharacterPanelSkillListContent.Refresh / OnDisableAndInited clear the
        // previous list, including rows currently outside the viewport.
        private void ClearOriginalListNewMarks(bool onlyPresented = false)
        {
            // A local transaction can notify both the panel and its parent in
            // one frame. The second projection has not displayed its new rows
            // yet; it must not consume their newly obtained marks.
            if (progression != null && (!onlyPresented || originalListBuiltFrame < Time.frameCount))
                foreach (int itemId in originalListedNewMarkItems) progression.RemoveNewSessionItem(itemId);
            originalListedNewMarkItems.Clear();
            originalListBuiltFrame = Time.frameCount;
        }

        // CharacterPanelSkillListPanelItem.RefreshSkillIcon / RefreshSkillPieceIcon.
        // These are the original attachment prefabs and config layer order.
        private void ConfigureOriginalListAttachments(GameObject icon, int skillId, bool owned)
        {
            originalListedNewMarkItems.Add(owned ? skillId : SkillPieceId(skillId));
            Transform mount = FindDeep(icon.transform, "ItemIconAttachment");
            if (mount == null) throw new InvalidOperationException("Original skill attachment anchor is absent.");
            var template = AuxObject(listItemTemplate["root"]);
            var rows = ((List<object>)skillUiTemplates["skill_list_attachments"]).Select(AuxObject);
            bool available = IsAvailableForCurrentProfession(skillId, progression.SkillContext());
            var redDots = FindOriginalComponent(template, "ItemIconAttachmentRedDotHandler");
            var handlers = ((List<object>)redDots["mAttachmentDatas"]).Select(AuxObject).ToArray();
            bool RedDot(string originalClass) {
                switch (originalClass) {
                    case "CharacterPanelSkillListPanelItemNewMark": return owned && progression.IsNewSessionItem(skillId);
                    case "SkillPanelCanRankUpRedDotItem":
                        var binding = FindOriginalComponent(template, originalClass);
                        return owned && (Convert.ToInt32(binding["ConsiderProfession"]) == 0 || available) && CanPromoteSkill(skillId);
                    case "SkillPanelRedDotItem": return owned && OriginalSkillInstallRedDot(skillId);
                    case "SkillCanMergeRedDotItem": return !owned; // Rows without a skill are mergeable pieces.
                    case "RedDotMultiItem":
                        return ((List<object>)FindOriginalComponent(template, originalClass)["myItemClasses"])
                            .Select(Convert.ToString).Any(RedDot);
                    default: throw new InvalidOperationException("Unmigrated original skill icon red-dot binding: " + originalClass);
                }
            }
            foreach (var row in rows.OrderBy(value => Convert.ToInt32(value["layer"]))) {
                int kind = Convert.ToInt32(row["type"]);
                bool show = false;
                if (kind == 10) show = owned && !available;
                else if (kind == 13) show = !owned; // This list includes only mergeable pieces.
                else {
                    foreach (var handler in handlers.Where(value => Convert.ToInt32(value["Type"]) == kind)) {
                        show |= RedDot(Convert.ToString(handler["RedDotClass"]));
                    }
                }
                if (!show) continue;
                var source = AuxObject(skillUiTemplates[Convert.ToString(row["template"])]);
                var attachmentNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                var instance = factory.Build(CharacterPanelNodeFactory.ReadRoot(source), mount, attachmentNodes,
                    new Dictionary<string, Button>(StringComparer.Ordinal));
                instance.name = "OriginalSkillAttachment_" + kind.ToString(CultureInfo.InvariantCulture);
                instance.gameObject.SetActive(true);
                foreach (var node in AuxiliaryNodes(AuxObject(source["root"]))) {
                    if (!node.TryGetValue("original_bindings", out var raw) || !AuxObject(raw).TryGetValue("MultiLanguageText", out var textRaw)) continue;
                    string key = kind == 13 ? "ui_ItemIconSkillPiece_text1" : Convert.ToString(AuxObject(textRaw)["myKey"]);
                    SetAuxiliaryText(attachmentNodes[Convert.ToString(node["path"])], OriginalDetailText(key));
                }
                foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            }
        }

        private bool OriginalSkillInstallRedDot(int skillId)
        {
            if (!IsAvailableForCurrentProfession(skillId, progression.SkillContext())) return false;
            int kind = progression.SkillDefinition(skillId).ItemType;
            int valid = progression.Player.Skills.Skills.Count(value => progression.SkillDefinition(value.ClassId).ItemType == kind
                && IsAvailableForCurrentProfession(value.ClassId, progression.SkillContext()));
            int[] equipped = kind == ActiveSkillItemType ? progression.Player.Skills.Plans[plan].ActiveSkills
                : progression.Player.Skills.Plans[plan].PassiveSkills;
            // Preserve the pinned client's Values.Count(index => index >= 0)
            // branch, including empty entries in the unsigned online slot map.
            return equipped.Any(value => value == 0) && valid - equipped.Length > 0;
        }

        private void MarkOriginalListSkillSeen(GameObject icon, int skillId)
        {
            progression.RemoveNewSessionItem(skillId);
            var mark = FindDeep(icon.transform, "OriginalSkillAttachment_4");
            if (mark != null) mark.gameObject.SetActive(false);
        }
    }
}
