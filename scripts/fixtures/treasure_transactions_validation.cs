using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.TreasureCultivation;
using O = TreasureOracle;

public static partial class TreasureCultivationValidation
{
    private sealed class TransitionTrace : ITreasureTransitionNotifications
    {
        internal readonly List<string> Events = new List<string>();
        public void Active(TreasureItem item) => Events.Add("active:" + item.State.Active);
        public void Refresh(TreasureItem item, bool isNew) => Events.Add("refresh:" + isNew + ":" + item.State.Star);
        public void UpgradeStar(TreasureItem item, int previous, int current) => Events.Add("star:" + previous + ":" + current);
    }

    private static void CheckTransitions()
    {
        foreach (var row in catalog.Treasures)
        foreach (int star in Enumerable.Range(0, row.StarMax + 1))
        {
            var local = new TreasureItem { Id = row.ClassId, Count = 1, Info = service.GetItemInfo(row.ClassId),
                State = new TreasureState { Star = star, HitCount = 37 } };
            var oracle = new O.PlayerItemDataWrap { Id = row.ClassId, Count = 1,
                Info = O.Parsers.Instance.ItemInfoParser.Get(row.ClassId),
                State = new O.PlayerItemParamTreasureWrap { Star = star, HitCount = 37 } };
            var trace = new TransitionTrace();
            var expected = new List<string>();
            var originalState = new O.OriginalTreasureStateTransitions();
            originalState.OnTreasureActive += item => { Check(ReferenceEquals(item, oracle), "original active object identity"); expected.Add("active:" + item.State.Active); };
            originalState.OnTreasureRefresh += (item, isNew) => expected.Add("refresh:" + isNew + ":" + item.State.Star);
            originalState.OnTreasureUpgradeStar += (item, old, current) => expected.Add("star:" + old + ":" + current);
            var state = new OfficialTreasureStateTransitions(trace);
            state.TreasureActivate(local); originalState.TreasureActivate(oracle);
            state.UpgradeTreasureStar(local); originalState.UpgradeTreasureStar(oracle);
            state.SetTreasureStar(local, star); originalState.SetTreasureStar(oracle, star);
            state.UpgradeTreasureStarWithoutNortify(local, 2); originalState.UpgradeTreasureStarWithoutNortify(oracle, 2);
            state.TreasureActivateWithoutNortify(local); originalState.TreasureActivateWithoutNortify(oracle);
            state.FireOnTreasureRefresh(local, true); originalState.FireOnTreasureRefresh(oracle, true);
            state.FireOnTreasureActive(local); originalState.FireOnTreasureActive(oracle);
            Check(trace.Events.SequenceEqual(expected), "all original transition event order and intermediate values");
            Check(local.State.Active == oracle.State.Active && local.State.Star == oracle.State.Star
                && local.State.HitCount == oracle.State.HitCount && local.Count == oracle.Count,
                "original notified and silent transitions preserve hit count and inventory");
        }
    }

    private static void SyncTransactionOracle()
    {
        original.Items.Clear();
        foreach (var stack in profile.Save.Player.Inventory)
        {
            var state = profile.Save.Player.CharacterProperties.Treasures.FirstOrDefault(row => row.ClassId == stack.ItemId);
            original.Items.Add(stack.ItemId, new O.PlayerItemDataWrap { Id = stack.ItemId, Count = stack.Count,
                Info = O.Parsers.Instance.ItemInfoParser.Get(stack.ItemId), State = state == null ? null :
                    new O.PlayerItemParamTreasureWrap { Active = state.Active, Star = state.Star, HitCount = state.HitCount } });
        }
    }

    private static void PersistTransactionSeed(int id, int star, bool active, bool owned, long budget, bool parameter = true,
        bool commonOnly = false)
    {
        profile.Reload();
        Check(profile.ApplyCharacterMutation(player => {
            var stock = sharedMaterialIds.Concat(new[] { catalog.GetTreasure(id).TreasurePieceId }).Distinct()
                .ToDictionary(material => material, material => budget);
            if (commonOnly) stock[catalog.GetTreasure(id).TreasurePieceId] = 0;
            if (owned) stock[id] = 1;
            player.Inventory = stock.Select(row => new OfflineItemStack { ItemId = row.Key, Count = row.Value }).ToList();
            player.CharacterProperties.Treasures.Clear();
            if (parameter) player.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure {
                ClassId = id, Star = star, Active = active, HitCount = 37, Level = 7, BaseLevel = 5 });
        }).IsApplied, "persist isolated transaction input");
        SyncTransactionOracle();
    }

    private static void CheckTransactions(ProfileSlotCatalog slots, string slot)
    {
        CheckTransitions();
        string saveFile = Path.Combine(slots.GetSlotDirectory(slot), "city-save.json");
        const long now = 1700000000;
        var live = new OfflineTreasureService(profile, catalog, ReadItem);
        var events = new List<string>();
        TreasureItem activeItem = null, starItem = null;
        live.OnTreasureActive += item => {
            Check(profile.Save.Player.CharacterProperties.Treasures.Single(row => row.ClassId == item.Id).Active,
                "activation observer reads committed state");
            Check(File.ReadAllText(saveFile).Contains("\"Active\": true"), "activation observer sees durable file");
            activeItem = item; events.Add("active");
        };
        live.OnTreasureUpgradeStar += (item, old, current) => {
            Check(current == old + 1 && profile.Save.Player.CharacterProperties.Treasures.Single(row => row.ClassId == item.Id).Star == current,
                "star observer reads committed one-step transition");
            starItem = item; events.Add("star");
        };
        live.OnTreasureRefresh += (item, isNew) => {
            Check(!isNew && (ReferenceEquals(item, activeItem) || ReferenceEquals(item, starItem)),
                "paired refresh preserves original item identity and isNew flag");
            events.Add("refresh");
        };

        // The previous base-material fixture remains persisted. Exercise its actual upgrade.
        profile.Reload(); SyncTransactionOracle();
        int autoId = profile.Save.Player.CharacterProperties.Treasures.Single().ClassId;
        Check(O.OriginalTreasureHelper.GetTreasureNextLevelUpgradeMaterial(autoId, original, true, false, out var autoNeed) == 0,
            "original automatic transaction quote");
        var autoPayment = O.OriginalTreasureHelper.GetTreasureUpgradeMaterial(new Dictionary<int, long>(autoNeed), true, original);
        var beforeAuto = profile.Save.Player.Inventory.ToDictionary(row => row.ItemId, row => row.Count);
        int oldAutoStar = profile.Save.Player.CharacterProperties.Treasures.Single().Star;
        var appliedAuto = live.UpgradeTreasure(autoId, false, true, "treasure:actual-base-upgrade", now);
        Check(appliedAuto.IsApplied && CostText(appliedAuto.Costs.ToDictionary(row => row.Key, row => row.Value)) == CostText(autoPayment),
            "actual automatic star-up debits original recursive cost");
        CheckTransactionDebits(beforeAuto, autoPayment, autoId, 0);
        Check(events.SequenceEqual(new[] { "star", "refresh" }), "automatic star-up publishes original success order");
        Check(profile.Save.Player.CharacterProperties.Treasures.Single().Star == oldAutoStar + 1, "base-only star-up changes saved star");
        events.Clear();
        var restored = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var restoredService = new OfflineTreasureService(restored, catalog, ReadItem);
        Check(restored.Save.Player.CharacterProperties.Treasures.Single().Star == oldAutoStar + 1,
            "cold-open restores automatic upgrade");
        Check(restoredService.UpgradeTreasure(autoId, false, true, "treasure:actual-base-upgrade", now).Status == ProgressionResultStatus.Unchanged,
            "receipt replay remains unchanged after cold-open");

        foreach (var info in catalog.Treasures.GroupBy(row => new { items[row.ClassId].Quality, row.StarMax,
            row.LimitUseCommonMaterial, row.InitialHitCount }).Select(group => group.First()))
        {
            int id = info.ClassId;
            PersistTransactionSeed(id, 0, false, false, 1000000000, false);
            var before = profile.Save.Player.Inventory.ToDictionary(row => row.ItemId, row => row.Count);
            var expectedMerge = O.Parsers.Instance.TreasureUpgradeInfoParser.GetMergeMaterials(id);
            var merge = live.MergeTreasure(id, "treasure:merge:" + id, now);
            Check(merge.IsApplied && merge.ErrorCode == 0, "fragment merge commits for each definition category");
            Check(CostText(merge.Costs.ToDictionary(row => row.Key, row => row.Value)) == CostText(expectedMerge), "merge pays original star-zero costs");
            CheckTransactionDebits(before, expectedMerge, id, 1);
            var merged = profile.Save.Player.CharacterProperties.Treasures.Single();
            Check(merged.Active && merged.Star == 0 && merged.HitCount == info.InitialHitCount
                && merged.SlotIndex == -1 && merged.Level == 1 && merged.BaseLevel == 1,
                "merge grants and activates actual item with official initial parameters");
            Check(events.SequenceEqual(new[] { "active", "refresh" }), "merge activates in original callback order"); events.Clear();
            string afterMerge = File.ReadAllText(saveFile);
            Check(live.MergeTreasure(id, "treasure:merge:" + id, now).Status == ProgressionResultStatus.Unchanged
                && File.ReadAllText(saveFile) == afterMerge && events.Count == 0, "merge receipt cannot debit or notify twice");

            PersistTransactionSeed(id, 0, false, true, 1000000000);
            before = profile.Save.Player.Inventory.ToDictionary(row => row.ItemId, row => row.Count);
            var activationCost = O.Parsers.Instance.TreasureUpgradeInfoParser.Get((O.Quality)items[id].Quality, 0).Materials;
            Check(live.ActivateTreasure(id, "treasure:active:" + id, now).IsApplied, "owned inactive item activates");
            CheckTransactionDebits(before, activationCost, id, 0);
            var activated = profile.Save.Player.CharacterProperties.Treasures.Single();
            Check(activated.Active && activated.Star == 0 && activated.HitCount == 37 && activated.Level == 7 && activated.BaseLevel == 5,
                "activation preserves unrelated existing parameters");
            Check(events.SequenceEqual(new[] { "active", "refresh" }), "activation publishes original order"); events.Clear();

            foreach (int star in Enumerable.Range(0, info.StarMax + 1))
            foreach (bool commonOnly in new[] { false, true })
            {
                PersistTransactionSeed(id, star, true, true, 1000000000, true, commonOnly);
                before = profile.Save.Player.Inventory.ToDictionary(row => row.ItemId, row => row.Count);
                int ec = O.OriginalTreasureHelper.GetTreasureNextLevelUpgradeMaterial(id, original, true, commonOnly, out var need);
                if (ec == 0) ec = O.OriginalTreasureHelper.CanTreasureUpgrade(id, need, original, false);
                byte[] beforeBytes = File.ReadAllBytes(saveFile);
                string receipt = "treasure:star:" + id + ":" + star + ":" + commonOnly;
                var result = live.UpgradeTreasure(id, commonOnly, false, receipt, now);
                Check(result.ErrorCode == ec && result.IsApplied == (ec == 0), "durable next-star outcome agrees with original ordinary/special/awakening checks");
                if (ec == 0)
                {
                    var payment = O.OriginalTreasureHelper.GetTreasureUpgradeMaterial(new Dictionary<int, long>(need), false, original);
                    CheckTransactionDebits(before, payment, id, 0);
                    var upgraded = profile.Save.Player.CharacterProperties.Treasures.Single();
                    Check(upgraded.Star == star + 1 && upgraded.Active && upgraded.HitCount == 37
                        && upgraded.Level == 7 && upgraded.BaseLevel == 5, "one-step upgrade preserves hit count and slot levels");
                    Check(events.SequenceEqual(new[] { "star", "refresh" }), "ordinary/special/awakening success event sequence"); events.Clear();
                    Check(live.UpgradeTreasure(id, commonOnly, false, receipt, now).Status == ProgressionResultStatus.Unchanged,
                        "star-up replay is not another level");
                }
                else Check(File.ReadAllBytes(saveFile).SequenceEqual(beforeBytes) && events.Count == 0
                    && !profile.Save.Player.AppliedInventoryEventIds.Contains(receipt), "failed official rule leaves no debit, receipt, state change or notification");
            }
        }
        int selectedId = catalog.Treasures.First().ClassId;
        PersistTransactionSeed(selectedId, 0, false, true, 0);
        var unchanged = File.ReadAllBytes(saveFile);
        Check(live.UpgradeTreasure(selectedId, false, false, "treasure:inactive", now).ErrorCode == 1223, "inactive upgrade rejection");
        Check(live.MergeTreasure(selectedId, "treasure:owned", now).ErrorCode == 1187, "owned merge rejection");
        Check(live.MergeTreasure(0, "treasure:unknown", now).ErrorCode == 1324, "unknown merge definition rejection");
        Check(live.ActivateTreasure(selectedId, "", now).Status == ProgressionResultStatus.InvalidState
            && live.ActivateTreasure(selectedId, "treasure:bad-time", -1).Status == ProgressionResultStatus.InvalidState,
            "invalid local transaction envelopes do not invent official error codes");
        Check(File.ReadAllBytes(saveFile).SequenceEqual(unchanged) && events.Count == 0, "all rejection envelopes preserve disk");
        var freeActivation = live.ActivateTreasure(selectedId, "treasure:free-activation", now);
        Check(freeActivation.IsApplied && freeActivation.Costs.Count == 0 && events.SequenceEqual(new[] { "active", "refresh" }),
            "official star-zero activation requires no additional materials"); events.Clear();
        unchanged = File.ReadAllBytes(saveFile);
        Check(live.UpgradeTreasure(selectedId, false, false, "treasure:insufficient", now).ErrorCode == 1188,
            "active star-up rejects insufficient fragments");
        Check(File.ReadAllBytes(saveFile).SequenceEqual(unchanged) && events.Count == 0, "insufficient star-up preserves disk");
        PersistTransactionSeed(selectedId, 0, false, true, 0, false);
        Check(live.ActivateTreasure(selectedId, "treasure:missing-parameter", now).ErrorCode == 1324,
            "missing owned-item parameters retain original activation rejection");

        PersistTransactionSeed(selectedId, 0, true, true, 1000000000);
        unchanged = File.ReadAllBytes(saveFile);
        string beforeFailed = JsonConvert.SerializeObject(profile.Save);
        // A directory blocks the writer's real temporary file, after all mutations have run on its clone.
        Directory.CreateDirectory(saveFile + ".tmp");
        try
        {
            var failed = live.UpgradeTreasure(selectedId, false, false, "treasure:write-failure", now);
            Check(failed.Status == ProgressionResultStatus.InvalidState && !failed.ErrorCode.HasValue,
                "actual filesystem failure is a local failure");
            Check(File.ReadAllBytes(saveFile).SequenceEqual(unchanged) && JsonConvert.SerializeObject(profile.Save) == beforeFailed
                && events.Count == 0, "failed durable write rolls back debits, parameters, receipt and notifications");
        }
        finally { Directory.Delete(saveFile + ".tmp"); }
        Check(live.UpgradeTreasure(selectedId, false, false, "treasure:write-failure", now).IsApplied,
            "failed receipt can be retried successfully after storage recovers"); events.Clear();
        var firstOwner = new OfflineTreasureService(profile, catalog, ReadItem);
        var secondOwner = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Check(secondOwner.ApplyCharacterMutation(p => p.Level += 1).IsApplied, "another owner commits after first adapter opens");
        int otherLevel = secondOwner.Save.Player.Level;
        Check(firstOwner.UpgradeTreasure(selectedId, false, false, "treasure:fresh-owner", now).IsApplied
            && profile.Save.Player.Level == otherLevel, "transaction reload preserves other owners' latest committed changes");
        profile.Reload();
        Check(profile.Save.Player.AppliedInventoryEventIds.Contains("treasure:fresh-owner"), "cold file retains successful transaction ledger");
    }

    private static void CheckTransactionDebits(Dictionary<int, long> before, Dictionary<int, long> payment, int treasureId, int grant)
    {
        var after = profile.Save.Player.Inventory.ToDictionary(row => row.ItemId, row => row.Count);
        foreach (int id in before.Keys.Concat(after.Keys).Concat(payment.Keys).Append(treasureId).Distinct())
        {
            long old = before.TryGetValue(id, out var prior) ? prior : 0;
            long actual = after.TryGetValue(id, out var current) ? current : 0;
            long cost = payment.TryGetValue(id, out var debit) ? Math.Max(0, debit) : 0;
            Check(actual == old - cost + (id == treasureId ? grant : 0), "durable inventory conservation " + id);
        }
    }
}
