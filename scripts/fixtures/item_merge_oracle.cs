using System;
using System.Collections.Generic;
using System.Linq;

namespace ItemMergeOracle
{
    public enum ItemType { }
    public sealed class Player { public Func<int, long> Read; public long GetItemAmount(int id) => Read(id); }
    public sealed class ItemInfo { public ItemType ItemType; }
    public sealed class ItemInfoParser
    {
        public Dictionary<int, ItemType> Types;
        public ItemInfo Get(int id) => new ItemInfo { ItemType = Types[id] };
    }
    public sealed class ItemMergeInfo
    {
        public int ProductId, Count, Material1, Count1, Material2, Count2, Material3, Count3,
            Material4, Count4, Material5, Count5, LevelFactorId;
        public Dictionary<int, int> Materials = new Dictionary<int, int>();
    }
    public sealed class LevelNumber { public int Id; public List<double> Nums; }
    public sealed partial class LevelNumberParser
    {
        public Dictionary<int, LevelNumber> Rows;
        public LevelNumber Get(int id) => Rows.TryGetValue(id, out var row) ? row : null;
    }
    public sealed class ConfigManager
    {
        public ItemInfoParser Items;
        public LevelNumberParser Levels;
        public T GetParser<T>() => (T)(object)(typeof(T) == typeof(ItemInfoParser) ? (object)Items : Levels);
    }
    public class ParserBase { public ConfigManager ConfigManager; public virtual void PostParse() { } }
    public sealed class Settings { public long MaxMergeAmountAtATime; }
    public static class Parsers { public static readonly Settings GameSettings = new Settings(); }
    public static class CollectionExtensions
    {
        public static V GetValueOrDefault<K, V>(IReadOnlyDictionary<K, V> rows, K key)
            => rows.TryGetValue(key, out var value) ? value : default;
    }
    public struct GLoggerErrorInterpolatedStringHandler
    {
        public GLoggerErrorInterpolatedStringHandler(int literals, int args, out bool enabled) { enabled = false; }
        public void AppendLiteral(string text) { }
        public void AppendFormatted<T>(T value, int alignment, string format, string name) { }
    }
    public static class Log
    {
        public static int Errors;
        public static void Error(ref GLoggerErrorInterpolatedStringHandler handler, object owner, string method, string path, int line) { Errors++; }
    }
    public sealed partial class OriginalItemMergeParser
    {
        public readonly Dictionary<int, ItemMergeInfo> configs = new Dictionary<int, ItemMergeInfo>();
        private readonly Dictionary<int, List<ItemMergeInfo>> ItemMergeDict = new Dictionary<int, List<ItemMergeInfo>>();
        private readonly Dictionary<ItemType, List<ItemMergeInfo>> ProductDict = new Dictionary<ItemType, List<ItemMergeInfo>>();
        public ItemMergeInfo Get(int id) => configs.TryGetValue(id, out var row) ? row : null;
    }
}
