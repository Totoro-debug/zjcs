using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
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
        rule.IsCart = false; props.Clear();
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
        CheckCollectionReceipts(flags);

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
        Require(home.TryCollect(1) == expected && expected > 0, "production collection uses migrated arithmetic");
        profile.Reload();
        Require(profile.Save.Player.Experience == experience + expected && home.Session.Save.Inventory.Count == 0,
            "experience reaches real character and durable ledger is acknowledged");
        double remainder = home.Session.Save.Income.Single(value => value.HangUpId == 1).StoredSeconds;
        Require(remainder > 0 && remainder < 3600 && home.TryCollect(1) == 0, "fractional time remains without a duplicate grant");
        long freeExpected = (long)OfflineHangUpIncome.RewardCount(home.Session.Definitions.Single(value => value.HangUpId == 1).Rule,
            home.PlayerProperties(), flags, 2);
        Require(home.TryFastForward(1) == freeExpected && home.TryFastForward(1) == 0,
            "actual free acceleration uses original arithmetic and saved daily limit");
        profile.Reload(); long awardedExperience = profile.Save.Player.Experience;
        Require(awardedExperience == experience + expected + freeExpected, "free acceleration grants experience once");
        long checkpoint = home.Session.Save.LastLocalUnixSeconds;
        double remainingSeconds = home.Session.Save.Income.Single(value => value.HangUpId == 1).StoredSeconds;
        Capture("home-income-collected.png");
        home.Close(); CloseCapture(); yield return null;
        Open(); yield return new WaitForSeconds(1); profile.Reload();
        double elapsed = home.Session.Save.LastLocalUnixSeconds - checkpoint;
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
        yield return CheckExperienceLedgerRecovery();
        Debug.Log("HOME_INCOME_FORMULA_PASSED: selected Float flags, original single precision and arithmetic boundary, actual native repair pointer, real lost goddess profile, production collection/experience/free acceleration, committed reward receipts, rejected experience grants retained, acknowledgement recovery, remainder, idempotency and disk reopen. Collection and acceleration business calls are direct; original bed buttons and complete HUD remain pending.");
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
    }

    private IEnumerator CheckExperienceLedgerRecovery()
    {
        const long recoveredSerial = 1000001, failedSerial = 1000002;
        long experience = home.Profile.Save.Player.Experience;
        string recoveredReceipt = "home:grant:" + slot + ":" + recoveredSerial;
        Require(home.Profile.ApplyCharacterMutation(player => {
            player.Experience += 11;
        }, recoveredReceipt).IsApplied, "persist award before interrupted ledger acknowledgement");
        Require(home.Session.Mutate("seed-unacknowledged", save => {
            save.GrantSerial = recoveredSerial;
            save.Inventory.Add(new OfflineHomeItemStack { ItemId = 50, Count = 11, Serial = recoveredSerial });
        }), "persist isolated unacknowledged experience");
        home.DrainHomeInventory();
        Require(home.Profile.Save.Player.Experience == experience + 11 && home.Session.Save.Inventory.Count == 0,
            "replayed character receipt acknowledges the ledger without adding experience again");
        Require(home.Session.Mutate("seed-rejected-award", save => {
            save.GrantSerial = failedSerial;
            save.Inventory.Add(new OfflineHomeItemStack { ItemId = 50, Count = long.MaxValue, Serial = failedSerial });
        }), "seed isolated experience overflow rejection");
        home.DrainHomeInventory();
        Require(home.Session.Save.Inventory.Single(row => row.Serial == failedSerial).Count == long.MaxValue
            && home.Profile.Save.Player.Experience == experience + 11,
            "rejected character mutation retains the durable owed experience");
        home.Close(); CloseCapture(); yield return null;
        Open(); yield return new WaitForSeconds(1);
        Require(home.Session.Save.Inventory.Single(row => row.Serial == failedSerial).Count == long.MaxValue
            && home.Profile.Save.Player.Experience == experience + 11,
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
}
