using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

public static class OptionalChestValidation
{
    static readonly List<string> checks = new List<string>();
    public static void Run()
    {
        bool success = false;
        try { Validate(); success = true; }
        catch (Exception error) { Debug.LogException(error); checks.Add(error.ToString()); }
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_OPTIONAL_CHEST_OUTPUT"), "report.json"),
            JsonConvert.SerializeObject(new { success, checks }, Formatting.Indented));
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Validate()
    {
        string output = Environment.GetEnvironmentVariable("ZJCS_OPTIONAL_CHEST_OUTPUT");
        var slots = new ProfileSlotCatalog(Path.Combine(output, "profiles"));
        string slot = slots.CreateSlot();
        var profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var payload = Resources.Load<TextAsset>("PetGacha/chest-use-preview");
        Check(payload, "actual original chest data loads");
        var rules = new OfficialChestUsePreviewRules(payload.text);
        var service = new OfflineOptionalChestService(rules, profile);
        long now = 1700000000;
        var json = JObject.Parse(payload.text);
        var boxIds = ((JArray)json["tables"]["chest"]).Select(row => (int)row["ClassId"]).ToArray();
        foreach (int id in boxIds)
        {
            var chest = rules.OptionalChest(id);
            Check(chest.OptionCount == 1 && chest.Costs.Count == 1 && chest.Costs[id] == 1
                && chest.Options.Count == 3, "actual original optional request definition: " + id);
            foreach (int selected in chest.Options.Keys)
            {
                string receipt = "open:" + id + ":" + selected;
                Seed(profile, id, 2, receipt);
                long before = OpenCount(profile, id);
                var result = service.Open(id, 2, new[] { selected }, now, receipt);
                Check(result.IsApplied && profile.InventoryCount(id) == 0
                    && profile.InventoryCount(selected) == chest.Options[selected] * 2,
                    "one atomic cost and selected reward: " + receipt);
                Check(result.Rewards.Count == 1 && result.Rewards[selected] == chest.Options[selected] * 2
                    && chest.Options.Keys.Where(value => value != selected).All(value => profile.InventoryCount(value) == 0),
                    "unselected materials are not granted: " + receipt);
                var reloaded = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                Check(reloaded.InventoryCount(id) == 0 && reloaded.InventoryCount(selected) == chest.Options[selected] * 2
                    && OpenCount(reloaded, id) == before + 2 && reloaded.Save.Player.AppliedInventoryEventIds.Contains(receipt),
                    "actual disk reload preserves rewards, costs and opening record: " + receipt);
                var replay = service.Open(id, 2, new[] { selected }, now, receipt);
                Check(replay.Code == OptionalChestOpenCode.Unchanged && replay.Rewards.Count == 0
                    && profile.InventoryCount(selected) == chest.Options[selected] * 2,
                    "replay at zero remaining boxes has no side effects: " + receipt);
            }
        }
        int box = boxIds[0];
        int option = rules.OptionalChest(box).Options.Keys.First();
        Seed(profile, box, 2, "invalid");
        string path = slots.GetProgressPath(slot);
        byte[] beforeInvalid = File.ReadAllBytes(path);
        foreach (var result in new[] {
            service.Open(box, 0, new[] { option }, now, "zero"),
            service.Open(box, -1, new[] { option }, now, "negative"),
            service.Open(box, 1, Array.Empty<int>(), now, "empty"),
            service.Open(box, 1, new[] { option, option }, now, "duplicate"),
            service.Open(box, 1, new[] { -1 }, now, "unknown-option"),
            service.Open(-1, 1, new[] { option }, now, "unknown-box"),
            service.Open(box, 1, new[] { option }, -1, "bad-time"),
            service.Open(box, 1, new[] { option }, now, "") })
            Check(result.Code == OptionalChestOpenCode.InvalidRequest && File.ReadAllBytes(path).SequenceEqual(beforeInvalid),
                "invalid request leaves the real save bytes unchanged");
        Check(service.Open(box, 3, new[] { option }, now, "not-enough").Code == OptionalChestOpenCode.InsufficientItems
            && File.ReadAllBytes(path).SequenceEqual(beforeInvalid), "insufficient boxes do not commit any part of a transaction");
        Check(profile.ApplyInventoryTransaction("overflow-seed", save =>
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = option, Count = long.MaxValue })).IsApplied,
            "seed reward overflow boundary");
        byte[] overflowBytes = File.ReadAllBytes(path);
        var overflow = service.Open(box, 1, new[] { option }, now, "overflow");
        Check(overflow.Code == OptionalChestOpenCode.Failed && File.ReadAllBytes(path).SequenceEqual(overflowBytes)
            && profile.InventoryCount(box) == 2 && !profile.Save.Player.AppliedInventoryEventIds.Contains("overflow"),
            "failed reward grant rolls back costs and receipt");
        Check(profile.ApplyInventoryTransaction("clear-overflow", save => save.Player.Inventory.RemoveAll(row => row.ItemId == option)).IsApplied,
            "clear owned overflow fixture");
        Check(service.Open(box, 1, new[] { option }, now, "overflow").IsApplied,
            "a rolled-back receipt can be retried after its actual cause is resolved");
        var other = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Check(other.ApplyInventoryTransaction("other-feature", save => {
            OfflineInventoryOperations.Grant(save.Player, box, 4, now);
            OfflineInventoryOperations.Grant(save.Player, 2, 17, now);
        }).IsApplied, "independent feature commits real inventory");
        Check(service.Open(box, 4, new[] { option }, now, "fresh-stock").IsApplied
            && profile.InventoryCount(box) == 1 && profile.InventoryCount(2) == 17,
            "opening reloads latest inventory and preserves unrelated feature changes");
        var multi = (JObject)json.DeepClone();
        var first = ((JArray)multi["tables"]["chest"]).Single(row => (int)row["ClassId"] == box);
        first["OptionCount"] = 2;
        var multiRules = new OfficialChestUsePreviewRules(multi.ToString());
        var multiChest = multiRules.OptionalChest(box);
        Seed(profile, box, 3, "multi");
        var options = multiChest.Options.Keys.Take(2).ToArray();
        var multiple = new OfflineOptionalChestService(multiRules, profile).Open(box, 3, options, now, "multiple-options");
        Check(multiple.IsApplied && profile.InventoryCount(box) == 0
            && options.All(id => profile.InventoryCount(id) == multiChest.Options[id] * 3),
            "multiple original options consume each box once and grant each selected entry");
    }
    static void Seed(OfflineMainlineProgressSession profile, int id, long count, string key)
        => Check(profile.ApplyInventoryTransaction("seed:" + key, save => {
            save.Player.Inventory.Clear();
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = id, Count = count });
        }).IsApplied, "seed isolated real profile: " + key);
    static long OpenCount(OfflineMainlineProgressSession profile, int id)
        => profile.Save.World.OpenChestCounts.FirstOrDefault(row => row.ChestId == id)?.Count ?? 0;
    static void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks.Add("PASS " + name); }
}
