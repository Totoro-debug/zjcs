using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Skills;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private Dictionary<string, object> originalDetailTexts;
        private OfficialSkillDetailProperties detailProperties;
        private Dictionary<string, RectTransform> currentDetailNodes;
        private int currentDetailSkillId;

        private string OriginalDetailText(string key)
        {
            if (originalDetailTexts == null)
                originalDetailTexts = CharacterModelPackage.AsObject(OfficialEquipmentJson.Parse(skillAssets.FidelityTexts.text), "original skill text");
            if (originalDetailTexts.TryGetValue(key, out object value)) return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (skillUiTemplates.TryGetValue("auxiliary_data", out object auxiliary)
                && CharacterModelPackage.AsObject(auxiliary, "auxiliary text").TryGetValue("texts", out object texts)
                && CharacterModelPackage.AsObject(texts, "auxiliary text values").TryGetValue(key, out value))
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            return null;
        }

        private void BindOriginalSkillDetail(int skillId, Dictionary<string, RectTransform> dialogNodes, bool bindActions = true,
            FightBattleSkillDefinition snapshot = null, string templateKey = "skill_panel_detail")
        {
            currentDetailNodes = dialogNodes;
            currentDetailSkillId = skillId;
            int effectSkillId = snapshot == null ? OriginalEffectSkillId(skillId) : snapshot.UseItemId > 0 ? snapshot.UseItemId : skillId;
            var template = CharacterModelPackage.AsObject(skillUiTemplates[templateKey], "skill detail");
            var root = CharacterModelPackage.AsObject(template["root"], "skill detail root");
            var itemBinding = FindOriginalComponent(root, "ItemDetailDialog");
            var skillBinding = FindOriginalComponent(root, "SkillDetailDialog");
            RectTransform Bound(Dictionary<string, object> binding, string field) =>
                binding.TryGetValue(field, out object path) && path is string key && dialogNodes.TryGetValue(key, out var node) ? node : null;
            bool Description(string field, string key)
            {
                RectTransform node = Bound(itemBinding, field);
                if (node == null) throw new InvalidOperationException("Original detail text binding is absent: " + field);
                string value = OriginalDetailText(key);
                node.gameObject.SetActive(value != null);
                if (value != null) {
                    var text = node.GetComponent<TMP_Text>();
                    text.text = RestoreOriginalMidSize(value, text.fontSize);
                    BindOriginalSkillHyperlinks(effectSkillId, text);
                }
                return value != null;
            }
            bool function = Description("FuncDescText", "item_" + effectSkillId + "_func_desc");
            bool description = Description("DescText", "item_" + effectSkillId + "_desc");
            Bound(itemBinding, "DetailGo").gameObject.SetActive(function || description);
            int rank = snapshot?.Rank ?? Math.Max(1, OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId));
            int quality = progression.SkillRules.RankDisplays.First(value => value.Rank == rank).Quality;
            string[] names = { "None", "White", "Blue", "Purple", "Orange", "Gold", "Red", "Rainbow" };
            Text qualityText = Bound(itemBinding, "QualityText").GetComponent<Text>();
            qualityText.text = OriginalDetailText("Quality." + names[quality]);
            if (qualityGradient == null) qualityGradient = skillAssets.LoadQualityGradient();
            if (qualityGradient.normalColors == null || qualityGradient.normalColors.Length <= quality)
                throw new InvalidOperationException("Original normal quality text colors are absent.");
            qualityText.color = qualityGradient.normalColors[quality];
            ApplyQualityGradient(qualityText, quality, 45f);
            var backgrounds = CharacterModelPackage.AsObject(skillUiTemplates["detail_quality_backgrounds"], "original quality backgrounds");
            string uri = Convert.ToString(backgrounds[names[quality]], CultureInfo.InvariantCulture);
            Image qualityBg = Bound(itemBinding, "QualityBg").GetComponent<Image>();
            qualityBg.sprite = skillAssets.FindSprite(uri);
            if (qualityBg.sprite == null) throw new InvalidOperationException("Original skill detail quality background is missing: " + uri);

            var definition = progression.SkillDefinition(skillId);
            var planData = progression.Player.Skills.Plans[plan];
            int[] slots = definition.ItemType == ActiveSkillItemType ? planData.ActiveSkills : planData.PassiveSkills;
            int index = Array.IndexOf(slots, skillId);
            int season = progression.SkillContext().CurrentSeasonId;
            int level = index >= 0 ? progression.SkillProgression.SlotLevel(progression.Player.Skills, definition.ItemType, index, season)
                : new[] { ActiveSkillItemType, PassiveSkillItemType }.SelectMany(type => Enumerable.Range(0, 4)
                    .Select(slot => progression.SkillProgression.SlotLevel(progression.Player.Skills, type, slot, season))).Min();
            if (snapshot != null) level = snapshot.Level;
            var levelText = Bound(skillBinding, "LevelText").GetComponent<TMP_Text>();
            levelText.text = RestoreOriginalMidSize(string.Format(CultureInfo.InvariantCulture,
                OriginalDetailText("ui_common_level_style_10"), level), levelText.fontSize);
            int subRank = snapshot?.SubRank ?? progression.Player.SubRank;
            int playerRank = subRank < 2 ? subRank : 2 + (subRank - 2) / 3;
            int baseLimit = progression.SkillRules.BaseLevelLimits.FirstOrDefault(value => value.SubRank == playerRank)?.SkillLevel ?? 0;
            SetOfficialState(templateKey, "SkillDetailDialog", "mBlessLevelView.mIsBlessStateSwitcher", level > baseLimit ? 1 : 0, dialogNodes);
            Bound(skillBinding, "mBlessLevelRuleBtn").gameObject.SetActive(level > baseLimit);
            Bound(skillBinding, "mBlessLevelRuleBtn").GetComponent<Button>().onClick.AddListener(() =>
                OpenBlessLevelRule(baseLimit, level, RectTransformUtility.WorldToScreenPoint(null,
                    Bound(skillBinding, "mBlessLevelRuleBtn").position)));
            Bound(skillBinding, "SkillTypeText").GetComponent<Text>().text = OriginalDetailText(
                definition.ItemType == ActiveSkillItemType ? "ItemType.ActiveSkill" : "ItemType.PassiveSkill");
            SetOfficialState(templateKey, "SkillDetailDialog", "mSkillBgIconStateSwithcer", definition.ItemType, dialogNodes);
            BindOriginalDetailStars(root, dialogNodes, rank);
            if (bindActions) {
                BindOriginalDetailSources(skillId, root, dialogNodes);
                BindOriginalSkillSkinButton(skillId, root, dialogNodes);
            }
            BindOriginalDetailTags(effectSkillId, root, dialogNodes);
            BindOriginalDetailProfession(effectSkillId, root, dialogNodes);
            RectTransform range = Bound(skillBinding, "RangeUI");
            var detailData = CharacterModelPackage.AsObject(skillUiTemplates["detail_data"], "original skill detail data");
            bool showRange = ((List<object>)detailData["active_skill_types"]).Any(value => Convert.ToInt32(value) == definition.ItemType);
            range.gameObject.SetActive(showRange);
            if (showRange) StartCoroutine(BindOriginalDetailRange(effectSkillId, root, dialogNodes));
            // ItemDetailDialogUseResult.TryInit only accepts reward/chest items.
            Bound(itemBinding, "mItemDetailDialogUseResult").gameObject.SetActive(false);
            SetOfficialState(templateKey, "ItemDetailDialog", "mRemainTimeStateSwitcher", 0, dialogNodes);
            var addition = FindOriginalComponent(root, "ItemDetailDialogAddition");
            if (addition != null) dialogNodes[Convert.ToString(addition["mAdditionDetailButton"])].gameObject.SetActive(false);
            if (detailProperties == null) detailProperties = new OfficialSkillDetailProperties(progression.Catalog, skillAssets);
            var props = detailProperties.Calculate(effectSkillId, rank, level, Math.Max(1, subRank));
            var propPanel = FindOriginalComponent(root, "SkillPanelPropPanel");
            var propPaths = (List<object>)propPanel["mSkillPropItems"];
            for (int i = 0; i < propPaths.Count; i++)
            {
                RectTransform row = dialogNodes[Convert.ToString(propPaths[i])];
                row.gameObject.SetActive(i < props.Count);
                if (i >= props.Count) continue;
                SetTextValue(FindText(row, "Key"), props[i].Name);
                SetTextValue(FindText(row, "Value"), props[i].Value);
            }
        }

        private void BindOriginalDetailTags(int skillId, Dictionary<string, object> root, Dictionary<string, RectTransform> dialogNodes)
        {
            var data = CharacterModelPackage.AsObject(skillUiTemplates["detail_data"], "original skill detail data");
            var skills = CharacterModelPackage.AsObject(data["skills"], "original skill tags");
            var skill = CharacterModelPackage.AsObject(skills[skillId.ToString(CultureInfo.InvariantCulture)], "skill tags");
            var tags = (List<object>)skill["tags"];
            var binding = FindOriginalComponent(root, "SkillDetailDialog");
            dialogNodes[Convert.ToString(binding["SkillTagGridLayout"])].gameObject.SetActive(tags.Count != 0);
            var slotBinding = FindOriginalComponent(root, "SkillTagGo");
            RectTransform slot = dialogNodes[Convert.ToString(slotBinding["SkillTagSlot"])];
            foreach (Transform child in slot)
                if (child.gameObject.activeSelf) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var template = CharacterModelPackage.AsObject(skillUiTemplates["skill_tag"], "original skill tag prefab");
            var itemBinding = FindOriginalComponent(CharacterModelPackage.AsObject(template["root"], "tag root"), "SkillTagItem");
            var configs = ((List<object>)itemBinding["TagCfgs"]).Select(value => CharacterModelPackage.AsObject(value, "tag colors")).ToArray();
            var names = CharacterModelPackage.AsObject(data["tag_names"], "original tag enum");
            foreach (object rawTag in tags)
            {
                int tag = Convert.ToInt32(rawTag);
                var config = configs.First(value => ((List<object>)value["Tags"]).Any(candidate => Convert.ToInt32(candidate) == tag));
                var itemNodes = new Dictionary<string, RectTransform>();
                RectTransform item = factory.Build(CharacterPanelNodeFactory.ReadRoot(template), slot, itemNodes, new Dictionary<string, Button>());
                item.gameObject.SetActive(true);
                itemNodes[Convert.ToString(itemBinding["Bg"])].GetComponent<Image>().color = OriginalDetailColor(config["BgColor"]);
                RectTransform label = itemNodes[Convert.ToString(itemBinding["Text"])];
                string text = OriginalDetailText("ui_skill_tag_" + Convert.ToString(names[tag.ToString(CultureInfo.InvariantCulture)]));
                if (label.TryGetComponent(out TMP_Text nativeText))
                {
                    nativeText.text = text;
                    nativeText.color = OriginalDetailColor(config["TextColor"]);
                }
                else
                {
                    label.GetComponent<Text>().text = text;
                    label.GetComponent<Text>().color = OriginalDetailColor(config["TextColor"]);
                }
            }
        }

        // SkillDetailDialogRangeUI.RefreshView/AddItems: defer until the original
        // layout has resolved, then populate in descending Y / ascending X order.
        private IEnumerator BindOriginalDetailRange(int skillId, Dictionary<string, object> root, Dictionary<string, RectTransform> dialogNodes)
        {
            yield return null;
            var binding = FindOriginalComponent(root, "SkillDetailDialogRangeUI");
            RectTransform gridNode = dialogNodes[Convert.ToString(binding["mGridLayoutGroup"])];
            if (gridNode == null) yield break;
            var data = CharacterModelPackage.AsObject(skillUiTemplates["detail_data"], "original range data");
            var ranges = CharacterModelPackage.AsObject(data["ranges"], "original ranges");
            if (!ranges.TryGetValue(skillId.ToString(CultureInfo.InvariantCulture), out object rawRange))
                throw new InvalidOperationException("The original skill range is absent: " + skillId);
            var range = (List<object>)rawRange;
            int diameter = Convert.ToInt32(range[0]);
            var center = (List<object>)range[1];
            var positions = (List<object>)range[2];
            var types = (List<object>)range[3];
            var positionsToType = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < positions.Count; i++)
            {
                var pos = (List<object>)positions[i];
                positionsToType.Add(new Vector2Int(Convert.ToInt32(pos[0]), Convert.ToInt32(pos[1])), Convert.ToInt32(types[i]));
            }
            var info = CharacterModelPackage.AsObject(data["range_grid"], "original range grid");
            var grid = gridNode.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = gridNode.gameObject.AddComponent<GridLayoutGroup>();
            var padding = CharacterModelPackage.AsObject(info["m_Padding"], "original range padding");
            grid.padding = new RectOffset(Convert.ToInt32(padding["m_Left"]), Convert.ToInt32(padding["m_Right"]),
                Convert.ToInt32(padding["m_Top"]), Convert.ToInt32(padding["m_Bottom"]));
            grid.childAlignment = (TextAnchor)Convert.ToInt32(info["m_ChildAlignment"]);
            grid.startCorner = (GridLayoutGroup.Corner)Convert.ToInt32(info["m_StartCorner"]);
            grid.startAxis = (GridLayoutGroup.Axis)Convert.ToInt32(info["m_StartAxis"]);
            grid.constraint = (GridLayoutGroup.Constraint)Convert.ToInt32(info["m_Constraint"]);
            grid.constraintCount = diameter;
            float spacing = Convert.ToSingle(binding["mSpacingScale"], CultureInfo.InvariantCulture);
            float size = gridNode.rect.width / (diameter + (diameter - 1) * spacing);
            grid.cellSize = new Vector2(size, size);
            grid.spacing = new Vector2(size * spacing, size * spacing);
            var colors = ((List<object>)binding["mRangeUIInfos"]).Select(value => CharacterModelPackage.AsObject(value, "range colors"))
                .ToDictionary(value => Convert.ToInt32(value["RangeType"]));
            var template = CharacterModelPackage.AsObject(skillUiTemplates["skill_range_item"], "original range item");
            var itemBinding = FindOriginalComponent(CharacterModelPackage.AsObject(template["root"], "range root"), "SkillDetailDialogRangeUIItem");
            int half = diameter / 2, cx = Convert.ToInt32(center[0]), cy = Convert.ToInt32(center[1]);
            for (int y = half + cy; y > half - diameter + cy; y--)
                for (int x = -half + cx; x < diameter - half + cx; x++)
                {
                    positionsToType.TryGetValue(new Vector2Int(x, y), out int type);
                    var itemNodes = new Dictionary<string, RectTransform>();
                    RectTransform item = factory.Build(CharacterPanelNodeFactory.ReadRoot(template), gridNode, itemNodes, new Dictionary<string, Button>());
                    item.gameObject.SetActive(true);
                    itemNodes[Convert.ToString(itemBinding["mBgImage"])].GetComponent<Image>().color = OriginalDetailColor(colors[type]["BgColor"]);
                    itemNodes[Convert.ToString(itemBinding["mImage"])].GetComponent<Image>().color = OriginalDetailColor(colors[type]["Color"]);
                }
        }

        private static Color OriginalDetailColor(object raw)
        {
            var value = CharacterModelPackage.AsObject(raw, "original detail color");
            return new Color(Convert.ToSingle(value["r"]), Convert.ToSingle(value["g"]), Convert.ToSingle(value["b"]), Convert.ToSingle(value["a"]));
        }

        private void BindOriginalDetailProfession(int skillId, Dictionary<string, object> root, Dictionary<string, RectTransform> dialogNodes)
        {
            var data = CharacterModelPackage.AsObject(skillUiTemplates["detail_data"], "original profession data");
            string key = skillId.ToString(CultureInfo.InvariantCulture);
            var skills = CharacterModelPackage.AsObject(data["skills"], "original skill users");
            var skill = CharacterModelPackage.AsObject(skills[key], "original skill user");
            var skillBinding = FindOriginalComponent(root, "SkillDetailDialog");
            RectTransform container = dialogNodes[Convert.ToString(skillBinding["ItemDetailDialogProfessionIcon"])];
            bool player = Convert.ToInt32(skill["user_type"]) == 1;
            container.gameObject.SetActive(player);
            if (!player) return;
            var owners = CharacterModelPackage.AsObject(data["skill_professions"], "original skill professions");
            int profession = owners.TryGetValue(key, out object owner) ? Convert.ToInt32(owner) : 0;
            var professions = CharacterModelPackage.AsObject(data["professions"], "original professions");
            var row = CharacterModelPackage.AsObject(professions[profession.ToString(CultureInfo.InvariantCulture)], "original profession");
            var chain = new HashSet<int> { profession };
            int previous = Convert.ToInt32(row["previous"]);
            while (previous != 0 && chain.Add(previous))
                previous = Convert.ToInt32(CharacterModelPackage.AsObject(professions[previous.ToString(CultureInfo.InvariantCulture)], "previous profession")["previous"]);
            Sprite icon = skillAssets.FindSprite(Convert.ToString(row["icon"]));
            if (icon == null) throw new InvalidOperationException("Original skill profession icon is absent: " + profession);
            var binding = FindOriginalComponent(root, "ItemDetailDialogProfessionIcon");
            foreach (object raw in (List<object>)binding["ProfessionIconCfgs"])
            {
                var config = CharacterModelPackage.AsObject(raw, "original profession icon");
                bool active = chain.Contains(Convert.ToInt32(config["Profession"]));
                dialogNodes[Convert.ToString(config["Obj"])].gameObject.SetActive(active);
                if (active) dialogNodes[Convert.ToString(config["Icon"])].GetComponent<Image>().sprite = icon;
            }
        }

        private void BindOriginalDetailStars(Dictionary<string, object> root, Dictionary<string, RectTransform> dialogNodes, int rank)
        {
            var star = FindOriginalComponent(root, "StarMiniInfo");
            var list = dialogNodes[Convert.ToString(star["StarListSlot"])];
            var display = progression.SkillRules.RankDisplays.First(value => value.Rank == rank);
            int maxRank = progression.SkillRules.RankDisplays.Where(value => value.Quality == display.Quality).Max(value => value.RankAddition);
            var dictionary = CharacterModelPackage.AsObject(star["mQualitySpriteDict"], "quality stars");
            var records = ((List<object>)dictionary["mSerializeBuffer"]).Select(value => CharacterModelPackage.AsObject(value, "quality star")).ToArray();
            var record = records.FirstOrDefault(value => Convert.ToInt32(value["Key"]) == display.Quality)
                ?? records.First(value => Convert.ToInt32(value["Key"]) == 0);
            string uri = Convert.ToString(CharacterModelPackage.AsObject(record["Value"], "star sprite")["file"]);
            Sprite sprite = skillAssets.FindSprite(uri);
            if (sprite == null) throw new InvalidOperationException("Original skill detail star sprite is missing: " + uri);
            list.gameObject.SetActive(true);
            Transform number = list.parent.Find("StarNum");
            if (number != null) number.gameObject.SetActive(false);
            for (int i = 0; i < list.childCount; i++)
            {
                Transform background = list.GetChild(i);
                background.gameObject.SetActive(i < display.RankAddition || i < maxRank);
                if (background.childCount == 0) continue;
                Image image = background.GetChild(0).GetComponent<Image>();
                image.sprite = sprite;
                image.gameObject.SetActive(i < display.RankAddition);
            }
        }

        private static string RestoreOriginalMidSize(string source, float baseSize)
        {
            return Regex.Replace(source, @"<midsize=([0-9]+(?:\.[0-9]+)?)>", match => {
                float size = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                float offset = (baseSize - size) * .5f * (38.04f / 40f);
                return "<voffset=" + (offset / Mathf.Max(1f, baseSize)).ToString("0.#####", CultureInfo.InvariantCulture)
                    + "em><size=" + size.ToString("0.###", CultureInfo.InvariantCulture) + ">";
            }).Replace("</midsize>", "</size></voffset>");
        }
    }
}
