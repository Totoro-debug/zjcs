using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.BagItemQueries;
using Zjcs.Official.BagTipDots;

namespace Zjcs.Offline.BagItemQueries
{
    // Selected official fields only. Original rules still execute all queries.
    public sealed class OfflineBagItemQueryCatalog : IBagItemQueryTables, IBagItemQuerySettings,
        IBagQueryPetTable, IBagQueryCookingTable
    {
        [Serializable] public sealed class ItemRow { public int ClassId, ItemType, RedDotType, Quality; }
        [Serializable] public sealed class MergeRow { public int Id, PieceMergeCount; }
        [Serializable] public sealed class RandomRow { public int ItemId, RequireCount; }
        [Serializable] public sealed class PetRow { public int ClassId, PetPieceId, PieceMergeCount; }
        [Serializable] public sealed class CardItemRow { public int ItemId, PeriodCardId; }
        [Serializable] public sealed class CardRow { public int Id; }
        [Serializable] public sealed class CookingRow
        { public int Id, ConditionItemId, UseSourceOperate, MapClassId; public string ConditionType; }
        [Serializable] public sealed class ReplaceRow { public int ItemId, AttributePropId; public int[] Quality; }
        [Serializable] public sealed class Payload
        {
            public ItemRow[] Items; public MergeRow[] EquipPieces, ItemPieces; public RandomRow[] RandomPieces;
            public PetRow[] Pets; public CardItemRow[] CardItems; public CardRow[] Cards;
            public CookingRow[] Cookings; public ReplaceRow[] Replacements;
            public int[] ShowTypes, EquipTypes; public int PeriodicCardMaxDurationDay;
        }
        sealed class Table<T> : IBagQueryTable<T> where T : class
        {
            readonly Dictionary<int, T> rows;
            internal Table(IEnumerable<KeyValuePair<int, T>> rows) => this.rows = rows.ToDictionary(row => row.Key, row => row.Value);
            public T Get(int id) { rows.TryGetValue(id, out var row); return row; }
        }
        readonly Dictionary<int, ItemRow> items;
        readonly Dictionary<int, PetInfo> pets;
        readonly Dictionary<int, PeriodCardItemInfo> cardItems;
        readonly Dictionary<int, CookingRow> cookings;
        readonly Dictionary<int, CookingInfo> cookingInfos;
        readonly Dictionary<int, int> formulas = new Dictionary<int, int>();
        readonly ItemType[] showTypes, equipTypes;
        public IBagQueryTable<EquipPieceMergeInfo> EquipPieceMergeInfoParser { get; }
        public IBagQueryTable<ItemPieceMergeInfo> ItemPieceMergeInfoParser { get; }
        public IBagQueryTable<RandomTreasureExchangeInfo> RandomTreasureExchangeInfoParser { get; }
        public IBagQueryPetTable PetInfoParser => this;
        public IBagQueryTable<PeriodicCardPayInfo> PeriodicCardPayInfoParser { get; }
        public IBagQueryCookingTable CookingInfoParser => this;
        public IBagQueryTable<EquipAttributeReplaceItemInfo> EquipAttributeReplaceItemInfoParser { get; }
        public IReadOnlyCollection<ItemType> ShowCanUseRedDotItemTypes => showTypes;
        public IReadOnlyCollection<ItemType> EquipTypes => equipTypes;
        public BagQueryPaySettings Pay { get; }
        public IReadOnlyDictionary<int, int> Formula2CookingLookup => formulas;
        public IReadOnlyCollection<ItemRow> Items => items.Values;
        public IReadOnlyCollection<CookingRow> Cookings => cookings.Values;
        public IReadOnlyCollection<PetRow> PetRows { get; }

        public static OfflineBagItemQueryCatalog LoadDefault()
        {
            var asset = Resources.Load<TextAsset>("Home/BagItemQueries/catalog");
            if (!asset) throw new InvalidOperationException("Original Bag query configuration is unavailable.");
            return new OfflineBagItemQueryCatalog(JsonUtility.FromJson<Payload>(asset.text));
        }
        public OfflineBagItemQueryCatalog(Payload data)
        {
            if (data == null || data.Items == null || data.EquipPieces == null || data.ItemPieces == null || data.RandomPieces == null
                || data.Pets == null || data.CardItems == null || data.Cards == null || data.Cookings == null || data.Replacements == null
                || data.ShowTypes == null || data.EquipTypes == null) throw new FormatException("Complete original Bag query configuration is required.");
            items = data.Items.ToDictionary(row => row.ClassId);
            showTypes = data.ShowTypes.Select(value => (ItemType)value).ToArray();
            equipTypes = data.EquipTypes.Select(value => (ItemType)value).ToArray();
            Pay = new BagQueryPaySettings { PeriodicCardMaxDurationDay = data.PeriodicCardMaxDurationDay };
            EquipPieceMergeInfoParser = new Table<EquipPieceMergeInfo>(data.EquipPieces.Select(row => Pair(row.Id, new EquipPieceMergeInfo { PieceMergeCount = row.PieceMergeCount })));
            ItemPieceMergeInfoParser = new Table<ItemPieceMergeInfo>(data.ItemPieces.Select(row => Pair(row.Id, new ItemPieceMergeInfo { PieceMergeCount = row.PieceMergeCount })));
            RandomTreasureExchangeInfoParser = new Table<RandomTreasureExchangeInfo>(data.RandomPieces.Select(row => Pair(row.ItemId, new RandomTreasureExchangeInfo { RequireCount = row.RequireCount })));
            PetRows = data.Pets;
            pets = data.Pets.ToDictionary(row => row.PetPieceId, row => new PetInfo { ClassId = row.ClassId, PieceMergeCount = row.PieceMergeCount });
            cardItems = data.CardItems.ToDictionary(row => row.ItemId, row => new PeriodCardItemInfo { PeriodCardId = row.PeriodCardId });
            PeriodicCardPayInfoParser = new Table<PeriodicCardPayInfo>(data.Cards.Select(row => Pair(row.Id, new PeriodicCardPayInfo { Id = row.Id })));
            cookings = data.Cookings.ToDictionary(row => row.Id);
            cookingInfos = data.Cookings.ToDictionary(row => row.Id, row => new CookingInfo { Id = row.Id });
            // CookingInfoParser.PostParse: ordered assignment, not Add/first-match.
            foreach (var row in data.Cookings)
                if (row.ConditionType == "UsedItemCondition") formulas[row.ConditionItemId] = row.Id;
            EquipAttributeReplaceItemInfoParser = new Table<EquipAttributeReplaceItemInfo>(data.Replacements.Select(row => Pair(row.ItemId,
                new EquipAttributeReplaceItemInfo { AttributePropId = row.AttributePropId, Quality = row.Quality.ToList() })));
        }
        static KeyValuePair<int, T> Pair<T>(int id, T value) => new KeyValuePair<int, T>(id, value);
        public ItemRow Item(int id) => items.TryGetValue(id, out var row) ? row : throw new InvalidOperationException("Original Bag item is unbound: " + id);
        public PeriodCardItemInfo CardItem(int id) { cardItems.TryGetValue(id, out var row); return row; }
        public CookingRow Cooking(int id) => cookings.TryGetValue(id, out var row) ? row : throw new InvalidOperationException("Original cooking condition is unbound: " + id);
        public PetInfo GetByPieceClassId(int id) { pets.TryGetValue(id, out var row); return row; }
        public bool TryGet(int id, out CookingInfo value) => cookingInfos.TryGetValue(id, out value);
    }
}
