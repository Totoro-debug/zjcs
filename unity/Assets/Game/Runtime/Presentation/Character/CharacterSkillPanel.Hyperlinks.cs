using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.SkillsUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private void BindOriginalSkillHyperlinks(int skillId, TMP_Text text)
        {
            var listener = text.GetComponent<OfficialSkillHyperlinkPointer>()
                ?? text.gameObject.AddComponent<OfficialSkillHyperlinkPointer>();
            text.raycastTarget = true;
            listener.Clicked = (index, position) => {
                var skill = AuxObject(AuxObject(AuxObject(skillUiTemplates["detail_data"])["skills"])
                    [skillId.ToString(CultureInfo.InvariantCulture)]);
                var parameters = (List<object>)skill["hyperlink_data"];
                if (index < 0 || index >= parameters.Count) return;
                var kinds = (List<object>)skill["hyperlink_types"];
                if (Convert.ToInt32(kinds[index]) == 0) {
                    Match match = Regex.Match(Convert.ToString(parameters[index]), @"EntryId:(\d+)");
                    if (match.Success) OpenOriginalEntry(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), position);
                }
                else if (Convert.ToInt32(kinds[index]) == 9) {
                    string parameter = Convert.ToString(parameters[index]);
                    int petId = int.Parse(Regex.Match(parameter, @"PetId:(\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
                    int entryId = int.Parse(Regex.Match(parameter, @"EntryId:(\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
                    var pet = progression.Player.Pets.Pets.FirstOrDefault(value => value.ClassId == petId);
                    var props = new OfflinePetPropSource(progression.Catalog).CalculateAbilityProps(petId, pet?.Abilities);
                    props.TryGetValue(progression.Catalog.PropertyId("PetInitEnergy"), out long energy);
                    var data = AuxObject(AuxiliaryData["pet_energy"]);
                    var configuration = AuxObject(AuxObject(data["skills"])[skillId.ToString(CultureInfo.InvariantCulture)]);
                    var setting = AuxObject(data["settings"]);
                    OpenOriginalEntry(entryId, position, new object[] { configuration["TriggerNeedEnergy"],
                        Convert.ToInt64(configuration["InitEnergy"]) + energy,
                        setting["NaturalRecoverEnergy"], setting["MasterLoseHpRecoverEnergy"] });
                }
            };
        }

        // HyperLinkClickHandlerEntry -> EntryDetailDialog -> EntryDetailDialogItem.
        private void OpenOriginalEntry(int entryId, Vector2 position, object[] descriptionArguments = null)
        {
            var entry = ((List<object>)AuxiliaryData["entry"]).Select(AuxObject).First(value => AuxInt(value, "Id") == entryId);
            var dialog = BuildAuxiliary("skill_entry_tip", out var entryNodes, out _);
            dialog.SetActive(false);
            var root = AuxObject(AuxObject(skillUiTemplates["skill_entry_tip"])["root"]);
            var binding = FindOriginalComponent(root, "EntryDetailDialog");
            var scrollBinding = FindOriginalComponent(root, "LoopVerticalScrollWithMaxHeight");
            var itemTemplate = AuxObject(skillUiTemplates["skill_entry_tip_item"]);
            var itemRoot = AuxObject(itemTemplate["root"]);
            var itemBinding = FindOriginalComponent(itemRoot, "EntryDetailDialogItem");
            var prefab = factory.Build(CharacterPanelNodeFactory.ReadRoot(itemTemplate), dialog.transform,
                new Dictionary<string, RectTransform>(), new Dictionary<string, Button>());
            prefab.gameObject.SetActive(false);
            entryNodes[Convert.ToString(AuxObject(scrollBinding["DefaultPrefabSource"])["Prefab"])] = prefab;
            RectTransform slot = entryNodes[Convert.ToString(scrollBinding["m_Content"])];
            foreach (Transform child in slot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var source = dialog.AddComponent<OfficialSkillEntryDataSource>();
            source.Bind = (item, index) => {
                string prefix = Convert.ToString(itemRoot["path"]);
                RectTransform Bound(string field) {
                    string relative = Convert.ToString(itemBinding[field]).Substring(prefix.Length).TrimStart('/');
                    return (RectTransform)(relative.Length == 0 ? item : item.Find(relative));
                }
                string icon = Convert.ToString(entry["Icon_uri"]), bg = Convert.ToString(entry["Bg_uri"]);
                Bound("IconGo").gameObject.SetActive(icon.Length > 0);
                Bound("BgImg").gameObject.SetActive(bg.Length > 0);
                if (icon.Length > 0) Bound("Icon").GetComponent<Image>().sprite = skillAssets.FindSprite(icon);
                if (bg.Length > 0) Bound("BgImg").GetComponent<Image>().sprite = skillAssets.FindSprite(bg);
                Bound("CountTxt").gameObject.SetActive(false);
                SetAuxiliaryText(Bound("NameTxt"), OriginalDetailText("entry_" + entryId + "_name"));
                string description = OriginalDetailText("entry_" + entryId + "_desc");
                SetAuxiliaryText(Bound("DescTxt"), descriptionArguments == null ? description
                    : string.Format(CultureInfo.InvariantCulture, description, descriptionArguments));
                Bound("TitleSplitGo").gameObject.SetActive(true);
                Bound("LineGo").gameObject.SetActive(false);
                foreach (Transform addition in Bound("mAdditionGoContainer")) addition.gameObject.SetActive(false);
            };
            var scrollNode = entryNodes[Convert.ToString(binding["mLoopVerticalScrollRect"])];
            var scroll = scrollNode.gameObject.AddComponent<LoopVerticalScrollWithMaxHeight>();
            OfficialSkillComponentBindings.Apply(scroll, scrollBinding, entryNodes);
            scroll.dataSource = source;
            scroll.totalCount = 1;
            dialog.SetActive(true);
            scroll.RefillCells();
            StartCoroutine(PositionOriginalEntry(root, entryNodes, position));
            dialog.AddComponent<OfficialSkillPopupMask>().Show(skillUiTemplates, 2, true, CloseAuxiliary, null);
        }

        private IEnumerator PositionOriginalEntry(Dictionary<string, object> root,
            Dictionary<string, RectTransform> entryNodes, Vector2 position)
        {
            yield return null;
            var target = entryNodes[Convert.ToString(FindOriginalComponent(root, "EntryDetailDialog")["Rect"])];
            yield return PositionOriginalTipRect(target, position);
        }
    }
}
