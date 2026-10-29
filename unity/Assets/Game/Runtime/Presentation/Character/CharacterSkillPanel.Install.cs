using System;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private void ToggleOriginalInstallMask(bool active)
        {
            var root = AuxObject(screen["root"]);
            var content = FindOriginalComponent(root, "CharacterPanelSkillContent");
            var mask = nodes[Convert.ToString(content["mMask"], CultureInfo.InvariantCulture)];
            mask.gameObject.SetActive(active);
            installMask = active ? mask.gameObject : null;
            PublicPlayerInfoVisibilityChanged?.Invoke(!active);
            if (active)
            {
                mask.SetAsLastSibling();
                mask.parent.SetAsLastSibling();
                var group = pendingType == ActiveSkillItemType ? activeGroup : passiveGroup;
                group.SetAsLastSibling();
                var cancel = nodes[Convert.ToString(content["mExitMaskBtn"], CultureInfo.InvariantCulture)].GetComponent<Button>();
                cancel.onClick.RemoveListener(CancelOriginalInstall);
                cancel.onClick.AddListener(CancelOriginalInstall);
            }
            else
            {
                var binding = FindOriginalComponent(root, "CharacterPanelSkillPanel");
                nodes[Convert.ToString(binding["mBottomObj"], CultureInfo.InvariantCulture)].SetAsLastSibling();
            }
        }

        private void CancelOriginalInstall()
        {
            ClearPendingInstall();
            Refresh();
        }

        private int OriginalSlotBaseLimit()
        {
            int rank = progression.Player.SubRank < 2 ? progression.Player.SubRank
                : 2 + (progression.Player.SubRank - 2) / 3;
            foreach (var row in progression.SkillRules.BaseLevelLimits)
                if (row.SubRank == rank) return row.SkillLevel;
            return 0;
        }

        private void RefreshOriginalSlotLevel(Transform slot, int itemType, int index)
        {
            var source = FindLayoutNode(AuxObject(screen["root"]), OriginalNodePath(slot));
            var binding = FindOriginalComponent(source, "CharacterPanelSkillItem");
            int level = progression.SkillProgression.SlotLevel(progression.Player.Skills, itemType, index,
                progression.SkillContext().CurrentSeasonId);
            var label = nodes[Convert.ToString(binding["LevelText"], CultureInfo.InvariantCulture)];
            SetAuxiliaryText(label, string.Format(CultureInfo.InvariantCulture, OriginalDetailText("ui_common_level_style_3"), level));
            bool blessing = level > OriginalSlotBaseLimit();
            RefreshOriginalBless(binding, "CurLevelTextView", nodes, blessing);
            var drag = FindOriginalComponent(source, "CharacterPanelSkillItemDragContent");
            nodes[Convert.ToString(drag["Hightlight"], CultureInfo.InvariantCulture)].gameObject.SetActive(
                pendingSkillId != 0 && pendingType == itemType);
        }

        private void RefreshOriginalBless(Dictionary<string, object> binding, string field,
            Dictionary<string, RectTransform> windowNodes, bool blessing)
        {
            object raw = binding;
            foreach (string key in field.Split('.')) raw = AuxObject(raw)[key];
            var view = AuxObject(raw);
            SetOriginalComponentState(binding, field + ".mIsBlessStateSwitcher", blessing ? 1 : 0, windowNodes);
            foreach (object path in (List<object>)view["mBlessTxt"])
                OfficialSkillNativeTextStyle.Bless(windowNodes[Convert.ToString(path, CultureInfo.InvariantCulture)], blessing);
        }

        private void RefreshOriginalIconBless(Transform icon, int level)
        {
            var source = AuxObject(itemTemplate["root"]);
            string rootPath = Convert.ToString(source["path"]);
            var iconNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            foreach (var node in AuxiliaryNodes(source))
            {
                string path = Convert.ToString(node["path"]);
                iconNodes[path] = (path == rootPath ? icon : icon.Find(path.Substring(rootPath.Length + 1))) as RectTransform;
            }
            RefreshOriginalBless(FindOriginalComponent(source, "ItemIconSkill"), "CurLevelTextView", iconNodes,
                level > OriginalSlotBaseLimit());
        }

        private string OriginalNodePath(Transform target)
        {
            foreach (var pair in nodes) if (pair.Value == target) return pair.Key;
            throw new InvalidOperationException("The original skill slot binding is absent.");
        }
    }
}
