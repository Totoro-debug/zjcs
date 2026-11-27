using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using S = Zjcs.Official.TreasureSlots;
using M = Zjcs.Official.ItemMerge;

namespace Zjcs.Offline.Progression
{
    // Capture these from the common growth/world owner against the supplied candidate player.
    public sealed class TreasureSlotLevelEnvironment
    {
        public int BaseLevel, Level, PlayerBlessLevel, WorldRank, ServerRecordSeason;
        public int? ActiveRecordSeason;
    }

    public sealed class OfflineTreasureSlotLevelCatalog
    {
        public sealed class Condition
        {
            public string Type, Op;
            public int ClassId, Count, ItemType, Level;
            public Condition[] Children;
            internal S.ICondition Create()
            {
                switch (Type)
                {
                    case "HasItemCondition": return new S.HasItemCondition { ClassId = ClassId, Count = Count, Op = Op };
                    case "HasItemTypeCondition": return new S.HasItemTypeCondition { ItemType = ItemType, Count = Count };
                    case "LevelCondition": return new S.LevelCondition { Level = Level, Op = Op };
                    case "CompositeOrCondition":
                        if (Children == null) throw new FormatException("Missing original composite conditions.");
                        return new S.CompositeOrCondition { Children = Children.Select(row => row.Create()).ToList() };
                    default: throw new FormatException("Unbound original merge-source condition: " + Type);
                }
            }
        }
        private sealed class MergeProduct { public int ItemId; public Condition Condition; }
        private sealed class Payload
        {
            public S.SystemLevelLimitInfo[] Limits;
            public S.SystemLevelGapLimitInfo[] Gaps;
            public S.TreasureLevelInfo[] LevelCosts;
            public S.TreasureBlessInfo[] BlessCosts;
            public S.TreasureLevelSeason[] Seasons;
            public Condition MergeSourceBagualu;
            public MergeProduct[] MergeSourceProducts;
        }
        private readonly Payload data;
        private readonly S.ICondition bagualu;
        private readonly Dictionary<int, S.ICondition> productConditions;
        private readonly HashSet<int> seasons;
        internal readonly M.OfficialItemMergeRules Merge;
        private OfflineTreasureSlotLevelCatalog(Payload data)
        {
            this.data = data ?? throw new FormatException("Missing treasure level data.");
            if (data.Limits == null || data.Limits.Length == 0 || data.Gaps == null || data.LevelCosts == null
                || data.BlessCosts == null || data.Seasons == null || data.MergeSourceBagualu == null || data.MergeSourceProducts == null
                || data.Limits.Any(row => row == null || row.PlayerLevel < 0 || row.PlayerBlessLevel < 0 || row.TreasureLevel < 0 || row.TreasureBlessLevel < 0)
                || data.Gaps.Any(row => row == null || row.Level < 0 || row.LimitLevel < 0)
                || data.LevelCosts.Any(row => row == null || row.Level < 1 || !ValidCosts(row.Materials))
                || data.BlessCosts.Any(row => row == null || row.BlessLevel < 1 || !ValidCosts(row.LevelCostItems))
                || data.Seasons.Any(row => row == null || row.Season <= 0)
                || data.MergeSourceProducts.Any(row => row == null || row.ItemId <= 0))
                throw new FormatException("Treasure level configuration is invalid.");
            bagualu = data.MergeSourceBagualu.Create();
            productConditions = data.MergeSourceProducts.ToDictionary(row => row.ItemId, row => row.Condition?.Create());
            seasons = new HashSet<int>(data.Seasons.Select(row => row.Season));
            Merge = OfflineItemMergeCatalog.LoadDefault().CreateRules();
            foreach (int material in data.LevelCosts.SelectMany(row => row.Materials.Keys)
                .Concat(data.BlessCosts.SelectMany(row => row.LevelCostItems.Keys)).Distinct())
                if (Merge.Get(material) != null && !productConditions.ContainsKey(material))
                    throw new FormatException("Original automatic-merge source is unbound.");
        }
        private static bool ValidCosts(Dictionary<int, long> rows) => rows != null && rows.All(row => row.Key > 0 && row.Value > 0);
        public static OfflineTreasureSlotLevelCatalog LoadDefault()
        {
            var asset = Resources.Load<TextAsset>("Treasure/treasure-slot-levels");
            if (asset == null) throw new InvalidOperationException("Original treasure slot level data is unavailable.");
            return new OfflineTreasureSlotLevelCatalog(JsonConvert.DeserializeObject<Payload>(asset.text));
        }
        public S.TreasureSlotLevelTables CreateTables()
        {
            // Each detached player owns its table rows too; a query must not expose the catalog's mutable dictionaries.
            var tables = new S.TreasureSlotLevelTables(
                data.Limits.Select(row => new S.SystemLevelLimitInfo { SubRank = row.SubRank, PlayerLevel = row.PlayerLevel,
                    PlayerBlessLevel = row.PlayerBlessLevel, TreasureLevel = row.TreasureLevel, TreasureBlessLevel = row.TreasureBlessLevel }),
                data.Gaps.Select(row => new S.SystemLevelGapLimitInfo { DevelopType = row.DevelopType, SeasonId = row.SeasonId,
                    Level = row.Level, LimitLevel = row.LimitLevel, IsOpen = row.IsOpen }),
                data.LevelCosts.Select(row => new S.TreasureLevelInfo { Level = row.Level, Materials = new Dictionary<int, long>(row.Materials) }),
                data.BlessCosts.Select(row => new S.TreasureBlessInfo { Rank = row.Rank, BlessLevel = row.BlessLevel,
                    LevelCostItems = new Dictionary<int, long>(row.LevelCostItems) }),
                data.Seasons.Select(row => new S.TreasureLevelSeason { Rank = row.Rank, Season = row.Season }));
            tables.CalculateMergeable = (player, id, recursion) => Merge.CalcCanMergeAmout(new MergeInventory(player), id, recursion);
            return tables;
        }
        internal bool IsValidSeason(int season) => seasons.Contains(season);
        internal bool IsMergeSourceAvailable(S.Player player, int id)
        {
            if (!productConditions.TryGetValue(id, out var condition)) return false;
            var hub = new S.ICondition.DataHub(player);
            return bagualu.IsMet(hub) && (condition == null || condition.IsMet(hub));
        }
        internal Dictionary<int, long> Costs(S.Player player, Dictionary<int, long> requested, bool autoMerge)
        {
            if (!autoMerge) return new Dictionary<int, long>(requested);
            Dictionary<int, long> result = null;
            Merge.CalcMergeCost(new MergeInventory(player), ref result, requested);
            return result;
        }
        private sealed class MergeInventory : M.IItemMergeInventory
        {
            private readonly S.Player player;
            internal MergeInventory(S.Player player) { this.player = player; }
            public long GetItemAmount(int id) => player.GetItemAmount(id);
        }
    }

    public sealed partial class OfflineTreasureSlotService
    {
        private readonly OfflineTreasureSlotLevelCatalog levelCatalog;
        private readonly Func<OfflinePlayerProgress, TreasureSlotLevelEnvironment> readLevelEnvironment;
        public event Action<S.SlotItem, int, int> OnTreasureLevelUp;
        public event Action<S.SlotItem, bool> OnTreasureRefresh;
        public event Action<S.ElementType> OnTreasureSlotShareLevelUpdate;
        public event Action OnAllTreasureBlessUpdate;
        public event Action<int> OnSeasonBlessDataUpdate;

        public OfflineTreasureSlotService(OfflineMainlineProgressSession session, OfflineTreasureSlotCatalog catalog,
            OfflineTreasureSlotLevelCatalog levels, Func<OfflinePlayerProgress, TreasureSlotLevelEnvironment> readEnvironment)
            : this(session, catalog)
        {
            levelCatalog = levels ?? throw new ArgumentNullException(nameof(levels));
            readLevelEnvironment = readEnvironment ?? throw new ArgumentNullException(nameof(readEnvironment));
        }

        public S.Player CurrentLevelSnapshot(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0 || nowUnixSeconds > 253402300799L) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds));
            return CreateLevelPlayer(session.Save.Player, nowUnixSeconds, null);
        }

        public TreasureCultivationResult InspectTreasureSlotLevelUp(int element, int index, bool autoMerge, long nowUnixSeconds)
        {
            if (!ValidLevelRequest(element, index, nowUnixSeconds)) return new TreasureCultivationResult(ProgressionResultStatus.InvalidState);
            var player = CreateLevelPlayer(session.Save.Player, nowUnixSeconds, null);
            int code = S.TreasureHelper.CanTreasureSlotLevelUp(player, (S.ElementType)element, index, autoMerge);
            if (code != 0) return new TreasureCultivationResult(ProgressionResultStatus.NotReady, code);
            var slot = player.GetTreasureSlotData((S.ElementType)element, index);
            var needed = S.TreasureHelper.GetTreasureLevelUpCostMaterials(player, slot, slot.GetFinalLevel() + 1);
            return new TreasureCultivationResult(ProgressionResultStatus.Ready, 0, costs: levelCatalog.Costs(player, needed, autoMerge));
        }

        public TreasureCultivationResult UpgradeTreasureSlot(int element, int index, bool autoMerge, string receipt, long nowUnixSeconds)
        {
            if (!ValidLevelRequest(element, index, nowUnixSeconds) || string.IsNullOrWhiteSpace(receipt) || receipt.Length > 160)
                return new TreasureCultivationResult(ProgressionResultStatus.InvalidState);
            var events = new List<Action>();
            Dictionary<int, long> paid = null;
            int code = 0;
            ProgressionResult committed;
            try
            {
                session.Reload();
                if (session.Save.Player.AppliedInventoryEventIds.Contains(receipt)) return new TreasureCultivationResult(ProgressionResultStatus.Unchanged);
                committed = session.ApplyInventoryTransaction(receipt, next => {
                    var player = CreateLevelPlayer(next.Player, nowUnixSeconds, events);
                    code = S.TreasureHelper.CanTreasureSlotLevelUp(player, (S.ElementType)element, index, autoMerge);
                    if (code != 0) throw new InvalidOperationException("Original treasure slot rule rejected the request.");
                    var slot = player.GetTreasureSlotData((S.ElementType)element, index);
                    int before = slot.GetFinalLevel();
                    var needed = S.TreasureHelper.GetTreasureLevelUpCostMaterials(player, slot, before + 1);
                    paid = levelCatalog.Costs(player, needed, autoMerge);
                    foreach (var cost in paid)
                        if (cost.Value > 0 && !OfflineInventoryOperations.Consume(next.Player, cost.Key, cost.Value, nowUnixSeconds).IsApplied)
                            throw new InvalidOperationException("Treasure slot material debit failed.");
                    player.TreasureSlotLevelUp((S.ElementType)element, index, 1);
                    if (slot.GetFinalLevel() != before + 1) throw new InvalidOperationException("Treasure seasonal record did not accept the upgrade.");
                    StorePlayer(next.Player, player);
                });
            }
            catch (IOException error) { return new TreasureCultivationResult(ProgressionResultStatus.InvalidState, message: error.Message); }
            catch (UnauthorizedAccessException error) { return new TreasureCultivationResult(ProgressionResultStatus.InvalidState, message: error.Message); }
            catch (FormatException error) { return new TreasureCultivationResult(ProgressionResultStatus.InvalidState, message: error.Message); }
            if (code != 0) return new TreasureCultivationResult(ProgressionResultStatus.NotReady, code);
            if (committed.IsApplied) foreach (var action in events) action();
            return new TreasureCultivationResult(committed.Status, committed.IsApplied ? 0 : (int?)null, committed.Message, committed.IsApplied ? paid : null);
        }

        public ProgressionResult SynchronizeTreasureSlotSeason(string receipt, long nowUnixSeconds)
        {
            if (string.IsNullOrWhiteSpace(receipt) || receipt.Length > 160 || nowUnixSeconds < 0 || nowUnixSeconds > 253402300799L)
                return Result(ProgressionResultStatus.InvalidState);
            var events = new List<Action>();
            ProgressionResult result;
            try
            {
                session.Reload();
                if (session.Save.Player.AppliedInventoryEventIds.Contains(receipt)) return Result(ProgressionResultStatus.Unchanged);
                var snapshot = CreateLevelPlayer(session.Save.Player, nowUnixSeconds, null);
                if (JsonUtility.ToJson(Capture(snapshot)) == JsonUtility.ToJson(session.Save.Player.CharacterProperties.TreasureSlots))
                    return Result(ProgressionResultStatus.Unchanged);
                result = session.ApplyInventoryTransaction(receipt, next => StorePlayer(next.Player, CreateLevelPlayer(next.Player, nowUnixSeconds, events)));
            }
            catch (IOException error) { return Result(ProgressionResultStatus.InvalidState, error.Message); }
            catch (UnauthorizedAccessException error) { return Result(ProgressionResultStatus.InvalidState, error.Message); }
            catch (FormatException error) { return Result(ProgressionResultStatus.InvalidState, error.Message); }
            if (result.IsApplied) foreach (var action in events) action();
            return result;
        }

        private bool ValidLevelRequest(int element, int index, long now)
            => levelCatalog != null && readLevelEnvironment != null && catalog.AllowsElement(element) && index >= 0
                && index < catalog.MaxSlotCount && now >= 0 && now <= 253402300799L;

        private S.Player CreateLevelPlayer(OfflinePlayerProgress candidate, long nowUnixSeconds, List<Action> events)
        {
            if (levelCatalog == null || readLevelEnvironment == null) throw new InvalidOperationException("Bind the original level tables and common player/world environment.");
            var player = CreatePlayer(candidate, catalog);
            var env = readLevelEnvironment(candidate) ?? throw new InvalidOperationException("The current treasure growth environment is unavailable.");
            player.Tables = levelCatalog.CreateTables();
            player.SubRank = (S.SubRank)candidate.SubRank;
            player.BaseLevel = env.BaseLevel; player.Level = env.Level; player.PlayerBlessLevel = env.PlayerBlessLevel;
            player.MyPlanesRank = (S.Rank)env.WorldRank; player.ServerRecordSeason = env.ServerRecordSeason;
            if (env.BaseLevel < 0 || env.Level < 0 || env.PlayerBlessLevel < 0 || !Enum.IsDefined(typeof(S.Rank), env.WorldRank)
                || env.WorldRank <= 0 || env.ServerRecordSeason != player.Tables.GetSeasonByRank(player.Rank))
                throw new FormatException("The common player/world environment is inconsistent with original treasure season mapping.");
            DateTime now = DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).UtcDateTime;
            player.ReadItemAmount = id => OfflineItemAutoIncrease.Count(candidate.Inventory.FirstOrDefault(row => row.ItemId == id), now);
            player.MergeSourceAvailable = id => levelCatalog.IsMergeSourceAvailable(player, id);
            player.ItemManager.ItemTypeDict.Add(305, candidate.Inventory.Where(row => catalog.TryGetElement(row.ItemId, out _))
                .Select(row => new S.SlotItem { Id = row.ItemId, Count = row.Count }).ToList());
            foreach (int season in player.BlessRecords.Keys) if (!levelCatalog.IsValidSeason(season))
                throw new FormatException("Saved treasure blessing season is unknown.");
            if (player.BlessRecordFormat == 0)
            {
                if (player.TreasureSlotData.TreasureSlotDataDict.Values.Any(group => group.TreasureSlots.Values.Any(slot => slot.BlessLevel > 0)))
                {
                    int season = player.TreasureSlotData.SeasonId;
                    if (!levelCatalog.IsValidSeason(season)) throw new FormatException("Existing treasure blessings have no explicit valid season.");
                    var record = NewBlessRecord(player);
                    foreach (var group in player.TreasureSlotData.TreasureSlotDataDict)
                        foreach (var slot in group.Value.TreasureSlots)
                            record.ElementTreasureSlotBlessDataDict[group.Key].TreasureSlotBlessLevelDict[slot.Key] = slot.Value.BlessLevel;
                    player.BlessRecords.Add(season, record);
                }
                player.BlessRecordFormat = 1;
            }
            if (env.ActiveRecordSeason.HasValue)
            {
                int season = env.ActiveRecordSeason.Value;
                if (!candidate.HasActiveSeasonRecord || !levelCatalog.IsValidSeason(season)
                    || season != player.Tables.GetSeasonByRank((S.Rank)candidate.SeasonRank))
                    throw new FormatException("The active player season record is inconsistent.");
                if (!player.BlessRecords.ContainsKey(season)) player.BlessRecords.Add(season, NewBlessRecord(player));
            }
            if (events != null)
            {
                player.OnTreasureLevelUp += (item, old, current) => events.Add(() => OnTreasureLevelUp?.Invoke(item, old, current));
                player.OnTreasureRefresh += (item, isNew) => events.Add(() => OnTreasureRefresh?.Invoke(item, isNew));
                player.OnTreasureSlotShareLevelUpdate += element => events.Add(() => OnTreasureSlotShareLevelUpdate?.Invoke(element));
                player.OnAllTreasureBlessUpdate += () => events.Add(() => OnAllTreasureBlessUpdate?.Invoke());
                player.OnSeasonBlessDataUpdate += season => events.Add(() => OnSeasonBlessDataUpdate?.Invoke(season));
            }
            player.TryUpdatePlayerTreasureSlotData(player.Rank);
            return player;
        }
        private static S.PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap NewBlessRecord(S.Player player)
        {
            var record = new S.PlayerSeasonRecordPlayerBlessTreasureRecordDataWrap(player.Settings, message => Debug.LogError(message));
            record.Init(player, player.Rank); return record;
        }
    }
}
