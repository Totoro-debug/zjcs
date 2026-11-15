using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        // SkillPieceDetailDialog.Internal_Init and ItemDetailDialog.UpdateItemDesc.
        // Bind serialized prefab fields instead of selecting ambiguous node names.
        private void BindOriginalPieceDetail(int skillId, Dictionary<string, RectTransform> dialogNodes,
            Dictionary<string, Button> dialogButtons)
        {
            var root = AuxObject(AuxObject(skillUiTemplates["skill_piece_detail"])["root"]);
            var itemBinding = FindOriginalComponent(root, "ItemDetailDialog");
            var binding = FindOriginalComponent(root, "SkillPieceDetailDialog");
            RectTransform Item(string field) => dialogNodes[Convert.ToString(itemBinding[field])];
            RectTransform Skill(string field) => dialogNodes[Convert.ToString(binding[field])];
            var definition = progression.SkillDefinition(skillId);
            int pieceId = definition.PieceItemId;
            var item = progression.Catalog.FindItem(pieceId);
            long count = progression.Player.Inventory.Where(value => value != null && value.ItemId == pieceId).Sum(value => value.Count);
            Item("ItemImage").GetComponent<Image>().sprite = assets.FindSprite(progression.Catalog.ItemIconUri(pieceId))
                ?? skillAssets.FindSprite(progression.Catalog.ItemIconUri(pieceId))
                ?? throw new InvalidOperationException("Original skill piece icon is absent: " + pieceId);
            Item("FakeItemIcon").gameObject.SetActive(false);
            SetAuxiliaryText(Item("NameText"), ItemName(pieceId));
            string type = Convert.ToString(AuxObject(AuxiliaryData["item_type_names"])[Convert.ToString(item["ItemType"], CultureInfo.InvariantCulture)]);
            SetAuxiliaryText(Item("TypeText"), OriginalDetailText("ItemType." + type));
            int quality = AuxInt(item, "Quality");
            string[] qualities = { "None", "White", "Blue", "Purple", "Orange", "Gold", "Red", "Rainbow" };
            SetAuxiliaryText(Item("QualityText"), OriginalDetailText("Quality." + qualities[quality]));
            if (qualityGradient == null) qualityGradient = skillAssets.LoadQualityGradient();
            Item("QualityText").GetComponent<Graphic>().color = qualityGradient.normalColors[quality];
            Item("QualityBg").GetComponent<Image>().sprite = skillAssets.FindSprite(Convert.ToString(
                AuxObject(skillUiTemplates["detail_quality_backgrounds"])[qualities[quality]]));
            bool Describe(string field, string suffix)
            {
                string value = OriginalDetailText("item_" + pieceId + suffix);
                Item(field).gameObject.SetActive(value != null);
                if (value != null) SetAuxiliaryText(Item(field), RestoreOriginalMidSize(value, Item(field).GetComponent<TMP_Text>().fontSize));
                return value != null;
            }
            bool function = Describe("FuncDescText", "_func_desc"), description = Describe("DescText", "_desc");
            Item("DetailGo").gameObject.SetActive(function || description);
            Item("ItemCountGo").gameObject.SetActive(Convert.ToBoolean(item["CanOverlap"]) && Convert.ToBoolean(item["ShowPlayerItemCount"]));
            SetOriginalPieceFormat(root, dialogNodes, Item("ItemCountText"), count);
            SetOfficialState("skill_piece_detail", "ItemDetailDialog", "mShowCountStateSwitcher", 0, dialogNodes);
            SetOfficialState("skill_piece_detail", "ItemDetailDialog", "mRemainTimeStateSwitcher", 0, dialogNodes);
            Item("mItemDetailDialogUseResult").gameObject.SetActive(false);
            var addition = FindOriginalComponent(root, "ItemDetailDialogAddition");
            if (addition != null) dialogNodes[Convert.ToString(addition["mAdditionDetailButton"])].gameObject.SetActive(false);
            SetAuxiliaryText(Skill("mSkillDescText"), RestoreOriginalMidSize(OriginalDetailText("item_" + skillId + "_func_desc"),
                Skill("mSkillDescText").GetComponent<TMP_Text>().fontSize));
            BindOriginalSkillHyperlinks(skillId, Skill("mSkillDescText").GetComponent<TMP_Text>());
            BindOriginalDetailTags(skillId, root, dialogNodes, "SkillPieceDetailDialog", "mSkillTagGridLayout");
            BindOriginalDetailProfession(skillId, root, dialogNodes, "SkillPieceDetailDialog", "mItemDetailDialogProfessionIcon");
            BindOriginalDetailSources(pieceId, root, dialogNodes, ClosePieceDetail);

            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId);
            long required = rank == 0 ? progression.SkillMergePiece(skillId)
                : definition.RankCosts.FirstOrDefault(value => value.CurrentRank == rank)?.Items
                    .Where(value => value.ItemId == pieceId).Sum(value => value.Count) ?? 0;
            int state = rank == 0 ? 1 : rank < definition.MaximumRank ? 2 : 0;
            int buttonState = state == 0 ? 1 : required > 0 && count >= required ? state + 1 : 0;
            SetOfficialState("skill_piece_detail", "SkillPieceDetailDialog", "mStatusSwitcher", state, dialogNodes);
            SetOfficialState("skill_piece_detail", "SkillPieceDetailDialog", "mBtnStateSwitcher", buttonState, dialogNodes);
            if (state != 0)
            {
                SetOriginalPieceFormat(root, dialogNodes, Skill(state == 1 ? "mMergeInfo" : "mRankUpInfo"), count, required);
                RectTransform sliderNode = Skill(state == 1 ? "mMergeUpSlider" : "mRankUpSlider");
                var source = AuxiliaryNodes(root).First(value => Convert.ToString(value["path"]) ==
                    dialogNodes.First(value => value.Value == sliderNode).Key);
                var sliderFields = FindOriginalComponent(source, "Slider");
                var slider = sliderNode.GetComponent<Slider>() ?? sliderNode.gameObject.AddComponent<Slider>();
                slider.fillRect = dialogNodes[Convert.ToString(sliderFields["mFillRect"])];
                slider.handleRect = sliderFields["mHandleRect"] is string handle ? dialogNodes[handle] : null;
                slider.direction = (Slider.Direction)Convert.ToInt32(sliderFields["mDirection"]);
                slider.wholeNumbers = Convert.ToBoolean(sliderFields["mWholeNumbers"]);
                ConfigureProgressSlider(slider, count, required);
            }
            dialogButtons[Convert.ToString(binding["mMergeButton"])].onClick.AddListener(() => OpenMergeConfirm(skillId));
            dialogButtons[Convert.ToString(binding["mRankUpButton"])].onClick.AddListener(() => { ClosePieceDetail(); OpenPromote(skillId, 0); });
            foreach (var button in dialogButtons.Where(value => value.Key.EndsWith("/CloseBtn", StringComparison.Ordinal)))
                button.Value.onClick.AddListener(ClosePieceDetail);
        }

        private void SetOriginalPieceFormat(Dictionary<string, object> root, Dictionary<string, RectTransform> dialogNodes, RectTransform target, params long[] counts)
        {
            var source = AuxiliaryNodes(root).First(value => Convert.ToString(value["path"]) == dialogNodes.First(value => value.Value == target).Key);
            // MultiLanguageText is already materialized in the prefab's text data.
            var text = target.GetComponent<TMP_Text>();
            string format = text.text;
            if (source.TryGetValue("original_bindings", out object raw)
                && AuxObject(raw).TryGetValue("MultiLanguageText", out object language))
                format = OriginalDetailText(Convert.ToString(AuxObject(language)["myKey"])) ?? format;
            SetAuxiliaryText(target, RestoreOriginalMidSize(string.Format(CultureInfo.InvariantCulture, format,
                counts.Select(value => (object)OfficialPropFormat.Compact(value)).ToArray()), text.fontSize));
        }

        // SkillMergeConfirmDialog.Init initializes its two existing ItemIcons.
        private void BindOriginalMergeIcons(int skillId, Dictionary<string, RectTransform> dialogNodes)
        {
            var root = AuxObject(AuxObject(skillUiTemplates["skill_merge_confirm"])["root"]);
            var binding = FindOriginalComponent(root, "SkillMergeConfirmDialog");
            int pieceId = progression.SkillDefinition(skillId).PieceItemId;
            foreach (string field in new[] { "mSkillPiece", "mSkillIcon" })
            {
                bool piece = field == "mSkillPiece";
                int itemId = piece ? pieceId : skillId;
                string path = Convert.ToString(binding[field]);
                var iconNodes = dialogNodes.Where(value => value.Key == path || value.Key.StartsWith(path + "/", StringComparison.Ordinal))
                    .ToDictionary(value => value.Key, value => value.Value);
                var source = AuxiliaryNodes(root).First(value => Convert.ToString(value["path"]) == path);
                var iconBinding = FindOriginalComponent(source, piece ? "ItemIconSkillPiece" : "ItemIconSkill");
                RectTransform icon = dialogNodes[path];
                BindOriginalSkillIcon(itemId, icon, iconNodes, iconLayout: source);
                dialogNodes[Convert.ToString(iconBinding["mFakeIcon"])].gameObject.SetActive(false);
                dialogNodes[Convert.ToString(iconBinding["BottomGo"])].gameObject.SetActive(true);
                SetAuxiliaryText(dialogNodes[Convert.ToString(iconBinding["myBottomText"])], ItemName(itemId));
                if (piece)
                {
                    long count = progression.Player.Inventory.Where(value => value != null && value.ItemId == pieceId).Sum(value => value.Count);
                    ShowOriginalPieceProgress(icon, pieceId, count);
                    var progress = FindOriginalComponent(source, "ItemProgress");
                    SetOriginalComponentState(progress, "mStateSwitcher", 0, dialogNodes);
                    dialogNodes[Convert.ToString(progress["mLeftCountText"])].GetComponent<Graphic>().color = OriginalDetailColor(
                        AuxObject(skillUiTemplates["cultivation_colors"])[count >= progression.SkillMergePiece(skillId) ? "Green" : "LightRed"]);
                    dialogNodes[Convert.ToString(progress["mRightCountText"])].GetComponent<Graphic>().color = OriginalDetailColor(progress["mDefaultTextColor"]);
                }
                Image image = icon.GetComponent<Image>() ?? icon.gameObject.AddComponent<Image>();
                image.color = Color.clear; image.raycastTarget = true; image.canvasRenderer.cullTransparentMesh = false;
                Button button = icon.GetComponent<Button>() ?? icon.gameObject.AddComponent<Button>();
                button.enabled = true; button.targetGraphic = image;
                button.onClick.AddListener(() => { if (piece) OpenPieceDetail(skillId); else OpenSkillDetail(skillId); });
            }
        }
    }
}
