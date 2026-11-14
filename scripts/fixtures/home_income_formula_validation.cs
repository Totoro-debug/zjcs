using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.HomeIncomeRules;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Home;
using Zjcs.Offline.Commerce;

public sealed partial class HomeIncomeFormulaHostRunner
{
    private IEnumerator CheckIncomeFormula()
    {
        var flags = (ISet<int>)typeof(HomeFeatureRuntime).GetMethod("FloatPropertyIds",
            BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        Require(flags.SetEquals(HomeIncomePropertyCatalog.CreateFloatPropertyIds())
            && flags.Contains(1001) && flags.Contains(1005) && flags.Contains(1034) && flags.Contains(6004),
            "production home uses every selected official Float flag");
        var rule = new HangUpIncomeRule { HourlyOutput = 50, LevelNum = 1, MaxStoreHours = 36,
            EffectPercentPropertyId = 1001, EffectFixedPropertyId = 1000, FinallyEffectPercentPropertyId = 6004 };
        var props = new Dictionary<int, long> { { 1001, 100 } };
        Require(OfflineHangUpIncome.HourlyRate(rule, props, flags) == 50.5, "100 raw is one percent");
        rule.IsCart = true; props[1034] = 200; props[6004] = 300; props[1000] = 7;
        Require(Math.Abs(OfflineHangUpIncome.HourlyRate(rule, props, flags) - 60.045) < 0.000000001,
            "cart-wide percentage precedes final multiplier and fixed output");
        Require(new BattleFormulaHandler(HomeIncomePropertyCatalog.Get).GetProp(props, (PropType)int.MaxValue) == 0,
            "missing original property configuration returns zero");
        var fixedRule = new HangUpIncomeRule { HourlyOutput = 50, LevelNum = 2, MaxStoreHours = 36,
            EffectFixedPropertyId = 1000 };
        var fixedProps = new Dictionary<int, long> { { 1000, 10 } };
        var fixedRecord = new HangUpIncomeRecord { StoredSeconds = 1800 };
        Require(OfflineHangUpIncome.RewardCount(fixedRule, fixedProps, flags, .5) == 55
            && OfflineHangUpIncome.Preview(fixedRecord, fixedRule, fixedProps, flags).Count == 70
            && OfflineHangUpIncome.Collect(fixedRecord, fixedRule, fixedProps, flags) == 70
            && fixedRecord.StoredSeconds == 0,
            "confirmed ordinary collection uses original unsettled parameter order independently of fast/hourly output");
        fixedRecord.StoredSeconds = 1810;
        long unsettled = OfflineHangUpIncome.UnsettledRewardCount(fixedRule, fixedProps, flags, 1810, out double originalRemainder);
        Require(unsettled == 70 && originalRemainder > 0 && originalRemainder < 10
            && OfflineHangUpIncome.Collect(fixedRecord, fixedRule, fixedProps, flags) == unsettled
            && BitConverter.DoubleToInt64Bits(fixedRecord.StoredSeconds) == BitConverter.DoubleToInt64Bits(originalRemainder),
            "ordinary collection saves original fractional time without dividing by hourly rate");
        rule.IsCart = false; props.Clear();
        rule.LevelNum = 1.1;
        Require(BitConverter.DoubleToInt64Bits(OfflineHangUpIncome.HourlyRate(rule, props, flags)) == 0x404b800000000001L,
            "original FurnitureLevelRuleInfo.LevelNum remains double instead of rounding 1.1 through float");
        rule.LevelNum = 1;
        props[1000] = 16777217;
        props[1001] = 0;
        Require(OfflineHangUpIncome.HourlyRate(rule, props, flags) == 16777266,
            "official fixed output casts to float before division");
        rule.HourlyOutput = 0.1; rule.EffectFixedPropertyId = 0;
        Require(BitConverter.DoubleToInt64Bits(OfflineHangUpIncome.HourlyRate(rule, props, flags)) == 0x3fb99999a0000000L,
            "official hourly output retains single precision");
        rule.HourlyOutput = 2; rule.LevelNum = 6.503173351287842;
        rule.EffectFixedPropertyId = 1000; props[1000] = 22; props[1001] = 6102;
        Require((long)OfflineHangUpIncome.RewardCount(rule, props, flags, 231.16786752053045) == 9926,
            "original hour-first arithmetic preserves integer boundary; multiplying hourly rate would grant 9927");
        rule.LevelNum = 1; props[1001] = -10000; props[1000] = 0;
        Require(double.IsNaN(OfflineHangUpIncome.HourlyRate(rule, props, flags)), "original zero factor division is preserved");
        rule.LevelNum = 0;
        Require(OfflineHangUpIncome.HourlyRate(rule, props, flags) == 0, "original nonpositive level branch");
        CheckAccelerationTransactions(flags);
        CheckFastBatchReceipts(flags);
        CheckFastOperationRecovery(flags);
        CheckCollectionReceipts(flags);
        CheckFreeFastForwardCalendar(flags);
        Require(home.Rules.IsFunctionLimited(164) && !home.Rules.IsFunctionLimited(53, 1)
            && !home.Rules.IsFunctionLimited(163),
            "selected bed function rows keep quick receive locked below level 30 and absent gates open");
        Require(BitConverter.DoubleToInt64Bits(home.Rules.LevelNumber(1, 2)) == BitConverter.DoubleToInt64Bits(1.1),
            "selected raw furniture level multiplier reaches the actual income catalog without a float cast");
        var originalOutput = new Zjcs.Offline.HomeUI.OfflineHomeFurnitureOutput(home.Rules,
            home.HomeIncomeProperties, _ => 2, home.FormatTopBarCurrency, key => key);
        originalOutput.Refresh();
        double expectedBedBase = 50 * home.Rules.LevelNumber(home.Rules.HangUp(1).levelRuleId, 2);
        Require(BitConverter.DoubleToInt64Bits(originalOutput.GetHangUpReward(1, 1, 2, true))
                == BitConverter.DoubleToInt64Bits(expectedBedBase)
            && BitConverter.DoubleToInt64Bits(originalOutput.GetHangUpReward(1, 1, 2))
                == BitConverter.DoubleToInt64Bits(expectedBedBase),
            "original bed output projection retains double level precision in base and actual output");

        double beforeRate = home.Session.Preview(1).HourlyRate;
        int beforeBlessing = home.BlessingLevel;
        Require(!home.PlayerProperties().TryGetValue(1000, out long fixedOutput) || fixedOutput == 0,
            "plain profile has no fixed bed output");
        home.Close(); CloseCapture(); yield return null;
        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        var character = OfficialCharacterAssetCatalog.LoadDefault().Load();
        var lost = character.Rows("map_region").First(row => Convert.ToInt32(row["ClassId"]) == 11
            && row.TryGetValue("GoddessActivePropAwardDict", out object value)
            && value is Dictionary<string, object> values && values.Count > 0);
        int region = Convert.ToInt32(lost["Region"]);
        Require(profile.InitializeMap(11, character.Rows("map_region")
            .Where(row => Convert.ToInt32(row["ClassId"]) == 11)
            .Select(row => Convert.ToInt32(row["Region"])).Distinct().ToArray()).IsApplied,
            "initialize actual selected map region identities in isolated profile");
        Require(profile.SetMapRegionProgress(11, region, 10000).IsApplied,
            "seed required explored region in isolated profile");
        var mutation = profile.RecordGoddessActivated(11, region, "income-fixture:goddess");
        Require(mutation.IsApplied, "persist confirmed lost goddess state: " + mutation.Message);
        Open(); yield return new WaitForSeconds(1);
        Require(home.PlayerProperties()[1001] == 100 && home.BlessingLevel == beforeBlessing,
            "actual lost goddess adds 100 raw without adding ordinary goddess blessing level");
        Require(Math.Abs(home.Session.Preview(1).HourlyRate / beforeRate - 1.01) < 0.000000001,
            "real restored profile gains one percent bed income");
        Require(home.Session.Mutate("income-fixture:hour", save =>
        {
            var income = save.Income.SingleOrDefault(value => value.HangUpId == 1);
            if (income == null) save.Income.Add(income = new OfflineHomeIncomeEntry { HangUpId = 1 });
            income.SettledCount = 0; income.StoredSeconds = 3600;
        }), "persist actual one-hour bed snapshot");
        profile.Reload(); long experience = profile.Save.Player.Experience;
        long expected = (long)home.Session.Preview(1).HourlyRate;
        var sharedSoundHost = Zjcs.Official.ProfessionUI.OfficialWwiseEvent.Host;
        Require(home.TryCollect(1) == expected && expected > 0, "production collection uses migrated arithmetic");
        profile.Reload();
        Require(profile.Save.Player.Experience == experience + expected && home.Session.Save.Inventory.Count == 0,
            "experience reaches real character and durable ledger is acknowledged");
        yield return new WaitForSeconds(0.2f);
        var queue = UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeAwardUI.AwardListPanel>();
        var awardIcon = UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeAwardUI.HomeAwardIcon>();
        Require(queue && awardIcon && awardIcon.ItemIcon && awardIcon.ItemIcon.myInfo.ClassId == 50,
            "committed production receipt opens the original queue with the selected XP icon");
        var displayed = (Zjcs.Official.HomeAwardUI.AwardData)typeof(Zjcs.Official.HomeAwardUI.HomeAwardIcon)
            .GetField("mAwardData", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(awardIcon);
        Require(displayed.Amount == expected && displayed.AwardId == 50,
            "original queue consumes only the committed claim delta without granting again");
        Require(queue.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text.Contains("经验")),
            "production award label resolves the selected original Chinese text");
        var sound = (AudioSource)typeof(HomeHudView).GetField("partnerUiAudioSource",
            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(home.Hud);
        Require(sound && sound.isPlaying && AudioListener.volume == 0
            && ReferenceEquals(sharedSoundHost, Zjcs.Official.ProfessionUI.OfficialWwiseEvent.Host),
            "original award sound is playing silently through its owner without replacing the global host");
        yield return new WaitForSeconds(0.25f);
        Capture("home-income-production-award.png");
        double remainder = home.Session.Save.Income.Single(value => value.HangUpId == 1).StoredSeconds;
        Require(remainder > 0 && remainder < 3600 && home.TryCollect(1) == 0, "fractional time remains without a duplicate grant");
        long freeExpected = (long)OfflineHangUpIncome.RewardCount(home.Session.Definitions.Single(value => value.HangUpId == 1).Rule,
            home.PlayerProperties(), flags, 2);
        Require(home.TryFastForward(1) == freeExpected && home.TryFastForward(1) == 0,
            "actual free acceleration uses original arithmetic and saved daily limit");
        profile.Reload(); long awardedExperience = profile.Save.Player.Experience;
        Require(awardedExperience == experience + expected + freeExpected, "free acceleration grants experience once");
        long checkpoint = home.Session.Save.LastLocalUnixSeconds;
        double checkpointSubsecond = home.Session.Save.LastIncomeSubsecond;
        double remainingSeconds = home.Session.Save.Income.Single(value => value.HangUpId == 1).StoredSeconds;
        Capture("home-income-collected.png");
        home.Close(); CloseCapture(); yield return null;
        Open(); yield return new WaitForSeconds(1); profile.Reload();
        Require(!UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeAwardUI.AwardListPanel>(),
            "closing and reopening home releases the previous production award queue");
        double elapsed = home.Session.Save.LastLocalUnixSeconds - checkpoint
            + home.Session.Save.LastIncomeSubsecond - checkpointSubsecond;
        Require(profile.Save.Player.Experience == awardedExperience && home.Session.Save.Inventory.Count == 0
            && Math.Abs(home.Session.Save.Income.Single(value => value.HangUpId == 1).StoredSeconds
                - Math.Min(36 * 3600, remainingSeconds + elapsed)) < 0.000001,
            "disk reopen preserves grants, remainder and consumed ledger while accruing real elapsed time");
        Capture("home-income-reopened.png");
        var beforeCard = new Dictionary<int, long>(home.PlayerProperties());
        var cards = OfficialCommerceCatalogView.LoadDefault();
        var card = cards.Data.periodicCards.First(value => value.extraProps != null
            && value.extraProps.Any(prop => prop.value != 0 && new[] { 1001, 1005, 1034, 6004 }.Contains(prop.propId)));
        var commerce = OfflineCommerceSession.Open(new ProfileSlotCatalog(root), slot, DateTime.Now, home.Profile);
        Require(commerce.UpdateState(state => {
            string key = "periodic-expiry:" + card.id;
            state.Bought.RemoveAll(value => value.Key == key);
            state.Bought.Add(new CommerceCounter { Key = key, Count = state.Day + 1 });
        }), "persist real periodic card and synchronize its character property chunk");
        home.Profile.Reload();
        foreach (var prop in card.extraProps.Where(value => value.value != 0))
        {
            beforeCard.TryGetValue(prop.propId, out long prior);
            Require(home.PlayerProperties()[prop.propId] == prior + prop.value,
                "periodic card non-battle property is included once, without a second commerce merge");
        }
        yield return CheckNativeBedCollection();
        yield return CheckIncomeRewardCatalog();
        CheckFastAutoIncrease();
        yield return CheckActualFastRequests();
        yield return CheckExperienceLedgerRecovery();
        Debug.Log("HOME_INCOME_FORMULA_PASSED: selected Float flags, original float/double arithmetic and unsettled-preview settlement, actual native repair and production bed pointer, bed output foldout, original blessing rule rows/highlight/scroll/mask close, real lost goddess profile, production collection/experience/free acceleration, original XP award queue, all 259 selected income items and 15 images, actual cart repair/unlock/collection and native award queue with disk reopen, committed reward receipts, rejected experience grants retained, acknowledgement recovery, original remainder, idempotency and precise disk reopen. Cart award presentation is a component check; acceleration business calls are direct; acceleration/output-detail windows and complete HUD remain pending.");
    }

    private IEnumerator CheckNativeBedCollection()
    {
        Require(home.Session.Mutate("native-bed:seed:" + Guid.NewGuid().ToString("N"), save => {
            var entry = save.Income.Single(value => value.HangUpId == 1);
            entry.SettledCount = 0; entry.StoredSeconds = 1800;
        }), "seed actual saved income for the native bed pointer");
        SceneClick(0);
        yield return new WaitForSeconds(.8f);
        var bed = UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeHangUpWindowsUI.FurnitureBedDialog>();
        Require(bed && bed.OutputInfo.OutputServices is Zjcs.Offline.HomeUI.OfflineHomeFurnitureOutput,
            "formal scene pointer opens the complete original bed with actual property services");
        Require(bed.Name.Text == Zjcs.Official.HomeHangUpWindowsUI.OfficialHomeHangUpWindowAssetCatalog.LoadDefault().Text("item_4000_name"),
            "original controller retains the furniture title rather than replacing it with the reward name");
        Require(!bed.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(value => value.text == "hang_up_1_base_output"),
            "native bed binds the selected dynamic base-output translation");
        Capture("home-bed-production-native.png");
        var ruleButton = bed.GetComponentInChildren<Zjcs.Official.HomeHangUpWindowsUI.BlessingRuleBtn>(true);
        Click(ruleButton.RuleBtn); yield return new WaitForSeconds(.8f);
        var rule = UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeHangUpWindowsUI.BlessingLevelRuleDialog>();
        Require(rule, "original bed blessing button opens its original rule window");
        var ruleRows = rule.GetComponentsInChildren<Zjcs.Official.HomeHangUpWindowsUI.BlessingLevelRuleItem>()
            .Where(row => row.gameObject.activeInHierarchy).ToArray();
        Require(ruleRows.Length == 11 && ruleRows.Count(row => row.GetComponentsInChildren<TMPro.TMP_Text>()
            .Any(text => Math.Abs(text.transform.localScale.x - 1.2f) < .0001f)) == 1,
            "original rule pool displays eleven levels and highlights the actual saved blessing level");
        Capture("home-bed-production-blessing-rule.png");
        var ruleScroll = rule.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
        Vector2 scrollPoint = RectTransformUtility.WorldToScreenPoint(home.Scene.Camera,
            ruleScroll.viewport.TransformPoint(ruleScroll.viewport.rect.center));
        var scrollPointer = new PointerEventData(EventSystem.current) {
            position = scrollPoint, scrollDelta = new Vector2(0, -100) };
        var scrollHits = new List<RaycastResult>(); EventSystem.current.RaycastAll(scrollPointer, scrollHits);
        Require(scrollHits.Count > 0 && ExecuteEvents.GetEventHandler<IScrollHandler>(scrollHits[0].gameObject)
            == ruleScroll.gameObject, "rule table pointer reaches the original scroll view");
        float scrollBefore = ruleScroll.content.anchoredPosition.y;
        for (int wheel = 0; wheel < 12 && ruleScroll.verticalNormalizedPosition > .001f; wheel++)
        {
            ExecuteEvents.ExecuteHierarchy(scrollHits[0].gameObject, scrollPointer, ExecuteEvents.scrollHandler);
            yield return null;
        }
        yield return new WaitForSeconds(.3f);
        Require(ruleScroll.content.anchoredPosition.y > scrollBefore && ruleScroll.verticalNormalizedPosition < .01f,
            "original rule table scrolls to its final level through internal wheel input: "
                + ruleScroll.verticalNormalizedPosition + ", " + ruleScroll.content.anchoredPosition);
        Capture("home-bed-production-blessing-scrolled.png");
        var rulePopup = rule.GetComponent<Zjcs.Official.WorldUI.WorldPopupWindow>();
        var ruleMask = UnityEngine.Object.FindObjectsOfType<Zjcs.Official.WorldUI.WorldWindowMask>()
            .Single(value => value.AttachWindow == rulePopup);
        var outside = new PointerEventData(EventSystem.current) {
            position = new Vector2(8, target.height - 8), button = PointerEventData.InputButton.Left };
        var maskHits = new List<RaycastResult>(); EventSystem.current.RaycastAll(outside, maskHits);
        Require(maskHits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(maskHits[0].gameObject) == ruleMask.Button.gameObject,
            "outside rule pointer first hits the original attached mask");
        ExecuteEvents.ExecuteHierarchy(maskHits[0].gameObject, outside, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(maskHits[0].gameObject, outside, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(maskHits[0].gameObject, outside, ExecuteEvents.pointerClickHandler);
        yield return new WaitForSeconds(.8f);
        Require(!UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeHangUpWindowsUI.BlessingLevelRuleDialog>()
            && bed.isActiveAndEnabled, "original mask click removes the rule while retaining the underlying bed");
        bool folded = bed.OutputFoldout;
        Click(bed.OutputFoldoutBtn); yield return null;
        Require(bed.OutputFoldout != folded, "original output foldout responds to internal pointer");
        Capture("home-bed-production-output.png");
        Click(bed.OutputFoldoutBtn); yield return null;
        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        long before = profile.Save.Player.Experience;
        long expected = home.Session.Preview(1).Count;
        Require(expected > 0, "actual native bed has a claim");
        Click(bed.GetHangUpRewardBtn);
        yield return new WaitForSeconds(.2f); profile.Reload();
        Require(profile.Save.Player.Experience == before + expected && home.Session.Save.Inventory.Count == 0,
            "native bed pointer awards the actual response exactly once and acknowledges its durable ledger");
        Click(bed.GetHangUpRewardBtn); profile.Reload();
        Require(profile.Save.Player.Experience == before + expected, "empty native claim does not duplicate XP");
        Capture("home-bed-production-collected.png");
        home.Hud.CloseDialog(); yield return null;
        Require(!UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeHangUpWindowsUI.FurnitureBedDialog>(),
            "native bed closes and releases the whole original controller");
        SceneClick(0); yield return new WaitForSeconds(.8f);
        Require(UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeHangUpWindowsUI.FurnitureBedDialog>(),
            "actual native bed can reopen without stale services or subscriptions");
        home.Hud.CloseDialog(); yield return null;
    }

    [Serializable] private sealed class IncomeRewardPayload { public IncomeRewardTables tables; }
    [Serializable] private sealed class IncomeRewardTables { public IncomeRewardItem[] item; }
    [Serializable] private sealed class IncomeRewardItem
    {
        public int ClassId, ItemType, Quality, Rank;
        public string RawIcon;
        public bool CanOverlap;
    }

    private IEnumerator CheckIncomeRewardCatalog()
    {
        var character = OfficialCharacterAssetCatalog.LoadDefault().Load();
        Require(home.Profile.ApplyCharacterMutation(player => {
            // LevelCondition reads BaseLevel, capped by the personal SubRank.
            // Selected system_level_limit caps SubRank 1 at 10 and SubRank 2 at 20.
            player.SubRank = 2;
            player.Level = 15; player.Experience = character.CumulativeLevelExperience[15];
        }, "income-reward:level").IsApplied, "seed actual cart unlock level in the isolated profile");
        home.Close(); CloseCapture(); yield return null;
        Open(); yield return new WaitForSeconds(1);
        Require(home.TryRepair(10), "actual selected cart repair transaction");
        Require(home.Session.IsHangUpUnlocked(2) || home.TryUnlockHangUp(2),
            "actual selected cart unlock condition and transaction");
        Require(home.Session.Mutate("income-reward:seed", save => {
            var entry = save.Income.SingleOrDefault(value => value.HangUpId == 2);
            if (entry == null) save.Income.Add(entry = new OfflineHomeIncomeEntry { HangUpId = 2 });
            entry.StoredSeconds = 3600; entry.SettledCount = 0;
        }), "seed actual configured cart income");
        var definition = home.Session.Definitions.Single(value => value.HangUpId == 2);
        var possibleItems = definition.OutputItemIds?.Length > 0
            ? definition.OutputItemIds : new[] { definition.OutputItemId };
        var inventoryBefore = possibleItems.ToDictionary(id => id, home.Profile.InventoryCount);
        var reward = home.Session.CollectReward(2, "income-reward:collect");
        Require(reward != null && possibleItems.Contains(reward.ItemId) && reward.ItemId != 50 && reward.Count > 0,
            "actual cart response selects the configured non-XP reward");
        int expectedItem = reward.ItemId;
        long before = inventoryBefore[expectedItem];
        home.DrainHomeInventory();
        Require(home.Profile.InventoryCount(expectedItem) == before + reward.Count,
            "actual cart receipt reaches the real profile once");
        var postMethod = home.Hud.GetType().GetMethod("PostPartnerPromptSound", BindingFlags.Instance | BindingFlags.NonPublic);
        var post = (Action<AudioClip, GameObject>)Delegate.CreateDelegate(typeof(Action<AudioClip, GameObject>), home.Hud, postMethod);
        using (var awards = new OfflineHomeBedAwards(home, home.Hud.CanvasTransform.GetComponent<Canvas>(), post))
        {
            var assets = Zjcs.Official.HomeFastHangUpUI.OfficialHomeFastHangUpAssetCatalog.LoadDefault();
            var selected = JsonUtility.FromJson<IncomeRewardPayload>(assets.rules.text).tables.item;
            Require(selected.Length == 259 && assets.images.Length == 15,
                "all configured income reward and acceleration-cost definitions use unique original images");
            foreach (var row in selected)
            {
                var item = awards.GetItemInfo(row.ClassId);
                Require(item.ClassId == row.ClassId && item.Icon == row.RawIcon && item.CanOverlap == row.CanOverlap
                    && (int)item.ItemType == row.ItemType && (int)item.Quality == row.Quality && item.Rank == row.Rank,
                    "selected original income item projection: " + row.ClassId);
                using var boundSprite = awards.LoadSprite(item.Icon);
                Require(boundSprite.Result, "selected original income image lease: " + row.ClassId);
            }
            Require(awards.IsIgnoreShowAward(56) && !awards.IsIgnoreShowAward(expectedItem),
                "original ignored-award setting is applied before queue display");
            awards.Show(reward);
            yield return new WaitForSeconds(.3f);
            var icon = UnityEngine.Object.FindObjectsOfType<Zjcs.Official.HomeAwardUI.HomeAwardIcon>()
                .Single(value => value.ItemIcon && value.ItemIcon.myInfo?.ClassId == expectedItem);
            using var expectedSprite = awards.LoadSprite(awards.GetItemInfo(expectedItem).Icon);
            Require(icon.GetComponentsInChildren<UnityEngine.UI.Image>().Any(value => value.sprite == expectedSprite.Result),
                "actual cart receipt renders its original reward image through the original queue");
            Capture("home-income-production-cart-award.png");
            Require(home.Profile.InventoryCount(expectedItem) == before + reward.Count && home.Session.Save.Inventory.Count == 0,
                "reward presentation does not issue another grant or recreate a ledger stack");
        }
        home.Close(); CloseCapture(); yield return null;
        Open(); yield return new WaitForSeconds(1);
        Require(home.Profile.InventoryCount(expectedItem) == before + reward.Count,
            "cart reward persists after reopening without another grant");
        var cartDefinition = home.Session.Definitions.Single(value => value.HangUpId == 2);
        var bedDefinition = home.Session.Definitions.Single(value => value.HangUpId == 1);
        var flags = HomeIncomePropertyCatalog.CreateFloatPropertyIds();
        long cartFastCount = (long)OfflineHangUpIncome.RewardCount(cartDefinition.Rule, home.PlayerProperties(), flags, 2);
        long bedFastCount = (long)OfflineHangUpIncome.RewardCount(bedDefinition.Rule, home.PlayerProperties(), flags, 2);
        var beforeFastItems = possibleItems.ToDictionary(id => id, home.Profile.InventoryCount);
        long beforeFastExperience = home.Profile.Save.Player.Experience;
        Require(home.Session.Mutate("income-reward:reset-free", save => save.FastForwardUsage.Clear()),
            "reset only the isolated fixture's consumed free use");
        Require(cartDefinition.FastHangUpId == -1 && cartFastCount > 0
            && home.TryFastForward(1) == bedFastCount + cartFastCount,
            "actual configured bed acceleration includes the unlocked cart with no independent fast ID");
        Require(home.Profile.Save.Player.Experience == beforeFastExperience + bedFastCount
            && possibleItems.Sum(id => home.Profile.InventoryCount(id) - beforeFastItems[id]) == cartFastCount
            && home.Session.Save.Inventory.Count == 0 && home.TryFastForward(1) == 0,
            "actual bed and random cart acceleration reach the profile exactly once under one free use");
    }

    private void CheckFastAutoIncrease()
    {
        var now = new DateTime(2026, 11, 14, 1, 0, 0, DateTimeKind.Utc);
        OfflineItemStack Stack(long count, double speed = 5, double fraction = 0,
            double elapsedHours = 0, double bonusSeconds = 0) => new OfflineItemStack {
                ItemId = 57, Count = count, AutoIncrease = new OfflineItemAutoIncreaseProgress {
                    IncreaseSpeed = speed, MaxCount = 300, ItemCountLimit = 1200, Decimal = fraction,
                    LastUpdateTimeTicks = now.AddHours(-elapsedHours).Ticks, BonusSeconds = bonusSeconds }
            };
        var below = Stack(100, fraction: .25, elapsedHours: .5, bonusSeconds: 1800);
        Require(OfflineItemAutoIncrease.SettleFastForward(below, 2, false, 0, now)
            && below.Count == 115 && below.AutoIncrease.Decimal == 0
            && Math.Abs(below.AutoIncrease.BonusSeconds - 180) < .000001
            && below.AutoIncrease.LastUpdateTimeTicks == now.Ticks,
            "original extra reward combines elapsed time, prior fraction and bonus seconds below the natural cap");
        var near = Stack(299, fraction: .5, elapsedHours: 1);
        Require(OfflineItemAutoIncrease.SettleFastForward(near, .1, false, 0, now)
            && near.Count == 305 && near.AutoIncrease.Decimal == 0 && near.AutoIncrease.BonusSeconds == 0,
            "original extra reward reaches the natural cap before awarding overflow with the remaining duration");
        var full = Stack(300);
        Require(OfflineItemAutoIncrease.SettleFastForward(full, 2.1, false, 0, now)
            && full.Count == 310 && full.AutoIncrease.LastUpdateTimeTicks == now.AddSeconds(-360).Ticks
            && full.AutoIncrease.Decimal == 0 && full.AutoIncrease.BonusSeconds == 0,
            "original post-cap fractional time is consumed by the update notification as a timestamp offset");
        var limit = Stack(300);
        Require(OfflineItemAutoIncrease.SettleFastForward(limit, 1000, false, 0, now) && limit.Count == 1200,
            "fast extras can exceed the natural cap while retaining the original item holding limit");
        var activity = Stack(100, speed: 10);
        var includedActivity = Stack(100, speed: 10);
        Require(OfflineItemAutoIncrease.SettleFastForward(activity, 2, false, 10000, now)
            && OfflineItemAutoIncrease.SettleFastForward(includedActivity, 2, true, 10000, now)
            && activity.Count == 110 && includedActivity.Count == 120,
            "original activity offset divides duration only when the request excludes output addition");
        var stopped = Stack(0, speed: 0);
        Require(OfflineItemAutoIncrease.SettleFastForward(stopped, .25, false, 0, now)
            && stopped.Count == 0 && stopped.AutoIncrease.BonusSeconds == 900,
            "original nonpositive-result branch retains the added duration without inventing an item grant");
        var absent = new OfflineItemStack { ItemId = 56, Count = 7 };
        Require(!OfflineItemAutoIncrease.SettleFastForward(absent, 2, false, 0, now)
            && absent.Count == 7 && absent.AutoIncrease == null,
            "extra reward preserves the original no-parameter branch instead of creating a missing record");
        DateTime actualNow = DateTime.UtcNow;
        var prior = home.Profile.Save.Player.Inventory.Single(value => value.ItemId == 57);
        long priorStamina = prior.Count;
        Require(prior.AutoIncrease != null && prior.Count == 320 && prior.AutoIncrease.IncreaseSpeed == 5,
            "two production free requests have each applied the original ten-stamina extra exactly once");
        var applied = home.Profile.ApplyInventoryTransaction("fast-extra:stamina", save => {
            var stamina = save.Player.Inventory.Single(value => value.ItemId == 57);
            Require(OfflineItemAutoIncrease.SettleFastForward(stamina, 2.1, false, 0, actualNow),
                "actual saved stamina accepts the original extra reward handler");
        });
        Require(applied.IsApplied, "extra reward commits through the real profile inventory transaction");
        var repeated = home.Profile.ApplyInventoryTransaction("fast-extra:stamina", _ =>
            throw new InvalidOperationException("A repeated extra-reward transaction must not execute again."));
        Require(repeated.Status == ProgressionResultStatus.Unchanged || repeated.Status == ProgressionResultStatus.Ready,
            "actual profile suppresses a repeated extra-reward receipt");
        var reopened = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        var restored = reopened.Save.Player.Inventory.Single(value => value.ItemId == 57);
        Require(restored.Count == priorStamina + 10 && restored.AutoIncrease.LastUpdateTimeTicks == actualNow.AddSeconds(-360).Ticks
            && restored.AutoIncrease.Decimal == 0 && restored.AutoIncrease.BonusSeconds == 0,
            "actual disk reopen retains the extra reward and consumed timestamp remainder exactly once");
    }

    private IEnumerator CheckActualFastRequests()
    {
        var service = new Zjcs.Offline.HomeUI.OfflineHomeFastForwardRequests(home.Rules, home.Session,
            home.Profile, () => new HomeConditionContext { Level = home.Profile.Save.Player.Level },
            new Zjcs.Offline.HomeUI.OfflineHomeOutputAddition(home.Session, () => DateTime.UtcNow, _ => false));
        Require(home.Profile.ApplyInventoryTransaction("fast-request:seed", save => {
            save.Player.Inventory.RemoveAll(value => value.ItemId == 120);
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 120, Count = 5 });
            save.Player.Health.HpPercentVal = .4f;
        }).IsApplied, "seed isolated real time crystals and damaged health");
        long stamina = home.Profile.Save.Player.Inventory.Single(value => value.ItemId == 57).Count;
        long experience = home.Profile.Save.Player.Experience;
        var timers = home.Session.Save.RandomRewards.Where(value => value.ItemId > 0)
            .ToDictionary(value => value.Slot, value => value.NextRefreshUnixSeconds);
        var flags = HomeIncomePropertyCatalog.CreateFloatPropertyIds();
        long expectedXp = (long)OfflineHangUpIncome.RewardCount(home.Session.Definitions.Single(value => value.HangUpId == 1).Rule,
            home.PlayerProperties(), flags, 4);
        Require(service.PrepareItems(1, new Dictionary<int, long> { { 120, 101 } }, "too-many") == null
            && service.PrepareItems(1, new Dictionary<int, long> { { 120, 6 } }, "too-expensive") == null,
            "official count limit and actual crystal stock reject before staging or charging");
        var operation = service.PrepareItems(1, new Dictionary<int, long> { { 120, 2 } }, "fast-request:recover");
        Require(operation != null && operation.Hours == 4 && operation.HealToFull
            && operation.RewindRandomRewards && operation.AutoIncrease.All(value => !value.HasOutputAddition)
            && operation.AutoIncrease.Select(value => value.ItemId).OrderBy(value => value).SequenceEqual(new[] { 56, 57 }),
            "actual selected extra rules retain original fruit, stamina and treatment branches");
        Require(service.CommitPendingProfile(DateTime.UtcNow).IsApplied,
            "real request commits costs, auto-increase extras and treatment in one character receipt");
        Require(home.Profile.InventoryCount(120) == 3
            && home.Profile.Save.Player.Inventory.Single(value => value.ItemId == 57).Count == stamina + 20
            && home.Profile.Save.Player.Health.HpPercentVal == 1
            && home.Profile.Save.Player.Inventory.All(value => value.ItemId != 56)
            && home.Session.PendingFastForwardOperation != null && home.Profile.Save.Player.Experience == experience,
            "profile phase consumes crystals once, grants stamina and health, preserves absent fruit and defers normal output");
        Require(service.CommitPendingProfile(DateTime.UtcNow).Status == ProgressionResultStatus.Unchanged
            && home.Profile.InventoryCount(120) == 3, "repeated profile phase never consumes or grants twice");
        home.Close(); CloseCapture(); yield return null;
        Open(); yield return new WaitForSeconds(1);
        Require(home.Session.PendingFastForwardOperation == null && home.Session.Save.Inventory.Count == 0
            && home.Profile.InventoryCount(120) == 3
            && home.Profile.Save.Player.Inventory.Single(value => value.ItemId == 57).Count == stamina + 20
            && home.Profile.Save.Player.Experience == experience + expectedXp
            && timers.All(value => home.Session.Save.RandomRewards.Single(slot => slot.Slot == value.Key)
                .NextRefreshUnixSeconds == value.Value - 4)
            && home.Profile.Save.Player.AppliedInventoryEventIds.Count(value => value == operation.ProfileReceipt) == 1,
            "production home reopen recovers already charged request, drains original normal income and preserves extras exactly once");
        long beforePaid = home.Profile.InventoryCount(120);
        Require(home.TryFastForwardItems(1, 1) > 0 && home.Profile.InventoryCount(120) == beforePaid - 1
            && home.Profile.Save.Player.Inventory.Single(value => value.ItemId == 57).Count == stamina + 30
            && home.Session.PendingFastForwardOperation == null,
            "production paid entry uses the same recoverable cost and extra reward boundary");
        var retry = new Zjcs.Offline.HomeUI.OfflineHomeFastForwardRequests(home.Rules, home.Session,
            home.Profile, () => new HomeConditionContext { Level = home.Profile.Save.Player.Level },
            new Zjcs.Offline.HomeUI.OfflineHomeOutputAddition(home.Session, () => DateTime.UtcNow, _ => false));
        Require(retry.PrepareItems(1, new Dictionary<int, long> { { 120, 2 } }, "fast-request:unpaid-cancel") != null,
            "stage uncharged request before a competing inventory spend");
        Require(home.Profile.ApplyInventoryTransaction("fast-request:competing-spend", save => {
            Require(OfflineInventoryOperations.Consume(save.Player, 120, 2, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied,
                "consume remaining isolated crystals through the real inventory operation");
        }).IsApplied, "competing inventory transaction persists");
        Require(retry.CompletePending(DateTime.UtcNow) == null && home.Session.PendingFastForwardOperation == null
            && home.Profile.InventoryCount(120) == 0
            && !home.Profile.Save.Player.AppliedInventoryEventIds.Contains("fast-request:unpaid-cancel:profile")
            && home.Profile.Save.Player.Inventory.Single(value => value.ItemId == 57).Count == stamina + 30,
            "insufficient uncharged request cancels without any extra reward or false character receipt");
        Capture("home-income-paid-request-recovered.png");
    }

    private void CheckFastOperationRecovery(ISet<int> flags)
    {
        var definition = new OfflineHomeIncomeDefinition {
            HangUpId = 1, OutputItemId = 50, FastHangUpId = 1, Unlocked = true,
            FreeFastForwardLimit = 1, FreeFastForwardHours = 2,
            Rule = new HangUpIncomeRule { HourlyOutput = 5, LevelNum = 1, MaxStoreHours = 0,
                EffectFixedPropertyId = 1000 }
        };
        var props = new Dictionary<int, long> { { 1000, 2 } };
        string path = System.IO.Path.Combine(root, "fast-operation.json");
        var session = OfflineHomeIncomeSession.Open(path, new[] { definition }, props, flags, 1700000000);
        Require(session.Mutate("journal:random-timer", save => save.RandomRewards.Add(
            new OfflineHomeRandomRewardSlot { Slot = 0, NextRefreshUnixSeconds = 1700001000 })),
            "seed original random reward refresh timer");
        var operation = session.PrepareFastForwardOperation(1, new[] { 1 }, 2, false,
            new[] { new HomeCostEntry { itemId = 120, count = 1 } },
            new[] { new OfflineHomeFastForwardAutoIncrease { HangUpId = 101, ItemId = 57 } }, true, "paid-request", true);
        Require(operation != null && operation.ProfileReceipt == "paid-request:profile"
            && operation.AcceptedUnixSeconds == 1700000000 && operation.AcceptedSubsecond == 0
            && session.Save.Inventory.Count == 0 && !session.CanFreeFastForward(1),
            "paid intent persists accepted time before any normal reward and blocks concurrent free eligibility");
        string before = JsonUtility.ToJson(session.Save);
        Require(session.CompleteFastForwardOperation("paid-request", _ => false) == null
            && session.ApplyFastForwardBatchRewards(new[] { 1 }, 2, "concurrent") == null
            && JsonUtility.ToJson(session.Save) == before, "unpaid intent and concurrent requests cannot grant rewards");
        operation.Costs[0].count = 99;
        Require(session.PendingFastForwardOperation.Costs[0].count == 1, "returned journal is detached from persisted request");
        definition.Rule.HourlyOutput = 999;
        props[1000] = 999;
        session = OfflineHomeIncomeSession.Open(path, new[] { definition }, props, flags, 1700000000);
        Require(session.PendingFastForwardOperation.HealToFull
            && session.PendingFastForwardOperation.AutoIncrease.Single().ItemId == 57,
            "reopen recovers accepted extra-handler inputs");
        Require(!session.CancelUnpaidFastForwardOperation("paid-request", _ => true), "charged request cannot be cancelled");
        var result = session.CompleteFastForwardOperation("paid-request", value => value == "paid-request:profile");
        Require(result != null && result.TotalCount == 14 && result.Rewards.Single().Count == 14
            && session.PendingFastForwardOperation == null
            && session.Save.RandomRewards.Single().NextRefreshUnixSeconds == 1700000998,
            "charged request completes with frozen numeric inputs and original two-second refresh rewind");
        before = JsonUtility.ToJson(session.Save);
        Require(session.CompleteFastForwardOperation("paid-request", _ => true) == null
            && session.PrepareFastForwardOperation(1, new[] { 1 }, 2, false,
                new[] { new HomeCostEntry { itemId = 120, count = 1 } }, null, false, "paid-request") == null
            && JsonUtility.ToJson(session.Save) == before, "completed request cannot be charged or granted twice");
        Require(session.PrepareFastForwardOperation(1, new[] { 1 }, 2, false,
            new[] { new HomeCostEntry { itemId = 120, count = 1 } }, null, false, "unpaid-cancel") != null
            && session.CancelUnpaidFastForwardOperation("unpaid-cancel", _ => false)
            && session.PendingFastForwardOperation == null, "only unpaid paid intent can be cancelled without a grant");
        Require(session.PrepareFastForwardOperation(1, new[] { 1 }, 2, true, null, null, false, "free-request") != null
            && session.GetTodayFreeFastForwardCount(1) == 1, "free use is reserved with its durable intent");
        session = OfflineHomeIncomeSession.Open(path, new[] { definition }, props, flags, 1700000000);
        Require(!session.CancelUnpaidFastForwardOperation("free-request", _ => false)
            && session.GetTodayFreeFastForwardCount(1) == 1
            && session.CompleteFastForwardOperation("free-request", _ => true) != null
            && session.GetTodayFreeFastForwardCount(1) == 1, "reopen and completion never reserve a second free use");
        var cart = new OfflineHomeIncomeDefinition {
            HangUpId = 2, OutputItemId = 51, Unlocked = true,
            Rule = new HangUpIncomeRule { HourlyOutput = 3, LevelNum = 1, MaxStoreHours = 0 }
        };
        session = OfflineHomeIncomeSession.Open(path, new[] { definition, cart }, props, flags, 1700000000);
        Require(session.Mutate("full-cart", save => save.Inventory.Add(new OfflineHomeItemStack {
            ItemId = 51, Count = long.MaxValue, Serial = ++save.GrantSerial })), "seed overflowing cart ledger");
        before = JsonUtility.ToJson(session.Save);
        Require(session.ApplyFastForwardBatchRewards(new[] { 1, 2 }, 2, "partial-overflow") == null
            && JsonUtility.ToJson(session.Save) == before, "one overflowing reward rejects the entire batch including earlier XP");
        Require(session.PrepareFastForwardOperation(1, new[] { 1, 2 }, 2, false,
            new[] { new HomeCostEntry { itemId = 120, count = 1 } }, null, false, "charged-overflow") != null,
            "accepted intent can remain pending when its later ledger transfer is temporarily blocked");
        before = JsonUtility.ToJson(session.Save);
        Require(session.CompleteFastForwardOperation("charged-overflow", _ => true) == null
            && JsonUtility.ToJson(session.Save) == before, "charged overflowing batch retains intent and all prior balances");
        string zeroPath = System.IO.Path.Combine(root, "fast-extra-only.json");
        definition.Rule.LevelNum = 0;
        session = OfflineHomeIncomeSession.Open(zeroPath, new[] { definition }, props, flags, 1700000000);
        Require(session.Mutate("fractional:random-timer", save => save.RandomRewards.Add(
            new OfflineHomeRandomRewardSlot { Slot = 0, NextRefreshUnixSeconds = 1700001000 })),
            "seed fractional callback refresh timer");
        Require(session.PrepareFastForwardOperation(1, new[] { 1 }, 2.5, false,
            new[] { new HomeCostEntry { itemId = 120, count = 1 } },
            new[] { new OfflineHomeFastForwardAutoIncrease { HangUpId = 101, ItemId = 57 } }, false, "extras-only", true) != null,
            "extra-handler request can persist even if there is no ordinary output");
        result = session.CompleteFastForwardOperation("extras-only", _ => true);
        Require(result != null && result.TotalCount == 0 && result.Rewards.Count == 0
            && session.PendingFastForwardOperation == null
            && session.Save.RandomRewards.Single().NextRefreshUnixSeconds == 1700000997.5,
            "extras-only receipt clears intent and preserves the fractional original callback rewind without a fabricated normal reward");
    }

    private void CheckFastBatchReceipts(ISet<int> flags)
    {
        var bed = new OfflineHomeIncomeDefinition {
            HangUpId = 1, OutputItemId = 50, FastHangUpId = 1, Unlocked = true,
            FreeFastForwardLimit = 1, FreeFastForwardHours = 2,
            Rule = new HangUpIncomeRule { HourlyOutput = 5, LevelNum = 1, MaxStoreHours = 0 }
        };
        var cart = new OfflineHomeIncomeDefinition {
            HangUpId = 2, OutputItemId = 51, OutputItemIds = new[] { 51, 52 }, OutputWeights = new[] { 1000, 1000 },
            FastHangUpId = -1, Unlocked = true,
            Rule = new HangUpIncomeRule { HourlyOutput = 3, LevelNum = 1, MaxStoreHours = 0 }
        };
        var locked = new OfflineHomeIncomeDefinition {
            HangUpId = 3, OutputItemId = 53, FastHangUpId = -1, Unlocked = false,
            Rule = new HangUpIncomeRule { HourlyOutput = 100, LevelNum = 1, MaxStoreHours = 36 }
        };
        string path = System.IO.Path.Combine(root, "fast-receipts.json");
        var entries = new[] { bed, cart, locked };
        var properties = new Dictionary<int, long>();
        var session = OfflineHomeIncomeSession.Open(path, entries, properties, flags, 1700000000);
        Require(session.Mutate("prior", save => {
            save.GrantSerial = 3;
            save.Inventory.Add(new OfflineHomeItemStack { ItemId = 50, Count = 90, Serial = 1, TransferStarted = true });
            save.Inventory.Add(new OfflineHomeItemStack { ItemId = 51, Count = 70, Serial = 2 });
            save.Inventory.Add(new OfflineHomeItemStack { ItemId = 52, Count = 80, Serial = 3 });
        }), "seed sealed XP transfer and older unrelated random output stacks");
        string before = JsonUtility.ToJson(session.Save);
        Require(session.ApplyFreeFastForwardGroupRewards(1, new[] { 1, 2 }, 3, "wrong-hours") == null
            && JsonUtility.ToJson(session.Save) == before, "rejected free duration returns no receipt or changed state");
        var result = session.ApplyFreeFastForwardGroupRewards(1, new[] { 1, 2, 2, 3 }, 2, "free-batch");
        Require(result != null && result.TotalCount == 16 && result.Rewards.Count == 2,
            "free group uses its configured members once, including carts without a fast ID, and excludes locked output");
        var xp = result.Rewards.Single(value => value.HangUpId == 1);
        var selected = result.Rewards.Single(value => value.HangUpId == 2);
        Require(xp.ItemId == 50 && xp.Count == 10 && xp.LedgerSerial == 4 && selected.Count == 6
            && new[] { 51, 52 }.Contains(selected.ItemId) && selected.Receipt == "free-batch"
            && session.Save.Inventory.Single(value => value.Serial == 1).Count == 90
            && session.Save.Inventory.Single(value => value.Serial == selected.LedgerSerial).Count
                == (selected.ItemId == 51 ? 70 : 80) + 6,
            "batch receipts contain exact new random outputs, excluding old amounts and sealed transfers, without a storage clamp");
        before = JsonUtility.ToJson(session.Save);
        Require(session.ApplyFreeFastForwardGroupRewards(1, new[] { 1, 2 }, 2, "free-batch") == null
            && session.ApplyFreeFastForwardGroupRewards(1, new[] { 1, 2 }, 2, "over-limit") == null
            && JsonUtility.ToJson(session.Save) == before, "duplicate and exhausted free requests produce no additional receipt");
        var paid = session.ApplyFastForwardBatchRewards(new[] { 1, 2, 2, 3 }, 2, "paid-income");
        Require(paid != null && paid.TotalCount == 16 && paid.Rewards.Count == 2
            && paid.Rewards.All(value => value.Count == (value.HangUpId == 1 ? 10 : 6)),
            "paid income batch reports only each selected output delta, independently of the prior free batch");
        session = OfflineHomeIncomeSession.Open(path, entries, properties, flags, 1700000000);
        before = JsonUtility.ToJson(session.Save);
        Require(session.ApplyFastForwardBatchRewards(new[] { 1, 2 }, 2, "paid-income") == null
            && JsonUtility.ToJson(session.Save) == before && session.Save.FastForwardUsage.Single().FreeUses == 1,
            "disk reopen retains batch idempotency and the single free use");
        Require(session.Mutate("full-ledger", save => {
            foreach (var stack in save.Inventory.Where(value => value.ItemId == 51 || value.ItemId == 52))
                stack.Count = long.MaxValue;
        }), "seed full pending stacks for either possible cart output");
        before = JsonUtility.ToJson(session.Save);
        Require(session.ApplyFastForwardBatchRewards(new[] { 2 }, 2, "rejected-random") == null
            && JsonUtility.ToJson(session.Save) == before,
            "rejected random batch retains its old ledger, random state and receipt list");
    }

    private void CheckCollectionReceipts(ISet<int> flags)
    {
        string path = System.IO.Path.Combine(root, "collection-receipts.json");
        var definition = new OfflineHomeIncomeDefinition {
            HangUpId = 1, OutputItemId = 51, OutputItemIds = new[] { 51, 52 },
            OutputWeights = new[] { 1000, 1000 }, Unlocked = true,
            Rule = new HangUpIncomeRule { HourlyOutput = 5, LevelNum = 1, MaxStoreHours = 36 }
        };
        var entries = new[] { definition };
        var properties = new Dictionary<int, long>();
        var session = OfflineHomeIncomeSession.Open(path, entries, properties, flags, 1700000000);
        Require(session.Mutate("seed", save => {
            save.Income.Add(new OfflineHomeIncomeEntry { HangUpId = 1, StoredSeconds = 3600 });
            save.GrantSerial = 2;
            save.Inventory.Add(new OfflineHomeItemStack { ItemId = 51, Count = 99, Serial = 1 });
            save.Inventory.Add(new OfflineHomeItemStack { ItemId = 52, Count = 99, Serial = 2 });
        }), "persist prior unrelated rewards in both possible output stacks");
        var reward = session.CollectReward(1, "collect");
        Require(reward != null && reward.HangUpId == 1 && reward.Count == 5 && reward.Receipt == "collect"
            && session.Save.Inventory.Single(row => row.ItemId == reward.ItemId).Count == 104
            && session.Save.Inventory.Single(row => row.Serial == reward.LedgerSerial).ItemId == reward.ItemId,
            "receipt uses the actual randomly selected output and only this claim's delta");
        string committed = JsonUtility.ToJson(session.Save);
        Require(session.CollectReward(1, "collect") == null && session.CollectReward(1, "empty") == null
            && JsonUtility.ToJson(session.Save) == committed, "duplicate and empty claims produce no presentation receipt or ledger mutation");
        session = OfflineHomeIncomeSession.Open(path, entries, properties, flags, 1700000000);
        Require(session.CollectReward(1, "collect") == null && JsonUtility.ToJson(session.Save) == committed,
            "disk reopen does not grant the committed receipt again");
        Require(session.Mutate("overflow", save => {
            save.Income.Single().StoredSeconds = 3600;
            foreach (var item in save.Inventory) item.Count = long.MaxValue;
        }), "seed isolated full ledger");
        string before = JsonUtility.ToJson(session.Save);
        Require(session.CollectReward(1, "retry") == null && JsonUtility.ToJson(session.Save) == before,
            "failed output grant preserves income, random state, serials and receipt");
        Require(session.Mutate("space", save => { foreach (var item in save.Inventory) item.Count = 1; }), "restore isolated ledger space");
        Require(session.CollectReward(1, "retry")?.Count == 5, "failed receipt remains retryable after storage succeeds");
        CheckPreciseCollectionClock(flags);
    }

    private void CheckPreciseCollectionClock(ISet<int> flags)
    {
        const long epoch = 1700001000;
        string path = System.IO.Path.Combine(root, "precise-collection-clock.json");
        var definitions = new[] { new OfflineHomeIncomeDefinition {
            HangUpId = 1, OutputItemId = 50, Unlocked = true,
            Rule = new HangUpIncomeRule { HourlyOutput = 3600, LevelNum = 1, MaxStoreHours = 36 }
        } };
        var properties = new Dictionary<int, long>();
        var session = OfflineHomeIncomeSession.Open(path, definitions, properties, flags, epoch);
        Require(session.Mutate("half-second", save => save.Income.Add(
            new OfflineHomeIncomeEntry { HangUpId = 1, StoredSeconds = .5 })), "seed subsecond production");
        var at = DateTimeOffset.FromUnixTimeSeconds(epoch).AddTicks(7500000);
        Require(session.PendingIncomeSeconds(at) == .75, "read-only bed preview includes pending subsecond time");
        Require(session.CollectReward(1, "precise", at)?.Count == 1
            && session.Save.LastIncomeSubsecond == .75 && session.Save.Income.Single().StoredSeconds == .25,
            "precise claim includes pending production and persists its original remainder atomically");
        Require(session.AdvanceTo(epoch + 1) && session.Save.LastIncomeSubsecond == 0
            && session.Save.Income.Single().StoredSeconds == .5,
            "next integral checkpoint does not accumulate the claimed fraction twice");
        session = OfflineHomeIncomeSession.Open(path, definitions, properties, flags, epoch + 1);
        Require(session.Save.Income.Single().StoredSeconds == .5, "disk reopen retains exact income time");
        at = DateTimeOffset.FromUnixTimeSeconds(epoch + 1).AddTicks(5000000);
        Require(session.CollectReward(1, "next-precise", at)?.Count == 1
            && session.Save.Income.Single().StoredSeconds == 0, "next fractional interval claims once");
        string before = JsonUtility.ToJson(session.Save);
        Require(session.CollectReward(1, "rollback", at.AddTicks(-2500000)) == null
            && JsonUtility.ToJson(session.Save) == before, "backward local time cannot rewind the income checkpoint");
        Require(session.Mutate("fill-ledger", save => {
            save.Income.Single().StoredSeconds = .5;
            save.Inventory.Single().Count = long.MaxValue;
        }), "seed rejected precise grant");
        before = JsonUtility.ToJson(session.Save);
        Require(session.CollectReward(1, "failed-precise", at.AddTicks(7500000)) == null
            && JsonUtility.ToJson(session.Save) == before, "failed precise grant preserves time, remainder and receipt");
    }

    private IEnumerator CheckExperienceLedgerRecovery()
    {
        const long recoveredSerial = 1000001, failedSerial = 1000003;
        long experience = home.Profile.Save.Player.Experience;
        string recoveredReceipt = "home:grant:" + slot + ":" + recoveredSerial;
        Require(home.Session.Mutate("seed-unacknowledged", save => {
            save.GrantSerial = recoveredSerial;
            save.Inventory.Add(new OfflineHomeItemStack { ItemId = 50, Count = 11, Serial = recoveredSerial });
        }), "persist isolated unacknowledged experience");
        var prepared = home.Session.PrepareInventoryTransfer(recoveredSerial);
        Require(prepared != null && prepared.TransferStarted && prepared.Count == 11,
            "persist transfer amount before awarding the character");
        prepared.Count = 99;
        Require(home.Session.PrepareInventoryTransfer(recoveredSerial).Count == 11,
            "transfer snapshots cannot mutate the durable receipt amount");
        Require(home.Profile.ApplyCharacterMutation(player => {
            player.Experience += 11;
        }, recoveredReceipt).IsApplied, "persist award before interrupted ledger acknowledgement");
        Require(home.Session.Mutate("seed-next-claim", save => {
            var income = save.Income.Single(value => value.HangUpId == 1);
            income.StoredSeconds = 3600; income.SettledCount = 0;
        }), "persist income earned while the earlier grant awaits acknowledgement");
        var nextClaim = home.Session.CollectReward(1, "claim-before-ack");
        Require(nextClaim != null && nextClaim.Count > 0 && nextClaim.LedgerSerial != recoveredSerial
            && home.Session.Save.Inventory.Single(row => row.Serial == recoveredSerial).Count == 11
            && home.Session.Save.Inventory.Single(row => row.Serial == nextClaim.LedgerSerial).Count == nextClaim.Count,
            "new claim cannot merge into an already transferred receipt");
        home.Close(); CloseCapture(); yield return null;
        Open(); yield return new WaitForSeconds(1);
        long recoveredExperience = experience + 11 + nextClaim.Count;
        Require(home.Profile.Save.Player.Experience == recoveredExperience && home.Session.Save.Inventory.Count == 0,
            "disk reopen acknowledges the old receipt and grants the newer claim exactly once");
        home.DrainHomeInventory();
        Require(home.Profile.Save.Player.Experience == recoveredExperience,
            "repeating recovery grants neither receipt again");
        Require(home.Session.Mutate("seed-rejected-award", save => {
            save.GrantSerial = failedSerial;
            save.Inventory.Add(new OfflineHomeItemStack { ItemId = 50, Count = long.MaxValue, Serial = failedSerial });
        }), "seed isolated experience overflow rejection");
        home.DrainHomeInventory();
        Require(home.Session.Save.Inventory.Single(row => row.Serial == failedSerial).Count == long.MaxValue
            && home.Session.Save.Inventory.Single(row => row.Serial == failedSerial).TransferStarted
            && home.Profile.Save.Player.Experience == recoveredExperience,
            "rejected character mutation retains the durable owed experience");
        home.Close(); CloseCapture(); yield return null;
        Open(); yield return new WaitForSeconds(1);
        Require(home.Session.Save.Inventory.Single(row => row.Serial == failedSerial).Count == long.MaxValue
            && home.Profile.Save.Player.Experience == recoveredExperience,
            "disk reopen preserves failed experience for retry and completed receipt stays idempotent");
    }

    private void CheckAccelerationTransactions(ISet<int> flags)
    {
        const double hours = 231.16786752053045;
        var definition = new OfflineHomeIncomeDefinition {
            HangUpId = 1, OutputItemId = 50, FastHangUpId = 1, Unlocked = true,
            FreeFastForwardLimit = 1, FreeFastForwardHours = hours,
            Rule = new HangUpIncomeRule { HourlyOutput = 2, LevelNum = 6.503173351287842,
                MaxStoreHours = 36, EffectPercentPropertyId = 1001, EffectFixedPropertyId = 1000 }
        };
        var entries = new[] { definition };
        var properties = new Dictionary<int, long> { { 1000, 22 }, { 1001, 6102 } };
        var session = OfflineHomeIncomeSession.Open(System.IO.Path.Combine(root, "formula-batch.json"),
            entries, properties, flags, 1700000000);
        Require(session.Mutate("crystal", save => {
            save.GrantSerial = 1;
            save.Inventory.Add(new OfflineHomeItemStack { ItemId = 120, Count = 1, Serial = 1 });
        }), "seed isolated acceleration cost ledger");
        Require(session.ApplyFastForwardItems(1, 120, 1, 100, hours, "paid") == 9926
            && session.ApplyFastForwardItems(1, 120, 1, 100, hours, "paid") == 0,
            "paid acceleration preserves original integer boundary and receipt idempotency");
        string before = JsonUtility.ToJson(session.Save);
        Require(session.ApplyFastForwardItems(1, 120, 1, 100, hours, "no-cost") == 0
            && JsonUtility.ToJson(session.Save) == before, "insufficient cost leaves ledger unchanged");
        Require(session.ApplyFastForwardBatch(new[] { 1, 1 }, hours, "batch") == 9926,
            "batch preserves original integer boundary and does not repeat a hang-up ID");
        Require(session.ApplyFreeFastForwardGroup(1, new[] { 1, 1 }, hours, "free") == 9926
            && session.ApplyFreeFastForwardGroup(1, new[] { 1 }, hours, "free-again") == 0,
            "free group preserves original integer boundary and daily usage");
        session = OfflineHomeIncomeSession.Open(System.IO.Path.Combine(root, "formula-batch.json"),
            entries, properties, flags, 1700000000);
        Require(session.Save.Inventory.Single(value => value.ItemId == 50).Count == 29778
            && !session.Save.Inventory.Any(value => value.ItemId == 120)
            && session.Save.FastForwardUsage.Single().FreeUses == 1,
            "all three acceleration paths persist exact rewards and the consumed cost");
    }

    private void CheckFreeFastForwardCalendar(ISet<int> flags)
    {
        long LocalTime(int day, int hour, int minute = 0, int second = 0)
            => new DateTimeOffset(new DateTime(2026, 11, day, hour, minute, second, DateTimeKind.Unspecified)).ToUnixTimeSeconds();
        var definition = new OfflineHomeIncomeDefinition {
            HangUpId = 1, OutputItemId = 50, FastHangUpId = 1, Unlocked = true,
            FreeFastForwardLimit = 1, FreeFastForwardHours = 2,
            Rule = new HangUpIncomeRule { HourlyOutput = 5, LevelNum = 1, MaxStoreHours = 36 }
        };
        var entries = new[] { definition };
        var properties = new Dictionary<int, long>();
        string path = System.IO.Path.Combine(root, "free-fast-calendar.json");
        var session = OfflineHomeIncomeSession.Open(path, entries, properties, flags, LocalTime(13, 23, 59, 59));
        Require(session.CanFreeFastForward(1) && !session.CanFreeFastForward(0)
            && session.ApplyFreeFastForwardGroup(1, new[] { 1 }, 2, "before-midnight") == 10,
            "free availability and actual award share the original configured count");
        session.AdvanceTo(LocalTime(14, 0));
        Require(!session.CanFreeFastForward(1) && session.GetTodayFreeFastForwardCount(1) == 1
            && session.ApplyFreeFastForwardGroup(1, new[] { 1 }, 2, "midnight") == 0,
            "midnight does not reset the confirmed local 06:00 logical day");
        session.AdvanceTo(LocalTime(14, 5, 59, 59));
        Require(!session.CanFreeFastForward(1), "05:59:59 retains the prior day's usage");
        session.AdvanceTo(LocalTime(14, 6));
        string beforeRead = JsonUtility.ToJson(session.Save);
        Require(session.CanFreeFastForward(1) && session.GetTodayFreeFastForwardCount(1) == 0
            && beforeRead == JsonUtility.ToJson(session.Save), "06:00 reads expired usage without clearing or writing it");
        Require(session.ApplyFreeFastForwardGroup(1, new[] { 1 }, 2, "at-six") == 10
            && session.GetTodayFreeFastForwardCount(1) == 1, "first post-reset use replaces the logical-day counter");
        properties[1101] = 1;
        session.Reconfigure(entries, properties, flags, Array.Empty<OfflineHomeRandomRewardDefinition>());
        Require(session.CanFreeFastForward(1)
            && session.ApplyFreeFastForwardGroup(1, new[] { 1 }, 2, "property-extra") == 10
            && session.GetTodayFreeFastForwardCount(1) == 2 && !session.CanFreeFastForward(1),
            "original non-battle FreeFastHangUpAmount adds to the configured daily count exactly once");
        definition.FreeFastForwardLimited = true;
        session.AdvanceTo(LocalTime(15, 6));
        beforeRead = JsonUtility.ToJson(session.Save);
        Require(!session.CanFreeFastForward(1)
            && session.ApplyFreeFastForwardGroup(1, new[] { 1 }, 2, "limited") == 0
            && beforeRead == JsonUtility.ToJson(session.Save), "function limit rejects free acceleration without consuming usage");
        definition.FreeFastForwardLimited = false; properties.Clear();
        Require(session.ApplyFreeFastForwardGroup(1, new[] { 1 }, 2, "next-day") == 10,
            "removing the actual limit allows the new day's first use");
        session = OfflineHomeIncomeSession.Open(path, entries, properties, flags, LocalTime(15, 8));
        Require(!session.CanFreeFastForward(1) && session.GetTodayFreeFastForwardCount(1) == 1
            && session.ApplyFreeFastForwardGroup(1, new[] { 1 }, 2, "reopened") == 0,
            "08:00 and disk reopen do not refresh the 06:00 use a second time");
        string legacyPath = System.IO.Path.Combine(root, "free-fast-calendar-legacy.json");
        System.IO.File.WriteAllText(legacyPath, JsonUtility.ToJson(new OfflineHomeSave {
            LastLocalUnixSeconds = LocalTime(14, 5, 59, 59),
            FastForwardUsage = new List<OfflineHomeFastForwardUsage> {
                new OfflineHomeFastForwardUsage { FastHangUpId = 1, LocalDate = "20261114", FreeUses = 1 }
            }
        }));
        session = OfflineHomeIncomeSession.Open(legacyPath, entries, properties, flags, LocalTime(14, 5, 59, 59));
        Require(!session.CanFreeFastForward(1) && session.Save.FastForwardUsage.Single().LocalDate == "20261113",
            "old midnight-based morning usage is retained in the preceding logical day");
        session.AdvanceTo(LocalTime(14, 6));
        Require(session.CanFreeFastForward(1) && session.ApplyFreeFastForwardGroup(1, new[] { 1 }, 2, "legacy-six") == 10,
            "migrated morning usage refreshes once at the confirmed 06:00 boundary");
    }
}
