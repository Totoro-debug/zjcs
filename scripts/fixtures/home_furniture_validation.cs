using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

public static class HomeFurnitureValidation
{
    public static void Run()
    {
        try { Check(); Zjcs.Editor.ProjectTools.Validate(); Debug.Log("HOME_FURNITURE_BINDING_PASSED"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void Check()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        Require(Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar), "Owned project");
        AudioListener.volume = 0;
        var configuration = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
        var partners = OfficialPartnerAssetCatalog.LoadDefault();
        var rules = OfficialHomeRules.LoadDefault();
        var saved = new OfflineMainlineSave();
        var slots = new ProfileSlotCatalog(Path.Combine(work, "furniture-saves-" + Guid.NewGuid().ToString("N")));
        string slot = slots.CreateSlot();
        var home = Open(slots, slot);
        var player = new OfflineHomeConditionPlayer(configuration, () => saved, type => home.OwnsFurniture(type));
        new OfflineCurrentHomeFurniture(configuration, partners.Conditions, () => player).Bind(rules);
        var progression = new OfflineHomeProgression(rules);
        var context = new HomeConditionContext { Level = 999, MaxProfessionRank = 999, HasManualMeetNpc = true };
        Require(rules.FurnitureVersion == "884_156692" && rules.Furniture.Count == 13, "Selected furniture binding");
        Require(progression.FurnitureState(home, 0, context) == HomeFurnitureState.Locked, "Saved level overrides legacy context");
        saved.Player.Level = 1;
        Require(progression.FurnitureState(home, 0, context) == HomeFurnitureState.RepairNotice, "Original repair notice");
        saved.Player.Level = 3;
        Require(progression.FurnitureState(home, 0, context) == HomeFurnitureState.CanRepair, "Original repair threshold");
        Require(progression.RepairFurniture(home, 0, context, player.GetItemAmount, null, null, "bed"), "Repair commits ownership");
        Require(!progression.RepairFurniture(home, 0, context, player.GetItemAmount, null, null, "bed"), "Repeated repair rejected");
        saved.Player.Level = 0;
        Require(progression.FurnitureState(home, 0, context) == HomeFurnitureState.Functional, "Ownership precedes unmet conditions");
        Require(player.GetItemAmount(4000) == 1 && !saved.Player.Inventory.Any(value => value.ItemId == 4000), "Owned furniture item projection without inventory mutation");
        saved.Player.Inventory.Add(new OfflineItemStack { ItemId = 4000, Count = 1 });
        Require(player.GetItemAmount(4000) == 1, "No duplicate ownership count");
        saved.Player.Inventory.Add(new OfflineItemStack { ItemId = 4001, Count = 1 });
        Require(progression.FurnitureState(home, 1, context) == HomeFurnitureState.Functional,
            "Original furniture item ownership precedes unlock conditions");
        saved.Player.Inventory.RemoveAll(value => value.ItemId == 4001);
        int book = Convert.ToInt32(configuration.Rows("item").First(row => Convert.ToInt32(row["ItemType"]) == 305)["ClassId"]);
        saved.Player.Inventory.Add(new OfflineItemStack { ItemId = book, Count = 1 });
        Require(rules.IsMet(rules.FurnitureOf(1).unlock, context), "Selected composite resolves original item type");
        saved.Player.Inventory.RemoveAll(value => value.ItemId == book);
        Require(!rules.IsMet(rules.FurnitureOf(1).unlock, context), "Inventory removal refreshes condition");
        saved.Player.MaxProfessionRank = 99;
        Require(!rules.IsMet(rules.FurnitureOf(5).unlock, context), "Cached profession summary is ignored");
        int profession = Convert.ToInt32(configuration.Rows("profession_base").First(row => Convert.ToInt32(row["Rank"]) == 1)["Profession"]);
        saved.Player.ProfessionLevels.Add(new OfflineProfessionLevel { Profession = profession, Level = 1 });
        Require(rules.IsMet(rules.FurnitureOf(5).unlock, context), "Actual unlocked profession rank enables statue");
        saved.Player.Level = 38;
        saved.Player.HasManualMeetNpc = false;
        Require(rules.IsMet(rules.FurnitureOf(2).unlock, context), "Meetable manual partner enables dining table before saved meet flag");
        saved.Player.Level = 31;
        Require(!rules.IsMet(rules.FurnitureOf(4).unlock, context), "Fruit tree requires a real goddess");
        saved.World.ActivatedGoddessRegions.Add(new OfflineMapRegionKey { MapClassId = 11, Region = 0 });
        Require(rules.IsMet(rules.FurnitureOf(4).unlock, context), "Activated goddess and level unlock production tree");
        saved.Player.Level = 30;
        Require(!rules.IsMet(rules.FurnitureOf(9).unlock, context), "Chest machine requires accessible map 11");
        saved.Player.UnlockedMapClassIds.Add(11);
        Require(rules.IsMet(rules.FurnitureOf(9).unlock, context), "Accessible map satisfies current composite");
        saved.Player.Level = 50;
        Require(rules.FurnitureOf(11).additionalRepair.SequenceEqual(new[] { 4016 }), "Original additional-repair item identity retained");
        Require(!home.OwnsFurniture(4) && !saved.Player.FruitTree.ProductionRepaired, "Production tree starts unrepaired");
        Require(progression.RepairFurniture(home, 11, context, player.GetItemAmount, null, null, "pet-tree"), "AdditionalRepairList is not a prerequisite");
        Require(home.OwnsFurniture(11) && !home.OwnsFurniture(4), "Repair changes only requested furniture");
        home = Open(slots, slot);
        Require(home.OwnsFurniture(0) && home.OwnsFurniture(11) && !home.OwnsFurniture(4), "Ownership survives disk reopen");
        Require(player.GetItemAmount(4017) == 1 && player.GetItemAmount(4016) == 0, "Reopened ledger projects independent identities");
        saved = new OfflineMainlineSave();
        Require(!rules.IsMet(rules.FurnitureOf(1).unlock, context), "Replacing profile Save cannot retain previous player snapshot");
        saved.Player.Level = 999; saved.Player.SubRank = 1;
        Require(player.BaseLevel == 10, "Base level excludes blessing levels at current subrank cap");
        Debug.Log("HOME_FURNITURE_RULES_PASSED: selected 13 records, four states, current inventory/profession/partner/map/story/goddess inputs, independent repair and disk restore.");
    }

    private static OfflineHomeIncomeSession Open(ProfileSlotCatalog slots, string slot)
        => OfflineHomeIncomeSession.Open(slots, slot, new[] { new OfflineHomeIncomeDefinition {
            HangUpId = 1, OutputItemId = 50, Rule = new HangUpIncomeRule { LevelNum = 1, MaxStoreHours = 36 }
        } }, new Dictionary<int, long>(), new HashSet<int>(), Array.Empty<OfflineHomeRandomRewardDefinition>(), 1000000);

    private static void Require(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
}
