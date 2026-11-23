using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.BagItemQueries;
using Zjcs.Official.BagTipDots;

[InitializeOnLoad]
public static class BagItemQueriesValidation
{
    const string Active = "Zjcs.BagItemQueriesValidation.Active";
    static BagItemQueriesValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            SessionState.SetBool(Active, false); AudioListener.volume = 0;
            try { Check(); Debug.Log("BAG_ITEM_QUERIES_PASSED: all original Bag eligibility/count query branches, exact thresholds and short-circuit order. Query component only; host/inventory owner integration remains separate."); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        };
    }
    public static void Run()
    {
        var project = Environment.GetEnvironmentVariable("ZJCS_BAG_QUERY_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
            throw new InvalidOperationException("Select the independent project explicitly");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
    }
    static void Require(bool value, string text) { if (!value) throw new Exception(text); Debug.Log("CHECK: " + text); }
    static PlayerItemDataWrap Item(ItemType type, int id = 1, long count = 1) => new PlayerItemDataWrap {
        Id = id, Count = count, Info = new BagQueryItemInfo { ClassId = id, ItemType = type, RedDotType = RedDotType.BlueDot } };
    static void Check()
    {
        var services = new Services(); var rules = new BagItemQueryRules(services);
        var item = Item(ItemType.CommonEquipPiece);
        services.Show.Add(item.Info.ItemType); services.EquipRows.Values[1] = new EquipPieceMergeInfo { PieceMergeCount = 3 };
        item.Count = 0;
        Require(!rules.ShowBagCanUseItemRedDot(item) && services.Lookups == 0 && services.SettingsReads == 0, "Nonpositive count returns before settings or table/player queries");
        item.Count = -4; Require(!rules.ShowBagCanUseItemRedDot(item), "Negative count retains the original unavailable branch");
        item.Count = 3; services.Show.Clear();
        Require(!rules.ShowBagCanUseItemRedDot(item) && services.HasItemCalls == 0, "Type settings gate every original item-specific branch");
        services.Show.Add(ItemType.CommonEquipPiece); services.Counts[1] = 2;
        Require(!rules.ShowBagCanUseItemRedDot(item), "Equipment piece uses total player ownership below its exact threshold");
        services.Counts[1] = 3; item.Count = 1;
        Require(rules.ShowBagCanUseItemRedDot(item), "Eligibility queries complete player ownership rather than the visible record count");
        Require(rules.GetCanUseBlueDotCount(Item(ItemType.CommonEquipPiece, 1, 8)) == 2, "Equipment blue count uses integer division with remainder");
        services.EquipRows.Values.Remove(1);
        Require(!rules.CanMergeEquipPiece(1), "Absent equipment merge configuration is unavailable");
        services.ItemRows.Values[2] = new ItemPieceMergeInfo { PieceMergeCount = 4 }; services.Show.Add(ItemType.CommonItemPiece); services.Counts[2] = 3;
        Require(!rules.ShowBagCanUseItemRedDot(Item(ItemType.CommonItemPiece, 2, 9)), "Item pieces use their own merge configuration");
        services.Counts[2] = 4;
        Require(rules.CanMergeItemPiece(2) && rules.GetCanUseBlueDotCount(Item(ItemType.CommonItemPiece, 2, 11)) == 2, "Item merge eligibility and blue count preserve distinct ownership/count inputs");
        Require(!rules.CanMergeItemPiece(999), "Absent item merge configuration is unavailable");
        services.Pets[3] = new PetInfo { ClassId = 303, PieceMergeCount = 5 }; services.Show.Add(ItemType.PetPiece); services.Counts[3] = 4;
        Require(!rules.CanMergePetPiece(999) && !rules.CanMergePetPiece(3), "Pet pieces require a mapped pet and enough pieces");
        services.Counts[3] = 5;
        Require(rules.ShowBagCanUseItemRedDot(Item(ItemType.PetPiece, 3, 5)), "Unowned pet with the exact piece threshold is available");
        services.OwnedPets.Add(303); int calls = services.HasItemCalls;
        Require(!rules.CanMergePetPiece(3) && services.HasItemCalls == calls, "Already-owned pet short-circuits before item ownership lookup");
        Require(rules.GetCanUseBlueDotCount(Item(ItemType.PetPiece, 3, 12)) == 2, "Raw blue-count query retains its independent pet-piece division branch");
        services.RandomRows.Values[4] = new RandomTreasureExchangeInfo { RequireCount = 6 }; services.Show.Add(ItemType.RandomTreasurePiece); services.Counts[4] = 5;
        Require(!rules.CanMergeRandomTreasurePiece(999) && !rules.ShowBagCanUseItemRedDot(Item(ItemType.RandomTreasurePiece, 4, 50)), "Random treasure pieces require their original exchange row and total ownership");
        services.Counts[4] = 6;
        Require(rules.CanMergeRandomTreasurePiece(4) && rules.GetCanUseBlueDotCount(Item(ItemType.RandomTreasurePiece, 4, long.MaxValue)) == long.MaxValue / 6,
            "Random treasure threshold and long integer blue count retain original precision");
        services.Show.Add(ItemType.Material);
        Require(rules.ShowBagCanUseItemRedDot(Item(ItemType.Material)) && rules.GetCanUseBlueDotCount(Item(ItemType.Material, 1, 19)) == 19, "Other enabled item types retain default eligibility and unit divisor");
        var red = Item(ItemType.Material, 1, 19); red.Info.RedDotType = RedDotType.RedDot;
        Require(rules.GetCanUseBlueDotCount(red) == 0, "Blue count checks the authored red-dot type");
        services.Show.Remove(ItemType.Material);
        Require(rules.GetCanUseBlueDotCount(Item(ItemType.Material, 1, 19)) == 0, "Blue count checks type settings before division");
        services.ItemRows.Values[2].PieceMergeCount = 0; bool dividedByZero = false;
        try { rules.GetCanUseBlueDotCount(Item(ItemType.CommonItemPiece, 2, 9)); } catch (DivideByZeroException) { dividedByZero = true; }
        Require(dividedByZero, "Malformed zero divisor retains the original failure instead of manufacturing a count");
        services.ItemRows.Values[2].PieceMergeCount = 4;

        var card = new PeriodCardItemInfo { PeriodCardId = 8 };
        Require(!rules.CanUsePeriodCardItem(null) && !rules.CanUsePeriodCardItem(card) && services.BuyCalls == 0 && services.TimeCalls == 0,
            "Null/missing periodic-card data returns before the clock and purchase manager");
        services.PeriodRows.Values[8] = new PeriodicCardPayInfo { Id = 8 };
        Require(!rules.CanUsePeriodCardItem(card) && services.LastPayType == PayType.PeriodicCard && services.LastPayId == 8 && services.LastPayTime == services.Now,
            "Periodic card queries the original pay type, configured id and supplied game time");
        services.BuyFound = true; services.BuyCount = 31;
        Require(!rules.CanUsePeriodCardItem(card), "Periodic card rejects duration above the configured maximum");
        services.BuyCount = 30; services.Show.Add(ItemType.PeriodCard);
        var cardItem = Item(ItemType.PeriodCard); cardItem.Info.PartInfo = card;
        Require(rules.ShowBagCanUseItemRedDot(cardItem), "Periodic card permits exactly the configured duration maximum");
        cardItem.Info.PartInfo = new object(); Require(!rules.ShowBagCanUseItemRedDot(cardItem), "Wrong optional item-part type follows the original null-cast branch");

        services.Show.Add(ItemType.CookingFormula);
        Require(!rules.CanUseCookingFormula(9), "Absent recipe mapping is unavailable");
        services.Formulas[9] = 90;
        Require(!rules.CanUseCookingFormula(9) && services.CookingChecks == 0, "Absent mapped recipe row returns before its eligibility predicate");
        services.Cooking[90] = new CookingInfo { Id = 90 };
        Require(!rules.CanUseCookingFormula(9) && services.CookingChecks == 1, "Recipe retains its original IsLimit predicate result");
        services.CookingAllowed = true;
        Require(rules.ShowBagCanUseItemRedDot(Item(ItemType.CookingFormula, 9)) && services.LastCooking == services.Cooking[90], "Recipe dispatch passes the exact configured row to its owner");

        services.ReplaceRows.Values[10] = new EquipAttributeReplaceItemInfo { Quality = new List<int> { 4 }, AttributePropId = 700 };
        var replace = Item(ItemType.EquipAttributeReplaceItem, 10); replace.Info.RedDotType = RedDotType.BlueDot;
        Require(rules.GetItemRedDotType(replace) == RedDotType.BlueDot, "Without eligible equipment the replacement item retains its authored dot type");
        var equip = Item(ItemType.Sword, 50); equip.Info.Quality = 3;
        equip.Equip = new PlayerItemParamEquipWrap { PurifyProps = new object[0], OtherProps = new BagQueryEquipProps { RawData = new Dictionary<int, EquipPropBase> { [700] = new EquipPropBase { PropId = 701 } } } };
        services.Equipment.Add(equip); services.EquipTypesList.Add(ItemType.Sword);
        Require(!rules.CanUseEquipReplaceItem(replace), "Replacement eligibility filters equipment quality first");
        equip.Info.Quality = 4; equip.Equip.PurifyProps = new[] { new object() };
        Require(!rules.CanUseEquipReplaceItem(replace), "Replacement eligibility skips equipment with any purify properties");
        equip.Equip.PurifyProps = new object[0];
        Require(!rules.CanUseEquipReplaceItem(replace), "Replacement matches the raw property's PropId rather than its dictionary key");
        equip.Equip.OtherProps.RawData = new Dictionary<int, EquipPropBase> { [999] = new EquipPropBase { PropId = 700 } };
        Require(rules.CanUseEquipReplaceItem(replace) && rules.GetItemRedDotType(replace) == RedDotType.RedDot,
            "Eligible original equipment property overrides the replacement item's authored dot type");
        services.Show.Add(ItemType.EquipAttributeReplaceItem);
        Require(rules.GetCanUseBlueDotCount(replace) == replace.Count, "Raw blue-count query uses authored dot type independently of dynamic replacement dot selection");
        Require(rules.GetItemRedDotType(Item(ItemType.Material)) == RedDotType.BlueDot, "Other item types retain their configured dot type");
    }
    sealed class Table<T> : IBagQueryTable<T> where T : class
    {
        public readonly Dictionary<int, T> Values = new Dictionary<int, T>();
        public Action OnRead;
        public T Get(int id) { OnRead?.Invoke(); Values.TryGetValue(id, out var value); return value; }
    }
    sealed class Services : IBagItemQueryServices, IBagItemQuerySettings, IBagItemQueryTables, IBagItemQueryPlayer, IBagQueryPetTable, IBagQueryCookingTable
    {
        public readonly List<ItemType> Show = new List<ItemType>(), EquipTypesList = new List<ItemType>();
        public readonly Dictionary<int, long> Counts = new Dictionary<int, long>();
        public readonly HashSet<int> OwnedPets = new HashSet<int>();
        public readonly Dictionary<int, PetInfo> Pets = new Dictionary<int, PetInfo>();
        public readonly Dictionary<int, int> Formulas = new Dictionary<int, int>();
        public readonly Dictionary<int, CookingInfo> Cooking = new Dictionary<int, CookingInfo>();
        public readonly List<PlayerItemDataWrap> Equipment = new List<PlayerItemDataWrap>();
        public readonly Table<EquipPieceMergeInfo> EquipRows = new Table<EquipPieceMergeInfo>();
        public readonly Table<ItemPieceMergeInfo> ItemRows = new Table<ItemPieceMergeInfo>();
        public readonly Table<RandomTreasureExchangeInfo> RandomRows = new Table<RandomTreasureExchangeInfo>();
        public readonly Table<PeriodicCardPayInfo> PeriodRows = new Table<PeriodicCardPayInfo>();
        public readonly Table<EquipAttributeReplaceItemInfo> ReplaceRows = new Table<EquipAttributeReplaceItemInfo>();
        public readonly DateTime Now = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        public int HasItemCalls, Lookups, BuyCalls, TimeCalls, BuyCount, CookingChecks, LastPayId, SettingsReads;
        public bool BuyFound, CookingAllowed;
        public DateTime LastPayTime; public PayType LastPayType; public CookingInfo LastCooking;
        public Services() { EquipRows.OnRead = () => Lookups++; }
        public IBagItemQuerySettings Settings { get { SettingsReads++; return this; } }
        public IBagItemQueryTables Tables => this; public IBagItemQueryPlayer Player => this;
        public IReadOnlyCollection<ItemType> ShowCanUseRedDotItemTypes => Show;
        public IReadOnlyCollection<ItemType> EquipTypes => EquipTypesList;
        public BagQueryPaySettings Pay { get; } = new BagQueryPaySettings { PeriodicCardMaxDurationDay = 30 };
        public IBagQueryTable<EquipPieceMergeInfo> EquipPieceMergeInfoParser => EquipRows;
        public IBagQueryTable<ItemPieceMergeInfo> ItemPieceMergeInfoParser => ItemRows;
        public IBagQueryTable<RandomTreasureExchangeInfo> RandomTreasureExchangeInfoParser => RandomRows;
        public IBagQueryPetTable PetInfoParser => this;
        public IBagQueryTable<PeriodicCardPayInfo> PeriodicCardPayInfoParser => PeriodRows;
        public IBagQueryCookingTable CookingInfoParser => this;
        public IBagQueryTable<EquipAttributeReplaceItemInfo> EquipAttributeReplaceItemInfoParser => ReplaceRows;
        public IReadOnlyDictionary<int, int> Formula2CookingLookup => Formulas;
        public bool TryGet(int id, out CookingInfo value) => Cooking.TryGetValue(id, out value);
        public PetInfo GetByPieceClassId(int id) { Pets.TryGetValue(id, out var value); return value; }
        public bool HasItem(int id, long count) { HasItemCalls++; return Counts.TryGetValue(id, out var value) && value >= count; }
        public bool HasPet(int id) => OwnedPets.Contains(id);
        public bool TryGetBuyCount(PayType type, int id, DateTime time, out int count) { BuyCalls++; LastPayType = type; LastPayId = id; LastPayTime = time; count = BuyCount; return BuyFound; }
        public IReadOnlyCollection<PlayerItemDataWrap> FindItemsByType(IReadOnlyCollection<ItemType> types) => Equipment.Where(value => types.Contains(value.Info.ItemType)).ToArray();
        public DateTime GetDateTime() { TimeCalls++; return Now; }
        public bool IsLimit(CookingInfo value) { CookingChecks++; LastCooking = value; return CookingAllowed; }
    }
}
