using System;
using System.Collections.Generic;
using System.Linq;

namespace TreasureOracle
{
    // Parser containers and saved-state inputs for untouched original methods.
    public enum Quality { }
    public enum ItemType { TreasurePiece = 306, CommonTreasureMaterial = 427 }
    public sealed class ItemInfo { public int ClassId; public Quality Quality; public ItemType ItemType; }
    public sealed class TreasureInfo
    {
        public int StarMax, TreasurePieceId;
        public ItemInfo ItemInfo;
        public bool LimitUseCommonMaterial;
        public int UpgradeMaxStar => StarMax - 1;
    }
    public sealed class TreasureUpgradeInfo
    {
        public int PieceCount, SpecialPieceCount;
        public Dictionary<int, long> Materials;
    }
    public sealed class PlayerItemParamTreasureWrap { public bool Active; public int Star; }
    public sealed class PlayerItemDataWrap
    {
        public int Id;
        public long Count;
        public ItemInfo Info;
        public PlayerItemParamTreasureWrap State;
        public PlayerItemParamTreasureWrap GetItemParamTreasure() => State;
    }
    public sealed class Player
    {
        public readonly Dictionary<int, PlayerItemDataWrap> Items = new Dictionary<int, PlayerItemDataWrap>();
        public PlayerItemDataWrap GetItemById(int id) => Items.TryGetValue(id, out var item) ? item : null;
        public long GetItemAmount(int id) => GetItemById(id)?.Count ?? 0;
        public bool HasItem(int id, long count) => GetItemAmount(id) >= count;
        public bool HasItems(Dictionary<int, long> costs) => costs.All(row => GetItemAmount(row.Key) >= row.Value);
    }
    public sealed class ItemParser
    {
        public readonly Dictionary<int, ItemInfo> Rows = new Dictionary<int, ItemInfo>();
        public ItemInfo Get(int id) => Rows.TryGetValue(id, out var row) ? row : null;
    }
    public sealed class TreasureParser
    {
        public readonly Dictionary<int, TreasureInfo> Rows = new Dictionary<int, TreasureInfo>();
        public TreasureInfo Get(int id) => Rows.TryGetValue(id, out var row) ? row : null;
    }
    public sealed partial class OriginalTreasureCostParser
    {
        public const int sCommonPieceClassId = -2147483555;
        public readonly Dictionary<(Quality, int), TreasureUpgradeInfo> Rows = new Dictionary<(Quality, int), TreasureUpgradeInfo>();
        public readonly HashSet<int> MaterialsClassId = new HashSet<int>();
        public TreasureUpgradeInfo Get(Quality quality, int star) => Rows.TryGetValue((quality, star), out var row) ? row : null;
    }
    public sealed class LevelParser { public readonly HashSet<int> MaterialsClassId = new HashSet<int>(); }
    public sealed class CommonMaterial { public int ItemClassId, Item2PieceCount; }
    public sealed class TreasureSettings { public readonly Dictionary<Quality, CommonMaterial> CommonMaterial2Piece = new Dictionary<Quality, CommonMaterial>(); }
    public sealed class Settings { public readonly TreasureSettings TreasureSettings = new TreasureSettings(); }
    public delegate void MergeCosts(Player player, ref Dictionary<int, long> result, Dictionary<int, long> requested);
    public sealed class ItemMergeInfoParser
    {
        public MergeCosts Calculate;
        public void CalcMergeCost(Player player, ref Dictionary<int, long> result, Dictionary<int, long> requested)
            => Calculate(player, ref result, requested);
    }
    public sealed class Parsers
    {
        public static readonly Parsers Instance = new Parsers();
        public static readonly Settings GameSettings = new Settings();
        public readonly ItemParser ItemInfoParser = new ItemParser();
        public readonly TreasureParser TreasureInfoParser = new TreasureParser();
        public readonly OriginalTreasureCostParser TreasureUpgradeInfoParser = new OriginalTreasureCostParser();
        public readonly LevelParser TreasureLevelInfoParser = new LevelParser();
        public readonly ItemMergeInfoParser ItemMergeInfoParser = new ItemMergeInfoParser();
    }
    public static class Formula
    {
        public static int GetPieceIdByTreasureId(int id) => Parsers.Instance.TreasureInfoParser.Get(id)?.TreasurePieceId ?? -1;
        public static int GetTreasureIdByPieceId(int id) => Parsers.Instance.TreasureInfoParser.Rows
            .FirstOrDefault(row => row.Value.TreasurePieceId == id).Key;
    }
}
