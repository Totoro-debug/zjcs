using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.SkillsUI;
using Zjcs.Official.SkillsUI.FancyScrollView;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Character
{
    [Serializable]
    public sealed class OfficialSkillDamageTestRequest
    {
        public int StageId, Difficulty, MonsterLevel, MaxRound;
        public bool EnableMonsterOneDamage, EnableMonsterUnlimitHp;
        public Dictionary<string, object> OriginalBattleData;
        public string BattleRuntimeJson;
    }

    public sealed partial class CharacterSkillPanel
    {
        // StageDamageTestPanel keeps these selections for the client session.
        private static int damageStageIndex, damageLevelIndex, damageRoundIndex, damageHpIndex, damageHurtIndex;
        public event Action<OfficialSkillDamageTestRequest> DamageTestRequested;

        private void OpenOriginalDamageTest()
        {
            var dialog = BuildAuxiliary("damage_test", out var damageNodes, out var damageButtons);
            dialog.SetActive(false);
            var source = AuxObject(AuxObject(skillUiTemplates["damage_test"])["root"]);
            var binding = FindOriginalComponent(source, "StageDamageTestPanel");
            RectTransform Bound(string field) => damageNodes[Convert.ToString(binding[field])];
            var templates = new Dictionary<string, Dictionary<string, object>>();
            foreach (string key in new[] { "damage_test_num_cell", "damage_test_dropdown_item" }) {
                var template = AuxObject(skillUiTemplates[key]);
                var root = factory.Build(CharacterPanelNodeFactory.ReadRoot(template), dialog.transform, damageNodes, new Dictionary<string, Button>());
                root.gameObject.SetActive(false);
                templates.Add(key, AuxObject(template["root"]));
            }
            var allNodes = AuxiliaryNodes(source).Concat(templates.Values.SelectMany(AuxiliaryNodes)).ToArray();
            var types = new Dictionary<string, Type> {
                { "MultiLanguageText", typeof(WorldMultiLanguageText) }, { "ToggleGroup", typeof(ToggleGroup) },
                { "Toggle", typeof(Toggle) }, { "UIToggleGroup", typeof(UIToggleGroup) }, { "UIToggle", typeof(UIToggle) },
                { "ToggleText", typeof(ToggleText) }, { "UIToggleObject", typeof(UIToggleObject) },
                { "Scroller", typeof(Scroller) }, { "NumScrollView", typeof(NumScrollView) },
                { "LoopVerticalScrollRect", typeof(LoopVerticalScrollRect) }, { "DropdownItem", typeof(WorldDropdownItem) },
                { "UIDropdown", typeof(WorldUIDropdown) }, { "NumCell", typeof(NumCell) }
            };
            foreach (var node in allNodes) {
                if (!node.TryGetValue("original_bindings", out object raw)) continue;
                RectTransform target = damageNodes[Convert.ToString(node["path"])];
                foreach (var pair in AuxObject(raw))
                    if (types.TryGetValue(pair.Key, out var type) && target.GetComponent(type) == null) target.gameObject.AddComponent(type);
            }
            foreach (var node in allNodes) {
                if (!node.TryGetValue("original_bindings", out object raw)) continue;
                RectTransform target = damageNodes[Convert.ToString(node["path"])];
                foreach (var pair in AuxObject(raw)) {
                    if (!types.TryGetValue(pair.Key, out var type)) continue;
                    var fields = new Dictionary<string, object>(AuxObject(pair.Value));
                    // Animator binding is fulfilled by the extracted clip player below.
                    if (pair.Key == "UIDropdown") fields.Remove("Animator");
                    if (pair.Key == "MultiLanguageText") fields["myKey"] = OriginalDetailText(Convert.ToString(fields["myKey"])) ?? fields["myKey"];
                    if (pair.Key == "NumCell") {
                        var clip = ((List<object>)node["animations"]).Select(AuxObject).Single(value => Convert.ToString(value["name"]) == Convert.ToString(fields["mAnimName"]));
                        foreach (var track in ((List<object>)clip["tracks"]).Select(AuxObject)) {
                            string property = Convert.ToString(track["property"]);
                            fields[property == "scale.x" ? "scaleX" : property == "scale.y" ? "scaleY" : "scaleZ"] =
                                new AnimationCurve(((List<object>)track["keys"]).Select(value => {
                                    var key = (List<object>)value;
                                    return new Keyframe(Convert.ToSingle(key[0]), Convert.ToSingle(key[1]), Convert.ToSingle(key[2]), Convert.ToSingle(key[3]));
                                }).ToArray());
                        }
                    }
                    OfficialSkillComponentBindings.Apply(target.GetComponent(type), fields, damageNodes);
                    if (pair.Key == "Toggle" && target.GetComponent<Toggle>().targetGraphic != null)
                        target.GetComponent<Toggle>().targetGraphic.raycastTarget = true;
                }
            }
            var stageRows = ((List<object>)AuxiliaryData["damage_test_stage_monster"]).Select(AuxObject).ToArray();
            var dropdown = Bound("mDropdown").GetComponent<WorldUIDropdown>();
            dropdown.ItemsKey = stageRows.Select(row => OriginalDetailText("damage_test_stage_monster_" + AuxInt(row, "Id") + "_name")).ToList();
            void RefreshStage(int index) {
                damageStageIndex = index;
                Bound("mImage").GetComponent<Image>().sprite = skillAssets.FindSprite(Convert.ToString(stageRows[index]["image_uri"]))
                    ?? throw new InvalidOperationException("Original damage-test monster image is absent.");
            }
            dropdown.OnDropdownUpdate += RefreshStage;
            Coroutine dropdownAnimation = null;
            void AnimateDropdown(string name) {
                if (dropdownAnimation != null) StopCoroutine(dropdownAnimation);
                var clip = allNodes.Where(node => node.ContainsKey("animations"))
                    .SelectMany(node => (List<object>)node["animations"]).Select(AuxObject)
                    .First(value => Convert.ToString(value["name"]) == name);
                if (TryBuildAnimationTracks(clip, damageNodes, out var tracks, out float duration))
                    dropdownAnimation = StartCoroutine(PlaySkillAnimation(tracks, duration, false));
            }
            dropdown.AnimationStateChanged = shown => AnimateDropdown(shown ? "UIDropDown_Show" : "UIDropDown_Hide");
            foreach (var graphic in dialog.GetComponentsInChildren<Graphic>(true)) {
                var pointer = graphic.gameObject.GetComponent<OfficialSkillDropdownPointer>() ?? graphic.gameObject.AddComponent<OfficialSkillDropdownPointer>();
                pointer.Dropdown = dropdown;
            }
            int rank = progression.Player.SubRank < 2 ? progression.Player.SubRank : 2 + (progression.Player.SubRank - 2) / 3;
            Bound("mySubRankText").GetComponent<WorldMultiLanguageText>().SetKey(
                OriginalDetailText("Rank." + Convert.ToString(((List<object>)AuxiliaryData["rank_names"])[rank])));
            var difficulty = ((List<object>)AuxiliaryData["damage_test_difficulty_level"]).Select(AuxObject)
                .FirstOrDefault(row => AuxInt(row, "Rank") == rank);
            var levels = difficulty == null ? new List<int>() : ((List<object>)difficulty["Level"]).Select(Convert.ToInt32).ToList();
            if (levels.Count > 0 && damageLevelIndex > levels.Count - 1) damageLevelIndex = levels.Count - 1;
            var numbers = Bound("mNumScrollView").GetComponent<NumScrollView>();
            numbers.SetSelectCallback(index => damageLevelIndex = numbers.GetSelectionIndex());
            numbers.SetIsUnlockedDelegate(index => true);
            numbers.SetShowRedDotRewardDelegate(index => false);
            var rounds = ((List<object>)AuxiliaryData["rounds"]).Select(Convert.ToInt32).ToArray();
            int roundSelection = damageRoundIndex, hpSelection = damageHpIndex, hurtSelection = damageHurtIndex;
            Action InitializeSetting(string field, int selected, Action<int> changed, bool isRound = false) {
                var node = allNodes.Single(value => Convert.ToString(value["path"]) == Convert.ToString(binding[field]));
                var setting = AuxObject(AuxObject(node["original_bindings"])["StageDamageTestSettingItem"]);
                var choices = ((List<object>)setting["mToggleTextList"]).Select(path => damageNodes[Convert.ToString(path)].GetComponent<ToggleText>()).ToList();
                var group = damageNodes[Convert.ToString(setting["mGroup"])].GetComponent<UIToggleGroup>();
                group.onToggleChanged += owner => {
                    for (int i = 0; i < choices.Count; i++) if (choices[i].IsOnToggleObj(owner)) { selected = i; break; }
                    for (int i = 0; i < choices.Count; i++) choices[i].SetSelectState(selected == i);
                    changed(selected);
                };
                return () => {
                    if (isRound) foreach (var choice in choices) choice.gameObject.SetActive(false);
                    for (int i = 0; i < (isRound ? Math.Min(choices.Count, rounds.Length) : choices.Count); i++) {
                        choices[i].SetSelectFlag(selected == i);
                        if (isRound) { choices[i].SetFormat(rounds[i].ToString(CultureInfo.InvariantCulture)); choices[i].gameObject.SetActive(true); }
                    }
                };
            }
            Action roundInit = InitializeSetting("mRoundItem", roundSelection, value => roundSelection = value, true);
            Action hpInit = InitializeSetting("mMonsterHpItem", hpSelection, value => hpSelection = value);
            Action hurtInit = InitializeSetting("mMonsterHurtItem", hurtSelection, value => hurtSelection = value);
            foreach (var button in damageButtons.Where(pair => pair.Key.EndsWith("/CloseBtn", StringComparison.Ordinal) || pair.Key.EndsWith("/BackBtn", StringComparison.Ordinal)))
                button.Value.onClick.AddListener(CloseAuxiliary);
            Bound("mStartTestBtn").GetComponent<Button>().onClick.AddListener(() => {
                int roundIndex = Math.Min(roundSelection, rounds.Length - 1);
                if (roundIndex < 0 || levels.Count == 0) return;
                damageRoundIndex = roundIndex; damageHpIndex = hpSelection; damageHurtIndex = hurtSelection;
                var request = new OfficialSkillDamageTestRequest { StageId = AuxInt(stageRows[damageStageIndex], "Id"),
                    Difficulty = damageLevelIndex, MonsterLevel = levels[damageLevelIndex], MaxRound = rounds[roundIndex],
                    EnableMonsterOneDamage = hurtSelection == 0, EnableMonsterUnlimitHp = hpSelection == 0,
                    OriginalBattleData = AuxObject(AuxiliaryData["battle"]),
                    BattleRuntimeJson = Convert.ToString(AuxiliaryData["battle_runtime_json"]) };
                DamageTestRequested?.Invoke(request);
            });
            dialog.SetActive(true);
            roundInit(); hpInit(); hurtInit();
            dropdown.SetSelectedItemIndex(damageStageIndex);
            AnimateDropdown("UIDropDown_Idle");
            if (levels.Count > 0) numbers.UpdateData(Enumerable.Range(1, levels.Count).ToList(), damageLevelIndex);
        }
    }
}
