using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private GameObject petSkillDetailDialog;
        private OfficialCharacterCatalog petSkillCatalog;
        private OfficialCharacterCatalog PetSkillCatalog => petSkillCatalog ??
            (petSkillCatalog = Zjcs.Presentation.Combat.OfficialBattlePetEnergyAssembler.LoadBattlePetCatalog(progression.Catalog));

        private string PetSkillIconUri(int skillId)
        {
            var icons = CharacterModelPackage.AsObject(skillUiTemplates["pet_skill_icons"], "pet skill icons");
            return Convert.ToString(icons[skillId.ToString(CultureInfo.InvariantCulture)], CultureInfo.InvariantCulture);
        }

        private void BindPetEntitySkills(OfflinePetInstance pet, int monsterId, int subRank, Transform slot)
        {
            if (slot == null) throw new InvalidOperationException("Original pet entity skill slot is absent.");
            List<Dictionary<string, object>> evolutions = PetSkillCatalog.Rows("pet_evolution")
                .Where(row => AuxInt(row, "ClassId") == pet.ClassId).ToList();
            Dictionary<string, object> current = evolutions.FirstOrDefault(row =>
                AuxInt(row, "EvolutionPhase") == pet.EvolutionPhase);
            if (current == null) throw new InvalidOperationException("Original pet evolution row is absent: " + pet.ClassId);
            Dictionary<string, object> childhood = evolutions.OrderBy(row => AuxInt(row, "EvolutionPhase")).FirstOrDefault();

            int[] entityActive = PetSkillIds(current, "EntityActiveSkills");
            int[] entityPassive = PetSkillIds(current, "EntityPassiveSkills");
            int[] active = entityActive.Concat(PetSkillIds(childhood, "ActiveSkills")).ToArray();
            int[] passive = entityPassive.Concat(PetSkillIds(childhood, "PassiveSkills"))
                .Where(skillId => PetSkillCatalog.Find("skill", skillId) is Dictionary<string, object> skill
                    && (!skill.TryGetValue("Invisible", out object invisible) || !Convert.ToBoolean(invisible)))
                .ToArray();
            bool HasEntityLevel(int skillId) => entityActive.Contains(skillId) || entityPassive.Contains(skillId);

            foreach (int skillId in active)
                BuildPetSkillIcon(skillId, pet, monsterId, subRank, true, HasEntityLevel(skillId), slot);
            foreach (int skillId in passive)
                BuildPetSkillIcon(skillId, pet, monsterId, subRank, false, HasEntityLevel(skillId), slot);
        }

        private void BuildPetSkillIcon(int skillId, OfflinePetInstance pet, int monsterId, int subRank,
            bool active, bool hideLevel, Transform parent, bool withOuterTag = true)
        {
            Transform iconParent = parent;
            if (withOuterTag)
            {
                var wrapperTemplate = CharacterModelPackage.AsObject(skillUiTemplates["skill_outer_tag"], "pet skill tag template");
                var wrapperRoot = CharacterModelPackage.AsObject(wrapperTemplate["root"], "pet skill tag root");
                var wrapperBinding = FindOriginalComponent(wrapperRoot, "SkillWithOuterTagItem");
                var wrapperNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                var wrapperButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
                RectTransform wrapper = factory.Build(CharacterPanelNodeFactory.ReadRoot(wrapperTemplate), parent,
                    wrapperNodes, wrapperButtons);
                wrapper.gameObject.SetActive(true);

                int outerTag = PetSkillOuterTag(skillId);
                RectTransform tagNode = wrapperNodes[Convert.ToString(wrapperBinding["mSkillTagItem"], CultureInfo.InvariantCulture)];
                if (outerTag == 0)
                {
                    tagNode.gameObject.SetActive(false);
                }
                else
                {
                    Dictionary<string, object> tagBinding = FindOriginalComponent(wrapperRoot, "SkillTagItem");
                    var tagConfig = ((List<object>)tagBinding["TagCfgs"]).Select(value =>
                        CharacterModelPackage.AsObject(value, "pet skill tag color")).FirstOrDefault(value =>
                            ((List<object>)value["Tags"]).Any(candidate => Convert.ToInt32(candidate, CultureInfo.InvariantCulture) == outerTag));
                    if (tagConfig == null)
                        throw new InvalidOperationException("Original pet skill tag color is absent: " + outerTag);
                    wrapperNodes[Convert.ToString(tagBinding["Bg"], CultureInfo.InvariantCulture)]
                        .GetComponent<Image>().color = OriginalDetailColor(tagConfig["BgColor"]);
                    RectTransform label = wrapperNodes[Convert.ToString(tagBinding["Text"], CultureInfo.InvariantCulture)];
                    var detailData = CharacterModelPackage.AsObject(skillUiTemplates["detail_data"], "skill detail data");
                    var tagNames = CharacterModelPackage.AsObject(detailData["tag_names"], "skill tag names");
                    string tagName = tagNames.TryGetValue(outerTag.ToString(CultureInfo.InvariantCulture), out object rawName)
                        ? Convert.ToString(rawName, CultureInfo.InvariantCulture) : outerTag.ToString(CultureInfo.InvariantCulture);
                    SetAuxiliaryText(label, OriginalDetailText("ui_skill_tag_" + tagName) ?? tagName);
                    Graphic labelGraphic = label.GetComponent<Graphic>();
                    if (labelGraphic != null) labelGraphic.color = OriginalDetailColor(tagConfig["TextColor"]);
                    tagNode.gameObject.SetActive(true);
                }

                RectTransform itemSlot = wrapperNodes[Convert.ToString(wrapperBinding["mItemSlot"], CultureInfo.InvariantCulture)];
                iconParent = itemSlot;
            }
            var iconTemplate = CharacterModelPackage.AsObject(skillUiTemplates["pet_skill_icon"], "pet skill icon template");
            var iconRoot = CharacterModelPackage.AsObject(iconTemplate["root"], "pet skill icon root");
            var iconBinding = FindOriginalComponent(iconRoot, "ItemIconPetSkill");
            var iconNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var iconButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            RectTransform icon = factory.Build(CharacterPanelNodeFactory.ReadRoot(iconTemplate), iconParent,
                iconNodes, iconButtons);
            icon.gameObject.SetActive(true);

            int rank = Math.Max(1, PetSkillRank(pet, skillId));
            OfflineSkillRankDisplay display = progression.SkillRules.RankDisplays.FirstOrDefault(value => value.Rank == rank)
                ?? throw new InvalidOperationException("Original pet skill rank display is absent: " + rank);
            string iconUri = PetSkillIconUri(skillId);
            Sprite skillSprite = assets.FindSprite(iconUri) ?? skillAssets.FindSprite(iconUri);
            if (skillSprite == null)
                throw new InvalidOperationException("Original pet skill icon is absent: " + skillId + " " + iconUri);
            var iconProxy = CharacterModelPackage.AsObject(iconBinding["mIconProxy"], "pet skill icon image proxy");
            iconNodes[Convert.ToString(iconProxy["mImage"], CultureInfo.InvariantCulture)].GetComponent<Image>().sprite = skillSprite;
            Sprite qualitySprite = assets.FindQualityBackgroundSprite(display.Quality);
            if (qualitySprite == null)
                throw new InvalidOperationException("Original pet skill quality background is absent: " + display.Quality);
            iconNodes[Convert.ToString(iconBinding["QualityImg"], CultureInfo.InvariantCulture)].GetComponent<Image>().sprite = qualitySprite;
            iconNodes[Convert.ToString(iconBinding["QualityShadowImg"], CultureInfo.InvariantCulture)].gameObject.SetActive(true);
            iconNodes[Convert.ToString(iconBinding["BottomGo"], CultureInfo.InvariantCulture)].gameObject.SetActive(false);
            iconNodes[Convert.ToString(iconBinding["myTextGo"], CultureInfo.InvariantCulture)].gameObject.SetActive(false);
            iconNodes[Convert.ToString(iconBinding["mSelectedObj"], CultureInfo.InvariantCulture)].gameObject.SetActive(false);
            SetOriginalComponentState(iconBinding, "mPetSkillHideStateSwitcher", 1, iconNodes);
            SetOriginalComponentState(iconBinding, "mSkillTagSwitcher", 0, iconNodes);

            BindOriginalDetailStars(iconRoot, iconNodes, rank);
            var starInfo = FindOriginalComponent(iconRoot, "SkillStarInfo");
            bool showStarNumber = display.RankAddition > AuxInt(starInfo, "ShowStarThresholdValue");
            SetOriginalComponentState(starInfo, "StarStateSwitcher", showStarNumber ? 1 : 0, iconNodes);
            if (showStarNumber)
            {
                SetAuxiliaryText(iconNodes[Convert.ToString(starInfo["StarNum"], CultureInfo.InvariantCulture)],
                    display.RankAddition.ToString(CultureInfo.InvariantCulture));
                var mini = FindOriginalComponent(iconRoot, "StarMiniInfo");
                var sprites = CharacterModelPackage.AsObject(mini["mQualitySpriteDict"], "pet skill star sprites");
                var records = ((List<object>)sprites["mSerializeBuffer"]).Select(value =>
                    CharacterModelPackage.AsObject(value, "pet skill star sprite")).ToArray();
                var record = records.FirstOrDefault(value => Convert.ToInt32(value["Key"], CultureInfo.InvariantCulture) == display.Quality)
                    ?? records.First(value => Convert.ToInt32(value["Key"], CultureInfo.InvariantCulture) == 0);
                string starUri = Convert.ToString(CharacterModelPackage.AsObject(record["Value"], "pet skill star sprite value")["file"],
                    CultureInfo.InvariantCulture);
                Sprite starSprite = skillAssets.FindSprite(starUri);
                if (starSprite == null) throw new InvalidOperationException("Original pet skill star-number sprite is absent: " + starUri);
                iconNodes[Convert.ToString(starInfo["StarNumImage"], CultureInfo.InvariantCulture)].GetComponent<Image>().sprite = starSprite;
            }

            string rootPath = Convert.ToString(iconRoot["path"], CultureInfo.InvariantCulture);
            if (!iconButtons.TryGetValue(rootPath, out Button click) || click == null)
                click = icon.GetComponent<Button>();
            if (click == null) throw new InvalidOperationException("Original pet skill icon click binding is absent.");
            if (withOuterTag)
                click.onClick.AddListener(() => OpenPetSkillDetail(pet, monsterId, subRank, skillId, active, hideLevel));
        }

        private void OpenPetSkillDetail(OfflinePetInstance pet, int monsterId, int subRank, int skillId,
            bool active, bool hideLevel)
        {
            ClosePetSkillDetail();
            petSkillDetailDialog = BuildOfficialDialog("pet_skill_detail", out var dialogNodes, out _);
            var template = CharacterModelPackage.AsObject(skillUiTemplates["pet_skill_detail"], "pet skill detail template");
            var root = CharacterModelPackage.AsObject(template["root"], "pet skill detail root");
            var itemBinding = FindOriginalComponent(root, "ItemDetailDialog");
            var skillBinding = FindOriginalComponent(root, "SkillDetailDialog");
            RectTransform Bound(Dictionary<string, object> binding, string field) =>
                binding.TryGetValue(field, out object path) && path is string key && dialogNodes.TryGetValue(key, out var node)
                    ? node : null;

            foreach (var node in AuxiliaryNodes(root))
            {
                if (!node.TryGetValue("original_bindings", out object rawBindings)
                    || !AuxObject(rawBindings).TryGetValue("MultiLanguageText", out object language)) continue;
                string key = Convert.ToString(AuxObject(language)["myKey"], CultureInfo.InvariantCulture);
                SetAuxiliaryText(dialogNodes[Convert.ToString(node["path"], CultureInfo.InvariantCulture)],
                    OriginalDetailText(key) ?? key);
            }
            Bound(itemBinding, "ItemCountGo")?.gameObject.SetActive(false);
            Bound(itemBinding, "ItemSourceObj")?.gameObject.SetActive(false);

            int rank = Math.Max(1, PetSkillRank(pet, skillId));
            int level = Math.Max(1, pet.Level);
            int quality = progression.SkillRules.RankDisplays.First(value => value.Rank == rank).Quality;
            SetTextValue(Bound(itemBinding, "NameText")?.GetComponent<TMP_Text>(),
                Convert.ToString(PetSkillCatalog.Find("item", skillId)["Name"], CultureInfo.InvariantCulture));
            BindOriginalQualityText(Bound(itemBinding, "QualityText"), quality, 45f);
            var backgrounds = CharacterModelPackage.AsObject(skillUiTemplates["detail_quality_backgrounds"], "skill quality backgrounds");
            string[] qualityNames = { "None", "White", "Blue", "Purple", "Orange", "Gold", "Red", "Rainbow" };
            string qualityUri = Convert.ToString(backgrounds[qualityNames[quality]], CultureInfo.InvariantCulture);
            Image qualityBg = Bound(itemBinding, "QualityBg").GetComponent<Image>();
            qualityBg.sprite = skillAssets.FindSprite(qualityUri);
            if (qualityBg.sprite == null)
                throw new InvalidOperationException("Original pet skill detail quality image is absent: " + qualityUri);

            BuildPetSkillIcon(skillId, pet, monsterId, subRank, active, hideLevel,
                Bound(skillBinding, "ItemIconSlot"), false);

            var detailData = CharacterModelPackage.AsObject(skillUiTemplates["detail_data"], "skill detail data");
            var detailSkills = CharacterModelPackage.AsObject(detailData["skills"], "skill detail entries");
            string skillKey = skillId.ToString(CultureInfo.InvariantCulture);
            Dictionary<string, object> detailSkill = detailSkills.TryGetValue(skillKey, out object rawSkill)
                ? CharacterModelPackage.AsObject(rawSkill, "pet skill detail entry") : null;
            bool Description(string field, string suffix)
            {
                RectTransform node = Bound(itemBinding, field);
                if (node == null) return false;
                string description = OriginalDetailText("item_" + skillId.ToString(CultureInfo.InvariantCulture) + suffix);
                node.gameObject.SetActive(description != null);
                if (description != null)
                {
                    TMP_Text text = node.GetComponent<TMP_Text>();
                    if (text != null)
                    {
                        text.text = RestoreOriginalMidSize(description, text.fontSize);
                        if (detailSkill != null && detailSkill.ContainsKey("hyperlink_data"))
                            BindOriginalSkillHyperlinks(skillId, text);
                    }
                }
                return description != null;
            }
            bool function = Description("FuncDescText", "_func_desc");
            bool description = Description("DescText", "_desc");
            Bound(itemBinding, "DetailGo")?.gameObject.SetActive(function || description);

            RectTransform levelNode = Bound(skillBinding, "LevelText");
            if (levelNode != null) levelNode.gameObject.SetActive(!hideLevel);
            RectTransform blessButton = Bound(skillBinding, "mBlessLevelRuleBtn");
            if (hideLevel)
            {
                if (blessButton != null) blessButton.gameObject.SetActive(false);
            }
            else
            {
                TMP_Text levelText = levelNode == null ? null : levelNode.GetComponent<TMP_Text>();
                SetTextValue(levelText, RestoreOriginalMidSize(string.Format(CultureInfo.InvariantCulture,
                    OriginalDetailText("ui_common_level_style_10"), level), levelText == null ? 16f : levelText.fontSize));
                int subRankType = subRank < 2 ? subRank : 2 + (subRank - 2) / 3;
                int baseLimit = progression.SkillRules.BaseLevelLimits.FirstOrDefault(value => value.SubRank == subRankType)?.SkillLevel ?? 0;
                RefreshOriginalBless(skillBinding, "mBlessLevelView", dialogNodes, level > baseLimit);
                if (blessButton != null)
                {
                    blessButton.gameObject.SetActive(level > baseLimit);
                    Button button = blessButton.GetComponent<Button>();
                    if (button != null) button.onClick.AddListener(() => OpenBlessLevelRule(baseLimit, level,
                        RectTransformUtility.WorldToScreenPoint(null, blessButton.position)));
                }
            }

            SetAuxiliaryText(Bound(skillBinding, "SkillTypeText"), OriginalDetailText(
                active ? "ItemType.ActiveSkill" : "ItemType.PassiveSkill"));
            SetOfficialState("pet_skill_detail", "SkillDetailDialog", "mSkillBgIconStateSwithcer",
                active ? ActiveSkillItemType : PassiveSkillItemType, dialogNodes);
            if (FindOriginalComponent(root, "StarMiniInfo") != null)
                BindOriginalDetailStars(root, dialogNodes, rank);
            if (detailSkill != null)
            {
                BindOriginalDetailTags(skillId, root, dialogNodes);
                BindOriginalDetailProfession(skillId, root, dialogNodes);
            }
            else
            {
                RectTransform tags = Bound(skillBinding, "SkillTagGridLayout");
                if (tags != null) tags.gameObject.SetActive(false);
                RectTransform profession = Bound(skillBinding, "ItemDetailDialogProfessionIcon");
                if (profession != null) profession.gameObject.SetActive(false);
            }
            RectTransform range = Bound(skillBinding, "RangeUI");
            var ranges = CharacterModelPackage.AsObject(detailData["ranges"], "skill ranges");
            bool showRange = active && detailSkill != null && ranges.ContainsKey(skillKey);
            if (range != null) range.gameObject.SetActive(showRange);
            if (showRange) StartCoroutine(BindOriginalDetailRange(skillId, root, dialogNodes));
            RectTransform useResult = Bound(itemBinding, "mItemDetailDialogUseResult");
            if (useResult != null) useResult.gameObject.SetActive(false);
            SetOfficialState("pet_skill_detail", "ItemDetailDialog", "mRemainTimeStateSwitcher", 0, dialogNodes);
            var addition = FindOriginalComponent(root, "ItemDetailDialogAddition");
            if (addition != null && addition.TryGetValue("mAdditionDetailButton", out object additionPath)
                && additionPath is string path && dialogNodes.TryGetValue(path, out RectTransform additionButton))
                additionButton.gameObject.SetActive(false);
            SetOfficialState("pet_skill_detail", "PetSkillDetailDialog", "mCanUpgradeSwitcher", 0, dialogNodes);
            SetOfficialState("pet_skill_detail", "PetSkillDetailDialog", "mNeedTipDot", 0, dialogNodes);

            var petProperties = new OfficialSkillDetailProperties(PetSkillCatalog, skillAssets);
            var properties = petProperties.CalculatePetSkill(pet.ClassId, pet.EvolutionPhase, monsterId,
                skillId, rank, level, Math.Max(1, subRank));
            var propPanel = FindOriginalComponent(root, "SkillPanelPropPanel");
            var propPaths = (List<object>)propPanel["mSkillPropItems"];
            for (int i = 0; i < propPaths.Count; i++)
            {
                RectTransform row = dialogNodes[Convert.ToString(propPaths[i], CultureInfo.InvariantCulture)];
                row.gameObject.SetActive(i < properties.Count);
                if (i >= properties.Count) continue;
                SetTextValue(FindText(row, "Key"), properties[i].Name);
                SetTextValue(FindText(row, "Value"), properties[i].Value);
            }
        }

        private void ClosePetSkillDetail()
        {
            if (petSkillDetailDialog != null) Destroy(petSkillDetailDialog);
            petSkillDetailDialog = null;
        }

        private int PetSkillOuterTag(int skillId)
        {
            Dictionary<string, object> skill = PetSkillCatalog.Find("skill", skillId);
            return skill != null && skill.TryGetValue("OuterTag", out object raw) && raw != null
                ? Convert.ToInt32(raw, CultureInfo.InvariantCulture) : 0;
        }

        private static int[] PetSkillIds(Dictionary<string, object> evolution, string field)
        {
            if (evolution == null || !evolution.TryGetValue(field, out object raw) || !(raw is List<object> ids))
                return Array.Empty<int>();
            return ids.Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).ToArray();
        }
    }
}
