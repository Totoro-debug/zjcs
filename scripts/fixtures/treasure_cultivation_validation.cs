using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.TreasureCultivation;
using O = TreasureOracle;

public static class TreasureCultivationValidation
{
    static string Work => Environment.GetEnvironmentVariable("ZJCS_TREASURE_CHECK_OUTPUT");
    static int checks, cases;
    static OfflineMainlineProgressSession profile;
    static OfflineTreasureService service;
    static OfflineTreasureCatalog catalog;
    static int[] sharedMaterialIds;
    static O.Player original;
    static readonly List<string> localMerge = new List<string>(), originalMerge = new List<string>();
    static readonly Dictionary<int, ItemInfo> items = new Dictionary<int, ItemInfo>();

    public static void Run()
    {
        try
        {
            Validate();
            File.WriteAllText(Path.Combine(Work, "result.json"), JsonConvert.SerializeObject(new { success = true, checks, cases, treasures = catalog.Treasures.Count }));
            UnityEngine.Debug.Log("TREASURE_CULTIVATION_PASSED: " + checks + " checks / " + cases + " original-method cases");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            UnityEngine.Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static void Validate()
    {
        catalog = OfflineTreasureCatalog.Load();
        sharedMaterialIds = catalog.UpgradeMaterials.SelectMany(row => row.Materials).Select(row => row.ItemId)
            .Concat(catalog.Settings.CommonMaterial2Piece.Select(row => row.ItemClassId)).Distinct().ToArray();
        var raw = JObject.Parse(File.ReadAllText(Path.Combine(Work, "OriginalTreasureRules.json")));
        var metadata = JArray.Parse(File.ReadAllText(Path.Combine(Work, "TreasureCultivationItems.json")));
        foreach (var row in metadata)
        {
            int id = (int)row["ClassId"];
            items.Add(id, new ItemInfo { ClassId = id, Quality = (int)row["Quality"], ItemType = (ItemType)(int)row["ItemType"] });
            O.Parsers.Instance.ItemInfoParser.Rows.Add(id, new O.ItemInfo { ClassId = id, Quality = (O.Quality)(int)row["Quality"], ItemType = (O.ItemType)(int)row["ItemType"] });
        }
        foreach (var row in raw["items"])
            Check(items[(int)row["ClassId"]].Quality == (int)row["Quality"], "shared item metadata agrees with original cultivation table");
        foreach (var row in raw["treasure"])
        {
            int id = (int)row["ClassId"];
            O.Parsers.Instance.TreasureInfoParser.Rows.Add(id, new O.TreasureInfo { StarMax = (int)row["StarMax"], TreasurePieceId = (int)row["TreasurePieceId"],
                LimitUseCommonMaterial = (bool)row["LimitUseCommonMaterial"], ItemInfo = O.Parsers.Instance.ItemInfoParser.Get(id) });
        }
        foreach (var row in raw["treasure_upgrade_material"])
        {
            var materials = ((JObject)row["Materials"]).Properties().ToDictionary(p => int.Parse(p.Name), p => (long)p.Value);
            O.Parsers.Instance.TreasureUpgradeInfoParser.Rows.Add(((O.Quality)(int)row["Quality"], (int)row["Star"]), new O.TreasureUpgradeInfo {
                PieceCount = (int)row["PieceCount"], SpecialPieceCount = (int)row["SpecialPieceCount"], Materials = materials });
            foreach (int id in materials.Keys) O.Parsers.Instance.TreasureUpgradeInfoParser.MaterialsClassId.Add(id);
        }
        foreach (var row in raw["treasure_levelup_material"])
            foreach (var material in ((JObject)row["Materials"]).Properties())
                O.Parsers.Instance.TreasureLevelInfoParser.MaterialsClassId.Add(int.Parse(material.Name));
        foreach (var row in ((JObject)raw["settings"]["TreasureSettings"]["CommonMaterial2Piece"]).Properties())
            O.Parsers.GameSettings.TreasureSettings.CommonMaterial2Piece.Add((O.Quality)int.Parse(row.Name), new O.CommonMaterial {
                ItemClassId = (int)row.Value["ItemClassId"], Item2PieceCount = (int)row.Value["Item2PieceCount"] });

        string saveRoot = Path.Combine(Work, "isolated-saves");
        var creation = CharacterCreationOfflineService.OpenDefault(saveRoot);
        string slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot); draft.name = "古遗物校验";
        Check(creation.TryCreateCharacter(slot, draft, out string error), "create actual isolated profile: " + error);
        Check(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out error), "set isolated weapon: " + error);
        var slots = new ProfileSlotCatalog(saveRoot);
        profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        service = new OfflineTreasureService(profile, catalog, ReadItem, LocalMerge);
        original = new O.Player();
        O.Parsers.Instance.ItemMergeInfoParser.Calculate = OriginalMerge;
        byte[] savedBefore = File.ReadAllBytes(Path.Combine(slots.GetSlotDirectory(slot), "city-save.json"));

        Seed(-1, 0, false, false, 0);
        Compare(0, "unknown definition");
        foreach (var info in catalog.Treasures)
        {
            Seed(info.ClassId, 0, false, false, 0); Compare(info.ClassId, "not owned");
            Seed(info.ClassId, 0, false, true, 1, false); Compare(info.ClassId, "missing treasure parameter");
            for (int star = 0; star <= info.StarMax; star++)
            {
                Seed(info.ClassId, star, false, true, 1); Compare(info.ClassId, "inactive");
                foreach (long available in new long[] { 0, 1, 10000000 })
                {
                    Seed(info.ClassId, star, true, true, available); Compare(info.ClassId, "active/availability " + available);
                }
            }
        }
        foreach (int id in items.Keys)
        {
            var item = new TreasureItem { Id = id, Info = items[id] };
            var oracle = new O.PlayerItemDataWrap { Id = id, Info = O.Parsers.Instance.ItemInfoParser.Get(id) };
            Check(service.Checks.IsItemEffectTreasurePromote(item) == O.OriginalTreasureHelper.IsItemEffectTreasurePromote(oracle), "promotion material classification " + id);
        }
        Check(File.ReadAllBytes(Path.Combine(slots.GetSlotDirectory(slot), "city-save.json")).SequenceEqual(savedBefore), "all read-only checks leave the disk profile unchanged");
        profile.Reload();
        var selected = catalog.Treasures.First();
        int selectedId = selected.ClassId;
        var needed = service.GetUpgrade(items[selectedId].Quality, 0).Materials;
        Check(profile.ApplyCharacterMutation(p => {
            p.Inventory.RemoveAll(row => row.ItemId == selectedId || needed.ContainsKey(row.ItemId));
            p.Inventory.Add(new OfflineItemStack { ItemId = selectedId, Count = 1 });
            foreach (var cost in needed) p.Inventory.Add(new OfflineItemStack { ItemId = cost.Key, Count = Math.Max(1, cost.Value) });
            p.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure { ClassId = selectedId, Star = 0, Active = false });
        }).IsApplied, "persist a real inactive treasure and its activation materials");
        Check(service.CanActivateTreasure(selectedId) == 0, "same adapter reads replaced saved player state");
        var detached = service.GetItemById(selectedId); detached.State.Active = true; detached.Info.Quality = -1;
        Check(service.CanActivateTreasure(selectedId) == 0 && items[selectedId].Quality >= 0, "detached item and parameter projections cannot mutate the profile or metadata");
        Check(profile.ApplyCharacterMutation(p => p.CharacterProperties.Treasures.First(row => row.ClassId == selectedId).Active = true).IsApplied, "persist active state through the established profile transaction");
        Check(service.CanActivateTreasure(selectedId) == 1224, "existing adapter reads a committed activation state without stale caching");
        var cold = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var coldService = new OfflineTreasureService(cold, catalog, ReadItem);
        Check(coldService.CanActivateTreasure(selectedId) == 1224, "actual cold profile restores the original already-active error");
        bool missingOwner = false;
        try { coldService.Checks.GetTreasureUpgradeMaterial(new Dictionary<int, long>(), true, coldService); }
        catch (InvalidOperationException) { missingOwner = true; }
        Check(missingOwner, "automatic merging requires its actual owner instead of pretending materials exist");
    }

    static ItemInfo ReadItem(int id) => items.TryGetValue(id, out var row) ? row : null;
    static void Seed(int id, int star, bool active, bool owned, long budget, bool parameter = true)
    {
        profile.Save.Player.Inventory.Clear(); profile.Save.Player.CharacterProperties.Treasures.Clear(); original.Items.Clear();
        int piece = catalog.GetTreasure(id)?.TreasurePieceId ?? 0;
        var materialIds = piece > 0 ? sharedMaterialIds.Concat(new[] { piece }).Distinct() : sharedMaterialIds.AsEnumerable();
        foreach (int material in materialIds)
        {
            profile.Save.Player.Inventory.Add(new OfflineItemStack { ItemId = material, Count = budget });
            original.Items.Add(material, new O.PlayerItemDataWrap { Id = material, Count = budget, Info = O.Parsers.Instance.ItemInfoParser.Get(material) });
        }
        if (!owned) return;
        profile.Save.Player.Inventory.Add(new OfflineItemStack { ItemId = id, Count = 1 });
        if (parameter) profile.Save.Player.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure { ClassId = id, Star = star, Active = active });
        original.Items.Add(id, new O.PlayerItemDataWrap { Id = id, Count = 1, Info = O.Parsers.Instance.ItemInfoParser.Get(id),
            State = parameter ? new O.PlayerItemParamTreasureWrap { Active = active, Star = star } : null });
    }
    static string CostText(Dictionary<int, long> value) => string.Join(";", value.Select(row => row.Key + ":" + row.Value));
    static void Compare(int id, string scenario)
    {
        cases++;
        string key = id + "/" + scenario;
        Check(service.CanActivateTreasure(id) == O.OriginalTreasureHelper.CanActivateTreasure(id, original), "activation " + key);
        Check(service.CanMergeTreasure(id) == O.OriginalTreasureHelper.CanMergeTreasure(id, original), "merge " + key);
        int piece = O.Formula.GetPieceIdByTreasureId(id);
        if (piece > 0) Check(service.PieceCanMerge(piece) == O.OriginalTreasureHelper.PieceCanMerge(piece, original), "piece hint " + key);
        foreach (bool empty in new[] { false, true }) foreach (bool common in new[] { false, true })
        {
            int left = service.GetTreasureNextLevelUpgradeMaterial(id, empty, common, out var costs);
            int right = O.OriginalTreasureHelper.GetTreasureNextLevelUpgradeMaterial(id, original, empty, common, out var oracle);
            Check(left == right && CostText(costs) == CostText(oracle), "ordered next-star costs " + key);
            foreach (bool autoMerge in new[] { false, true }) foreach (bool allowCommon in new[] { false, true })
            {
                var l = new Dictionary<int, long>(costs); var r = new Dictionary<int, long>(oracle);
                l[20000001] = r[20000001] = 0; l[20000002] = r[20000002] = -3;
                localMerge.Clear(); originalMerge.Clear();
                int got = service.CanTreasureUpgrade(id, l, autoMerge, allowCommon);
                int expected = O.OriginalTreasureHelper.CanTreasureUpgrade(id, r, original, autoMerge, allowCommon);
                Check(got == expected && CostText(l) == CostText(r) && localMerge.SequenceEqual(originalMerge), "star eligibility, dictionary effects and merge call order " + key);
            }
        }
    }
    static void LocalMerge(ITreasureCultivationPlayer player, ref Dictionary<int, long> result, Dictionary<int, long> requested)
    {
        Check(ReferenceEquals(player, service), "merge receives the owning player");
        localMerge.Add(CostText(requested)); result = new Dictionary<int, long>(requested);
    }
    static void OriginalMerge(O.Player player, ref Dictionary<int, long> result, Dictionary<int, long> requested)
    {
        Check(ReferenceEquals(player, original), "oracle merge receives the owning player");
        originalMerge.Add(CostText(requested)); result = new Dictionary<int, long>(requested);
    }
    static void Check(bool value, string name) { checks++; if (!value) throw new Exception("CHECK FAILED: " + name); }
}
