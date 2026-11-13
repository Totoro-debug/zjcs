using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeIncomeRules;

public sealed partial class HomeIncomeSelectionHostRunner
{
    private sealed class Choice
    {
        public bool HasCondition, Met;
        public string Name;
    }

    private IEnumerator CheckIncomeSelection()
    {
        var fallback = new Choice { Name = "fallback" };
        var early = new Choice { Name = "early", HasCondition = true, Met = true };
        var late = new Choice { Name = "late", HasCondition = true, Met = true };
        var choices = new Dictionary<int, Dictionary<int, Choice>>
        {
            { 1, new Dictionary<int, Choice> { { 3, late }, { -1, fallback }, { 1, early } } },
            { 2, new Dictionary<int, Choice> { { -1, fallback } } }
        };
        var rewards = new RewardRuleInfoParser<Choice>(choices, row => row.Met, row => row.HasCondition);
        Require(rewards.Get(1) == early, "original rule selector tests ordered conditions before default");
        early.Met = false;
        Require(rewards.Get(1) == late, "next satisfied rule wins");
        late.Met = false;
        Require(rewards.Get(1) == fallback && rewards.Get(1, true) == early, "default and ignoreCondition retain original branches");
        Require(rewards.Get(2) == fallback && rewards.Get(999) == null && rewards.Get(1, 999) == null,
            "default-only and missing double keys");
        var rates = new HangUpRewardInfoParser<Choice>(choices, row => row.Met);
        Require(rates.Get(1) == late, "original binary rate selector keeps final positive row when all conditions fail");
        early.Met = true;
        Require(rates.Get(1) == early && rates.Get(2) == fallback && rates.Get(999) == null,
            "rate selector orders positive keys and keeps -1 fallback only when no positive keys exist");

        Require(home.Rules.IncomeVersion == "884_156692", "formal home opts into selected income tables");
        var selected = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
        Require(home.Rules.HangUps.Count == 12 && home.Rules.Data.hangUpRewards.Length == 11
            && home.Rules.Data.furnitureLevelRules.Length == 595 && home.Rules.Data.hangUpUpgradeMaterials.Length == 3024,
            "complete selected income, level and upgrade row counts");
        Require(home.Rules.HangUp(1).needRecordData && !home.Rules.HangUp(100).needRecordData,
            "original record ownership flags survive projection");
        Require(home.Rules.SelectRewardRule(11008, home.Context).ids.Single() == 1500,
            "selected 11008 yields 1500 instead of historical 1501");
        foreach (var row in selected.Rows("furniture_level_rule"))
            Require(home.Rules.LevelNumber(Convert.ToInt32(row["Id"]), Convert.ToInt32(row["Level"]))
                == (double)(float)Convert.ToDouble(row["LevelNum"]), "all selected level multipliers preserve original float precision");
        Require(home.Rules.LevelNumber(2, 163) == 0 && home.Rules.IncomeMaxStoreHours(2, 163) == 0,
            "missing selected level does not borrow a lower level or a 36-hour fallback");
        var definition = home.Session.Definitions.Single(row => row.HangUpId == 1);
        Require(definition.Rule.HourlyOutput == 50 && definition.Rule.LevelNum == home.Rules.LevelNumber(2, home.BlessingLevel)
            && definition.Rule.MaxStoreHours == 36, "actual bed session consumes selected rate, blessing multiplier and capacity");

        // Each condition reads the latest save, including composite references.
        // Standalone profiles here do not change the running home player's rank.
        var profile = new OfflineMainlineSave();
        var conditionPlayer = new OfflineHomeConditionPlayer(selected, () => profile, _ => false);
        var rules = OfficialHomeRules.LoadDefault();
        new OfflineCurrentHomeIncome(selected, OfficialPartnerAssetCatalog.LoadDefault().Conditions, () => conditionPlayer).Bind(rules);
        profile.Player.Wardrobe.Profession = 0;
        profile.Player.SubRank = 1;
        Require(rules.SelectRewardRule(11100, new HomeConditionContext { SubRank = 999 }).order == 172,
            "selected reward condition reads actual player, never stale supplied context");
        profile.Player.SubRank = 2;
        Require(rules.SelectRewardRule(11100, new HomeConditionContext()).order == 171,
            "reward selection refreshes after current profile rank changes");
        profile.Player.SubRank = 0;
        Require(rules.SelectRewardRule(11100, new HomeConditionContext()) == null,
            "no satisfied equipment reward has no invented default");
        var conditions = selected.CreateConditions(OfficialPartnerAssetCatalog.LoadDefault().Conditions);
        var faction = new Dictionary<string, object> { { "_type", "PlayerProfessionFactionCondition" }, { "Profession", 1 } };
        var exact = new Dictionary<string, object> { { "_type", "PlayerProfessionCondition" }, { "Profession", 3 } };
        profile.Player.Wardrobe.Profession = 3;
        Require(conditions.IsMet(faction, conditionPlayer) && conditions.IsMet(exact, conditionPlayer),
            "original profession 3 resolves its recursive 2-to-1 faction and exact identity");
        profile.Player.Wardrobe.Profession = 1;
        Require(conditions.IsMet(faction, conditionPlayer) && !conditions.IsMet(exact, conditionPlayer),
            "same faction identity and changed exact profession read current profile");
        profile.Player.Wardrobe.Profession = 10;
        Require(!conditions.IsMet(faction, conditionPlayer), "another profession lineage does not satisfy faction");
        profile.Player.Wardrobe.Profession = 999;
        Require(!conditions.IsMet(faction, conditionPlayer), "missing original profession row does not invent lineage");
        profile.Player.Wardrobe.Profession = 1; profile.Player.SubRank = 1;
        Require(rules.SelectRewardRule(11100, new HomeConditionContext()).order == 150,
            "profession faction equipment row takes priority over broad rank-only row");
        profile.Player.SubRank = 2;
        Require(rules.SelectRewardRule(11100, new HomeConditionContext()).order == 149,
            "cached composite conditions still read the latest rank and profession");
        yield return null;
        Debug.Log("HOME_INCOME_SELECTION_PASSED: original ordered selectors, all selected level multipliers, actual home catalog, version-specific 1500 reward, live player conditions and missing-level behavior.");
    }
}
