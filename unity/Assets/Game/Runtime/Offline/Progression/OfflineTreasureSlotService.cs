using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using S = Zjcs.Official.TreasureSlots;

namespace Zjcs.Offline.Progression
{
    public sealed class OfflineTreasureSlotCatalog
    {
        [Serializable] private sealed class Payload
        {
            public int TreasureMaxSlotCount, TreasurePlanMaxCount;
            public int[] TreasureSlotElementTypes;
            public ItemElement[] Items;
        }
        [Serializable] private sealed class ItemElement { public int ClassId, ElementType; }
        private readonly Payload data;
        private readonly Dictionary<int, int> elements;
        public int MaxSlotCount => data.TreasureMaxSlotCount;
        public int MaxPlanCount => data.TreasurePlanMaxCount;
        private OfflineTreasureSlotCatalog(Payload data)
        {
            this.data = data;
            if (data == null || data.TreasureMaxSlotCount < 1 || data.TreasureMaxSlotCount > 32
                || data.TreasurePlanMaxCount < 1 || data.TreasurePlanMaxCount > 32
                || data.TreasureSlotElementTypes == null || data.TreasureSlotElementTypes.Length == 0
                || data.TreasureSlotElementTypes.Length > 16 || data.TreasureSlotElementTypes.Any(id => id < 0)
                || data.TreasureSlotElementTypes.Distinct().Count() != data.TreasureSlotElementTypes.Length
                || data.Items == null || data.Items.Length == 0 || data.Items.Any(row => row == null || row.ClassId <= 0
                    || !data.TreasureSlotElementTypes.Contains(row.ElementType))
                || data.Items.Select(row => row.ClassId).Distinct().Count() != data.Items.Length)
                throw new FormatException("Treasure slot configuration is invalid.");
            elements = data.Items.ToDictionary(row => row.ClassId, row => row.ElementType);
        }
        public static OfflineTreasureSlotCatalog LoadDefault()
        {
            var asset = Resources.Load<TextAsset>("Treasure/treasure-slots");
            if (asset == null) throw new InvalidOperationException("Original treasure slot configuration is unavailable.");
            return new OfflineTreasureSlotCatalog(JsonUtility.FromJson<Payload>(asset.text));
        }
        public bool TryGetElement(int id, out int element) => elements.TryGetValue(id, out element);
        public bool AllowsElement(int element) => data.TreasureSlotElementTypes.Contains(element);
        public S.TreasureSlotSettings CreateSettings() => new S.TreasureSlotSettings {
            TreasureMaxSlotCount = MaxSlotCount, TreasurePlanMaxCount = MaxPlanCount,
            TreasureSlotElementTypes = data.TreasureSlotElementTypes.Select(id => (S.ElementType)id).ToList() };
    }

    public sealed partial class OfflineTreasureSlotService
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflineTreasureSlotCatalog catalog;
        public event Action OnSetupTreasureSlot;
        public OfflineTreasureSlotService(OfflineMainlineProgressSession session, OfflineTreasureSlotCatalog catalog)
        { this.session = session ?? throw new ArgumentNullException(nameof(session)); this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }

        public S.Player Snapshot() => CreatePlayer(session.Save.Player, catalog);

        public ProgressionResult SetupTreasureSlot(int treasureId, int targetIndex, string receipt)
        {
            if (string.IsNullOrWhiteSpace(receipt) || receipt.Length > 160 || targetIndex < 0 || targetIndex >= catalog.MaxSlotCount
                || !catalog.TryGetElement(treasureId, out int element))
                return Result(ProgressionResultStatus.InvalidState, "A known treasure, slot and transaction receipt are required.");
            ProgressionResult committed;
            bool notified = false;
            try
            {
                session.Reload();
                if (session.Save.Player.AppliedInventoryEventIds.Contains(receipt)) return Result(ProgressionResultStatus.Unchanged);
                var owned = session.Save.Player.CharacterProperties.Treasures.FirstOrDefault(row => row.ClassId == treasureId);
                if (session.InventoryCount(treasureId) < 1 || owned == null || !owned.Active)
                    return Result(ProgressionResultStatus.NotReady, "An owned, activated treasure is required for replacement.");
                if (!S.TreasureHelper.CanSetupTreasureSlot(Snapshot(), treasureId, targetIndex))
                    return Result(ProgressionResultStatus.Unchanged);
                committed = session.ApplyInventoryTransaction(receipt, next => {
                    var player = CreatePlayer(next.Player, catalog);
                    player.OnSetupTreasureSlot += () => notified = true;
                    int origin = player.GetTreasureInSlotIndex(treasureId);
                    player.SetupTreasureInSlot(treasureId, (S.ElementType)element, origin, targetIndex);
                    S.PlayerTreasurePlanExt.WriteWornTreasureElementToActivePlan(player, (S.ElementType)element);
                    StorePlayer(next.Player, player);
                });
            }
            catch (IOException error) { return Result(ProgressionResultStatus.InvalidState, error.Message); }
            catch (UnauthorizedAccessException error) { return Result(ProgressionResultStatus.InvalidState, error.Message); }
            catch (FormatException error) { return Result(ProgressionResultStatus.InvalidState, error.Message); }
            if (committed.IsApplied && notified) OnSetupTreasureSlot?.Invoke();
            return committed;
        }

        public static S.Player CreatePlayer(OfflinePlayerProgress player, OfflineTreasureSlotCatalog catalog)
        {
            if (player == null || catalog == null) throw new ArgumentNullException();
            var settings = catalog.CreateSettings();
            var result = new S.Player(settings, id => player.Inventory.Any(row => row.ItemId == id)
                && catalog.TryGetElement(id, out int element) ? new S.SlotItem { Id = id,
                    Count = player.Inventory.First(row => row.ItemId == id).Count,
                    Info = new S.SlotItemInfo { ElementType = (S.ElementType)element } } : null,
                message => Debug.LogError(message));
            var data = player.CharacterProperties.TreasureSlots;
            if (!OfflineTreasureSlotProgress.IsValid(data)) throw new FormatException("Treasure slot save data is invalid.");
            if (data.Format == 0) ImportLegacy(result, player.CharacterProperties, catalog);
            else Restore(result, data, catalog);
            return result;
        }

        private static void ImportLegacy(S.Player player, OfflineCharacterPropertyProgress legacy, OfflineTreasureSlotCatalog catalog)
        {
            foreach (var shared in legacy.TreasureElementLevels)
            {
                if (!catalog.AllowsElement(shared.ElementType)) throw new FormatException("Legacy treasure shared element is unknown.");
                var group = player.GetTreasureElementSlotData((S.ElementType)shared.ElementType);
                group.ShareLevel = shared.Level; group.BaseShareLevel = shared.BaseLevel;
            }
            foreach (var item in legacy.Treasures)
            {
                if (item.SlotIndex < 0) continue;
                if (item.SlotIndex >= catalog.MaxSlotCount || !catalog.TryGetElement(item.ClassId, out int element))
                    throw new FormatException("Legacy treasure slot cannot be resolved against original configuration.");
                var group = player.GetTreasureElementSlotData((S.ElementType)element);
                if (group.TreasureSlots.ContainsKey(item.SlotIndex)) throw new FormatException("Legacy treasures occupy the same slot.");
                group.TreasureSlots.Add(item.SlotIndex, new S.PlayerTreasureSlotItemDataWrap {
                    TreasureClassId = item.ClassId, BaseLevel = item.BaseLevel, BlessLevel = item.Level - item.BaseLevel });
            }
            foreach (var item in legacy.Treasures.Where(row => row.SlotIndex < 0 && (row.Level != 1 || row.BaseLevel != 1)))
            {
                if (!catalog.TryGetElement(item.ClassId, out int element)) throw new FormatException("Legacy treasure element is unknown.");
                var group = player.GetTreasureElementSlotData((S.ElementType)element);
                if (group.GetShareLevel() != item.Level || group.GetBaseShareLevel() != item.BaseLevel)
                    throw new FormatException("Legacy private treasure levels conflict with element share levels.");
            }
            // The old representation had no plans. Preserve its explicit equipment in the default plan.
            foreach (var element in player.TreasureSlotData.TreasureSlotDataDict.Keys.ToArray())
                S.PlayerTreasurePlanExt.WriteWornTreasureElementToActivePlan(player, element);
        }

        private static void Restore(S.Player player, OfflineTreasureSlotProgress data, OfflineTreasureSlotCatalog catalog)
        {
            if (data.ActivePlanIndex >= catalog.MaxPlanCount) throw new FormatException("Treasure active plan index is invalid.");
            player.ActivePlanIndex = data.ActivePlanIndex;
            player.BlessRecordFormat = data.BlessRecordFormat;
            player.TreasureSlotData.SeasonId = data.SeasonId;
            player.TreasureSlotData.UpgradeUseCommonMaterailEnable = data.UpgradeUseCommonMaterailEnable;
            player.TreasureSlotData.AwakeAutoMergeEnable = data.AwakeAutoMergeEnable;
            foreach (var element in data.Elements)
            {
                if (!catalog.AllowsElement(element.ElementType)) throw new FormatException("Treasure slot element is unknown.");
                var group = player.GetTreasureElementSlotData((S.ElementType)element.ElementType);
                group.ShareLevel = element.ShareLevel; group.BaseShareLevel = element.BaseShareLevel;
                foreach (var slot in element.Slots)
                {
                    RequireSlot(catalog, element.ElementType, slot.Index, slot.TreasureClassId);
                    group.TreasureSlots.Add(slot.Index, new S.PlayerTreasureSlotItemDataWrap {
                        TreasureClassId = slot.TreasureClassId, BaseLevel = slot.BaseLevel, BlessLevel = slot.BlessLevel });
                }
            }
            foreach (var plan in data.Plans)
            {
                if (plan.Index >= catalog.MaxPlanCount) throw new FormatException("Treasure plan index is invalid.");
                var content = (S.PlayerBattlePlanTreasurePlanContentWrap)player.GetOrCreatePlanContent(S.BattlePlanType.Treasure, plan.Index);
                foreach (var element in plan.Elements)
                {
                    if (!catalog.AllowsElement(element.ElementType)) throw new FormatException("Treasure plan element is unknown.");
                    var slots = new Dictionary<int, int>();
                    foreach (var slot in element.Slots)
                    { RequireSlot(catalog, element.ElementType, slot.Index, slot.TreasureClassId); slots.Add(slot.Index, slot.TreasureClassId); }
                    content.Data.TreasureSlots.Add((S.ElementType)element.ElementType, slots);
                }
            }
            foreach (var record in data.BlessRecords)
            {
                var restored = new S.PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap(player.Settings, message => Debug.LogError(message));
                foreach (var element in record.Elements)
                {
                    if (!catalog.AllowsElement(element.ElementType)) throw new FormatException("Treasure blessing record element is unknown.");
                    var group = new S.PlayerSeasonRecordPlayerTreasureBlessRecordDataWrap();
                    foreach (var slot in element.Slots)
                    {
                        if (slot.Index >= catalog.MaxSlotCount) throw new FormatException("Treasure blessing record index is invalid.");
                        group.TreasureSlotBlessLevelDict.Add(slot.Index, slot.Level);
                    }
                    restored.ElementTreasureSlotBlessDataDict.Add((S.ElementType)element.ElementType, group);
                }
                player.BlessRecords.Add(record.SeasonId, restored);
            }
        }

        private static void RequireSlot(OfflineTreasureSlotCatalog catalog, int element, int index, int treasure)
        {
            if (index < 0 || index >= catalog.MaxSlotCount || treasure != 0
                && (!catalog.TryGetElement(treasure, out int expected) || element != expected))
                throw new FormatException("Treasure slot identity or element is invalid.");
        }

        public static OfflineTreasureSlotProgress Capture(S.Player player)
        {
            var result = new OfflineTreasureSlotProgress { Format = 1, ActivePlanIndex = player.ActivePlanIndex,
                SeasonId = player.TreasureSlotData.SeasonId, UpgradeUseCommonMaterailEnable = player.TreasureSlotData.UpgradeUseCommonMaterailEnable,
                AwakeAutoMergeEnable = player.TreasureSlotData.AwakeAutoMergeEnable, BlessRecordFormat = player.BlessRecordFormat };
            foreach (var element in player.TreasureSlotData.TreasureSlotDataDict)
                result.Elements.Add(new OfflineTreasureElementSlots { ElementType = (int)element.Key,
                    ShareLevel = element.Value.ShareLevel, BaseShareLevel = element.Value.BaseShareLevel,
                    Slots = element.Value.TreasureSlots.Select(slot => new OfflineTreasureSlot { Index = slot.Key,
                        TreasureClassId = slot.Value.TreasureClassId, BaseLevel = slot.Value.BaseLevel, BlessLevel = slot.Value.BlessLevel }).ToList() });
            foreach (var plan in player.Plans)
                result.Plans.Add(new OfflineTreasureSlotPlan { Index = plan.Key,
                    Elements = plan.Value.Data.TreasureSlots.Select(element => new OfflineTreasurePlanElement { ElementType = (int)element.Key,
                        Slots = element.Value.Select(slot => new OfflineTreasurePlanSlot { Index = slot.Key, TreasureClassId = slot.Value }).ToList() }).ToList() });
            foreach (var record in player.BlessRecords)
                result.BlessRecords.Add(new OfflineTreasureSlotBlessRecord { SeasonId = record.Key,
                    Elements = record.Value.ElementTreasureSlotBlessDataDict.Select(element => new OfflineTreasureSlotBlessElement {
                        ElementType = (int)element.Key, Slots = element.Value.TreasureSlotBlessLevelDict.Select(slot =>
                            new OfflineTreasureSlotBlessLevel { Index = slot.Key, Level = slot.Value }).ToList() }).ToList() });
            if (!OfflineTreasureSlotProgress.IsValid(result)) throw new FormatException("Original treasure slot operation produced invalid local data.");
            return result;
        }
        private static void StorePlayer(OfflinePlayerProgress player, S.Player view)
        {
            player.CharacterProperties.TreasureSlots = Capture(view);
            // Legacy fields are import-only. Canonical slots own equipment and training.
            player.CharacterProperties.TreasureElementLevels.Clear();
            foreach (var item in player.CharacterProperties.Treasures) { item.SlotIndex = -1; item.Level = 1; item.BaseLevel = 1; }
        }
        private static ProgressionResult Result(ProgressionResultStatus status, string message = null)
            => new ProgressionResult { Status = status, Message = message ?? string.Empty };
    }
}
