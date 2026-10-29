using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.SkillsUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Presentation.Combat;

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
                else if (Convert.ToInt32(kinds[index]) == 1) {
                    Match match = Regex.Match(Convert.ToString(parameters[index]), @"MonsterId:(\d+)");
                    if (match.Success) OpenMonsterBrief(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), skillId);
                }
                else if (Convert.ToInt32(kinds[index]) == 8) {
                    Match match = Regex.Match(Convert.ToString(parameters[index]), @"PetId:(\d+)");
                    if (match.Success) OpenPetEntityBrief(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
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

        private void OpenMonsterBrief(int monsterId, int sourceSkillId = 0)
        {
            var combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            var combat = combatAssets.Load();
            var dialog = BuildAuxiliary("monster_brief", out var view, out var buttons);
            var root = AuxObject(AuxObject(skillUiTemplates["monster_brief"])["root"]);
            var binding = FindOriginalComponent(root, "MonsterBriefInfoPanel");
            int level = 1;
            int subRank = 0;
            Dictionary<int, long> props = new Dictionary<int, long>(combat.CalculateMonsterProps(monsterId, 0, level, subRank));
            Dictionary<int, long> ownerProps = progression.PropertyCalculator.Evaluate(progression.Player,
                DateTimeOffset.Now.ToUnixTimeSeconds()).OutOfFightBattleProps.ToDictionary(value => value.Key, value => value.Value);
            if (sourceSkillId > 0 && combat.HasSummonInheritance(monsterId))
            {
                int rank = Math.Max(1, OfflineSkillProgression.OwnedRank(progression.Player.Skills, sourceSkillId));
                FightSummonPropertyInheritance inheritance = combat.CalculateSummonInheritance(monsterId,
                    level, rank, subRank, ownerProps);
                MergePropertyValues(props, inheritance.Fixed);
                MergePropertyValues(props, inheritance.Summoner);
            }

            TransformAt(binding, "mMonseterNameText", view).GetComponent<TMP_Text>().text = combat.MonsterName(monsterId) ?? monsterId.ToString(CultureInfo.InvariantCulture);
            TransformAt(binding, "mMonsterTypeText", view).GetComponent<TMP_Text>().text = OriginalDetailText("ui_monster_type_" + monsterId) ?? string.Empty;
            TransformAt(binding, "mMonsterLevelText", view).GetComponent<TMP_Text>().text = level.ToString(CultureInfo.InvariantCulture);
            TransformAt(binding, "mFightUiRoot", view).gameObject.SetActive(false);
            TransformAt(binding, "mSkillSlot", view).gameObject.SetActive(false);
            TransformAt(binding, "mCommonRankShow", view).gameObject.SetActive(false);
            Transform modelSlot = TransformAt(binding, "mMonsterIconContent", view);
            auxiliaryModelLibrary = OfficialBattleNativeAnimationLibrary.LoadDefault();
            string modelKey = auxiliaryModelLibrary.ResolveModelKey(new FightBattleUnitState { MonsterId = monsterId });
            OfficialBattleNativeModelRecord model = auxiliaryModelLibrary.FindModel(modelKey);
            if (model != null)
            {
                auxiliaryNativeModel = new OfficialBattleNativeModelView(auxiliaryModelLibrary, model,
                    modelSlot, "MonsterBrief_" + monsterId);
                auxiliaryNativeModel.Play("idle_01", true);
            }

            var snapshot = new CharacterPropSnapshot
            {
                BaseBattleProps = props, BattleProps = props, OutOfFightBattleProps = props,
                NonBattleProps = new Dictionary<int, long>(), CombatRating = 0,
                Chunks = new Dictionary<int, IReadOnlyDictionary<int, long>>(),
                DetailProps = new Dictionary<int, IReadOnlyDictionary<int, long>>(), DetailOrder = Array.Empty<int>()
            };
            auxiliaryMonsterProps = dialog.AddComponent<CharacterPropPanels>();
            auxiliaryMonsterProps.Initialize(OfficialCharacterAssetCatalog.LoadDefault(), progression.Catalog,
                () => snapshot);
            Button detail = view[Convert.ToString(binding["mDetailPropBtn"])].GetComponent<Button>();
            if (detail != null) detail.onClick.AddListener(() => auxiliaryMonsterProps.OpenDialog(dialog.transform,
                "ui_monster_detail_props_title_text"));
        }

        private void OpenPetEntityBrief(int petClassId)
        {
            OpenMonsterBrief(OfflinePetEntityMonsterId(petClassId));
        }

        private int OfflinePetEntityMonsterId(int petClassId)
        {
            Dictionary<string, object> pet = progression.Catalog.Find("pet", petClassId);
            return pet == null ? 0 : Convert.ToInt32(pet["FightEntityClassId"], CultureInfo.InvariantCulture);
        }

        private static Transform TransformAt(Dictionary<string, object> binding, string field,
            Dictionary<string, RectTransform> nodes)
        {
            string path = Convert.ToString(binding[field], CultureInfo.InvariantCulture);
            return nodes.TryGetValue(path, out RectTransform node) ? node : null;
        }

        private static void MergePropertyValues(Dictionary<int, long> target,
            IReadOnlyDictionary<int, long> additions)
        {
            if (additions == null) return;
            foreach (KeyValuePair<int, long> pair in additions)
                target[pair.Key] = target.TryGetValue(pair.Key, out long current)
                    ? checked(current + pair.Value) : pair.Value;
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
