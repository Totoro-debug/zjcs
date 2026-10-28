using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        public void ShowOriginalDamageTestResult(OfficialSkillDamageTestRequest request, FightBattleCheckpoint checkpoint,
            Action again, Action settings, Action leave)
        {
            BuildAuxiliary("damage_test_result", out var resultNodes, out _);
            var root = AuxObject(AuxObject(skillUiTemplates["damage_test_result"])["root"]);
            var binding = FindOriginalComponent(root, "GameEndPanelDamageTest");
            RectTransform Bound(string field) => resultNodes[Convert.ToString(binding[field])];
            string Format(string field, params object[] args) {
                var node = AuxiliaryNodes(root).Single(value => Convert.ToString(value["path"]) == Convert.ToString(binding[field]));
                string key = Convert.ToString(AuxObject(AuxObject(node["original_bindings"])["MultiLanguageText"])["myKey"]);
                return string.Format(CultureInfo.InvariantCulture, OriginalDetailText(key) ?? key, args);
            }
            bool won = checkpoint.Outcome.Won;
            Bound("mWinObject").gameObject.SetActive(won || request.EnableMonsterUnlimitHp);
            Bound("mLoseObject").gameObject.SetActive(!won && !request.EnableMonsterUnlimitHp);
            string winKey = request.EnableMonsterUnlimitHp ? "ui_dmagae_test_complete"
                : !won && checkpoint.Timeline.Round >= request.MaxRound ? "ui_dmagae_test_time_out" : "ui_battle_win_text";
            SetAuxiliaryText(Bound("mWinTitle"), OriginalDetailText(winKey));
            SetAuxiliaryText(Bound("mLostTitle"), OriginalDetailText("ui_battle_lose_text"));
            int rank = progression.Player.SubRank < 2 ? progression.Player.SubRank : 2 + (progression.Player.SubRank - 2) / 3;
            var ranks = (System.Collections.Generic.List<object>)AuxiliaryData["rank_names"];
            SetAuxiliaryText(Bound("mDamageTestTitle"), Format("mDamageTestTitle",
                OriginalDetailText("damage_test_stage_monster_" + request.StageId + "_name"),
                OriginalDetailText("Rank." + Convert.ToString(ranks[rank])), (request.Difficulty + 1).ToString("D2"),
                OriginalDetailText("ui_skill_stage_monsterhp_text_label"),
                OriginalDetailText(request.EnableMonsterUnlimitHp ? "ui_skill_stage_monsterhp_text_endless" : "ui_skill_stage_monsterhurt_text_normal"),
                OriginalDetailText("ui_skill_stage_monsterhurt_text_label"),
                OriginalDetailText(request.EnableMonsterOneDamage ? "ui_skill_stage_monsterhurt_text_one" : "ui_skill_stage_monsterhurt_text_normal")));
            // GameEndPanelDamageTest.UpdateDamageUI sums the player's normal,
            // pet and summon groups, then divides using integer arithmetic.
            long damage = checkpoint.Statistics.Units.Where(value => checkpoint.Units.Any(unit => unit.Id == value.UnitId && unit.Side == 1))
                .Sum(value => value.DamageDealt + value.AdditionalAttributedDamage);
            int round = checkpoint.Units.Any(unit => unit.Id == 1) ? checkpoint.Timeline.Round : 0;
            SetAuxiliaryText(Bound("mDamageCountText"), Format("mDamageCountText", OfficialPropFormat.Compact(damage)));
            SetAuxiliaryText(Bound("mRoundCountText"), Format("mRoundCountText", round));
            SetAuxiliaryText(Bound("mAvgDamageText"), Format("mAvgDamageText", OfficialPropFormat.Compact(round == 0 ? damage : damage / round)));
            SetAuxiliaryText(Bound("mGlobalRoundTest"), Format("mGlobalRoundTest", Math.Min(checkpoint.Timeline.Round, request.MaxRound), request.MaxRound));
            RectTransform hpNode = Bound("mHpSlider");
            var hp = hpNode.GetComponent<Slider>() ?? hpNode.gameObject.AddComponent<Slider>();
            OfficialSkillComponentBindings.Apply(hp, FindOriginalComponent(root, "Slider"), resultNodes);
            FightBattleUnitState player = checkpoint.Units.FirstOrDefault(unit => unit.Id == 1);
            hpNode.gameObject.SetActive(!request.EnableMonsterUnlimitHp && player != null);
            if (player != null) hp.value = player.LastMaxHp != 0 ? (float)player.Hp / player.LastMaxHp : 1f;
            SetOfficialState("damage_test_result", "PlayerInfoUI", "mStateSwitcher", 0, resultNodes);
            SetOfficialState("damage_test_result", "PlayerInfoUI", "mIsMyselfSwitcher", 1, resultNodes);
            var preview = Bound("mAppearanceUIPartHandler").gameObject.AddComponent<CharacterModelPreview>();
            preview.Initialize(progression, null, profileSlotId, profileSavesRoot);
            preview.UseOriginalPreviewShader(skillAssets.NativeCharacterShader);
            string animation = Convert.ToString(binding[won || request.EnableMonsterUnlimitHp ? "mWinAnimationName" : "mLoseAnimationName"]);
            if (!preview.PlayNativeAnimation(animation))
                throw new InvalidOperationException("Original damage-test result animation is unavailable: " + animation);
            Bound("mTestAgainButton").GetComponent<Button>().onClick.AddListener(() => { CloseAuxiliary(); again(); });
            Bound("mSettingButton").GetComponent<Button>().onClick.AddListener(() => { CloseAuxiliary(); settings(); OpenOriginalDamageTest(); });
            Bound("mLeaveGameBtn").GetComponent<Button>().onClick.AddListener(() => { CloseAuxiliary(); leave(); });
            auxiliaryReturn = leave;
            Action restoreResult = () => ShowOriginalDamageTestResult(request, checkpoint, again, settings, leave);
            var common = FindOriginalComponent(root, "GameEndPanelBase") ?? binding;
            resultNodes[Convert.ToString(common["mSkillItemReportBtn"])].GetComponent<Button>().onClick.AddListener(
                () => OpenOriginalBattleReport(checkpoint, request.MaxRound, restoreResult));
            var share = resultNodes[Convert.ToString(common["mGamePlaybackShareBtn"])].GetComponent<Button>();
            // Offline choice: statistics live only for this completed battle.
            // The online record/link share action is unavailable.
            share.gameObject.SetActive(false);
        }
    }
}
