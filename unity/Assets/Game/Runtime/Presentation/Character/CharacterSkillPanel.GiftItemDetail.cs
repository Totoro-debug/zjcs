using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        // ItemDetailDialogFactory selects ItemDetailDialog for all ten skill gifts.
        private void OpenOriginalGiftItemDetail(int itemId)
        {
            BuildAuxiliary("skill_gift_item_detail", out var itemNodes, out var itemButtons);
            var root = AuxObject(AuxObject(skillUiTemplates["skill_gift_item_detail"])["root"]);
            var binding = FindOriginalComponent(root, "ItemDetailDialog");
            RectTransform Bound(string field) => itemNodes[Convert.ToString(binding[field])];
            var item = progression.Catalog.FindItem(itemId);
            if (Convert.ToBoolean(item["CanUse"])) throw new InvalidOperationException("A skill gift requires a specialized use dialog: " + itemId);
            Bound("ItemImage").GetComponent<Image>().sprite = skillAssets.FindSprite(Convert.ToString(AuxObject(skillUiTemplates["gift_item_icons"])[itemId.ToString(CultureInfo.InvariantCulture)]));
            Bound("FakeItemIcon").gameObject.SetActive(false);
            SetAuxiliaryText(Bound("NameText"), OriginalDetailText("item_" + itemId + "_name"));
            string type = Convert.ToString(AuxObject(AuxiliaryData["item_type_names"])[Convert.ToString(item["ItemType"], CultureInfo.InvariantCulture)]);
            SetAuxiliaryText(Bound("TypeText"), OriginalDetailText("ItemType." + type));
            int quality = AuxInt(item, "Quality");
            string[] qualityNames = { "None", "White", "Blue", "Purple", "Orange", "Gold", "Red", "Rainbow" };
            SetAuxiliaryText(Bound("QualityText"), OriginalDetailText("Quality." + qualityNames[quality]));
            if (qualityGradient == null) qualityGradient = skillAssets.LoadQualityGradient();
            Bound("QualityText").GetComponent<Graphic>().color = qualityGradient.normalColors[quality];
            string bg = Convert.ToString(AuxObject(skillUiTemplates["detail_quality_backgrounds"])[qualityNames[quality]]);
            Bound("QualityBg").GetComponent<Image>().sprite = skillAssets.FindSprite(bg);
            bool Describe(string field, string suffix) {
                string value = OriginalDetailText("item_" + itemId + suffix);
                Bound(field).gameObject.SetActive(value != null);
                if (value != null) SetAuxiliaryText(Bound(field), RestoreOriginalMidSize(value, Bound(field).GetComponent<TMP_Text>().fontSize));
                return value != null;
            }
            bool function = Describe("FuncDescText", "_func_desc"), description = Describe("DescText", "_desc");
            Bound("DetailGo").gameObject.SetActive(function || description);
            Bound("ItemCountGo").gameObject.SetActive(Convert.ToBoolean(item["CanOverlap"]) && Convert.ToBoolean(item["ShowPlayerItemCount"]));
            long count = progression.Player.Inventory.Where(value => value.ItemId == itemId).Sum(value => value.Count);
            string countPath = Convert.ToString(binding["ItemCountText"]);
            var countSource = AuxiliaryNodes(root).First(value => Convert.ToString(value["path"]) == countPath);
            string countKey = Convert.ToString(AuxObject(AuxObject(countSource["original_bindings"])["MultiLanguageText"])["myKey"]);
            SetAuxiliaryText(Bound("ItemCountText"), string.Format(CultureInfo.InvariantCulture, OriginalDetailText(countKey), OfficialPropFormat.Compact(count)));
            SetOfficialState("skill_gift_item_detail", "ItemDetailDialog", "mShowCountStateSwitcher", 0, itemNodes);
            SetOfficialState("skill_gift_item_detail", "ItemDetailDialog", "mRemainTimeStateSwitcher", 0, itemNodes);
            Bound("mItemDetailDialogUseResult").gameObject.SetActive(false);
            // ItemDetailDialogAddition.UpdateItemAdditionButton: none of the ten
            // original skill gifts belongs to ItemOutputGroupTypeDict.
            var addition = FindOriginalComponent(root, "ItemDetailDialogAddition");
            itemNodes[Convert.ToString(addition["mAdditionDetailButton"])].gameObject.SetActive(false);
            BindOriginalGiftItemSources(itemId, root, itemNodes);
            foreach (var button in itemButtons.Where(value => value.Key.EndsWith("/CloseBtn", StringComparison.Ordinal)))
                button.Value.onClick.AddListener(CloseAuxiliary);
        }

        private void BindOriginalGiftItemSources(int itemId, Dictionary<string, object> root,
            Dictionary<string, RectTransform> itemNodes)
        {
            var binding = FindOriginalComponent(root, "ItemSourceGo");
            RectTransform slot = itemNodes[Convert.ToString(binding["ItemSourceGetSlot"])];
            foreach (Transform child in slot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var rows = ((List<object>)AuxiliaryData["item_source"]).Select(AuxObject).Where(value => AuxInt(value, "Id") == itemId).ToArray();
            var template = AuxObject(skillUiTemplates["skill_source_item"]);
            var rowBinding = FindOriginalComponent(AuxObject(template["root"]), "ItemSourceItem");
            foreach (var row in rows) {
                var view = GetComponentInParent<OfficialCharacterPanelView>();
                if (AuxInt(row, "BlockCondition") > 0 && view?.SkillGiftSourceBlockConditionMet?.Invoke(AuxInt(row, "BlockCondition")) == true) continue;
                var rowNodes = new Dictionary<string, RectTransform>();
                factory.Build(CharacterPanelNodeFactory.ReadRoot(template), slot, rowNodes, new Dictionary<string, Button>());
                int type = AuxInt(row, "ItemSourceType");
                string name = Convert.ToString(AuxObject(AuxiliaryData["item_source_type_names"])[type.ToString(CultureInfo.InvariantCulture)]);
                if (type == 11) name += "_" + Convert.ToString(AuxObject(AuxiliaryData["material_dungeon_type_names"])[Convert.ToString(row["Param"])]);
                string param = Convert.ToString(row["Param"]);
                SetAuxiliaryText(rowNodes[Convert.ToString(rowBinding["TypeText"])], OriginalDetailText(type == 12 ? "FurnitureSourceTitle_" + param : "ItemSourceType." + name));
                string limitCode = view?.SkillGiftSourceLimitCode?.Invoke(type, param);
                bool limited = !string.IsNullOrEmpty(limitCode);
                SetOfficialState("skill_source_item", "ItemSourceItem", "mStateSwitcher", limited ? 0 : 1, rowNodes);
                var canvas = rowNodes[Convert.ToString(rowBinding["mCanvasGroup"])].GetComponent<CanvasGroup>();
                canvas.alpha = limited ? .5f : 1f;
                foreach (object path in (List<object>)rowBinding["SwitchToBtns"])
                    rowNodes[Convert.ToString(path)].GetComponent<Button>().onClick.AddListener(() => {
                        if (limited) { ShowOriginalSystemMessage("ItemInfoDialog.Limit." + limitCode); return; }
                        CloseAuxiliary(); NavigationRequested?.Invoke("item-source:" + type + ":" + param);
                    });
            }
            itemNodes[Convert.ToString(FindOriginalComponent(root, "ItemDetailDialog")["ItemSourceObj"])].gameObject.SetActive(rows.Length > 0);
        }
    }
}
