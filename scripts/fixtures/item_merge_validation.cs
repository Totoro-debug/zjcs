using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Zjcs.Offline.Progression;
using M = Zjcs.Official.ItemMerge;
using R = ItemMergeOracle;
using O = TreasureOracle;

public static partial class TreasureCultivationValidation
{
    private sealed class MergeStock : M.IItemMergeInventory
    {
        internal readonly Dictionary<int, long> Counts = new Dictionary<int, long>();
        public long GetItemAmount(int id) => Counts.TryGetValue(id, out long value) ? value : 0;
    }

    private static void CheckItemMerge()
    {
        string text = File.ReadAllText(Path.Combine(Work, "OriginalItemMergeRules.json"));
        var data = JObject.Parse(text);
        var localRows = data["Recipes"].ToObject<M.ItemMergeInfo[]>();
        var localNumbers = data["Levels"].ToObject<M.ItemMergeLevelNumber[]>();
        var types = data["ItemTypes"].ToDictionary(row => (int)row["ClassId"], row => (int)row["ItemType"]);
        var localLevels = new M.ItemMergeLevelNumbers(localNumbers, _ => { });
        long maximum = (long)data["MaxMergeAmountAtATime"];
        var local = new M.OfficialItemMergeRules(localRows, maximum, id => types[id], localLevels.GetNumber);
        var oracle = MergeOracle(data);
        var stock = new MergeStock();
        var player = new R.Player { Read = stock.GetItemAmount };
        int[] ids = localRows.Select(row => row.ProductId)
            .Concat(localRows.SelectMany(row => new[] { row.Material1, row.Material2, row.Material3, row.Material4, row.Material5 }))
            .Where(id => id > 0).Distinct().ToArray();
        Check(localRows.All(row => row.Materials.Count == 0), "parser initialization leaves reusable raw table rows unchanged");
        foreach (int id in ids.Concat(new[] { -101 }))
        {
            foreach (long amount in new long[] { -1, 0, 1, 7, long.MaxValue / 4 })
                EqualCosts(local.GetMaterials(id, amount), oracle.GetMaterials(id, amount), "ordered direct merge materials " + id);
            Check(local.IsBaseMaterial(id) == oracle.IsBaseMaterial(id), "base material lookup " + id);
            var li = local.GetItemMergeInfosByMaterialId(id)?.Select(row => row.ProductId).ToArray();
            var ri = oracle.GetItemMergeInfosByMaterialId(id)?.Select(row => row.ProductId).ToArray();
            Check((li == null && ri == null) || (li != null && ri != null && li.SequenceEqual(ri)), "reverse material index order " + id);
            foreach (bool recursion in new[] { false, true })
            {
                var left = new HashSet<int> { -3 }; var right = new HashSet<int> { -3 };
                local.GetMaterialItemIds(id, left, recursion); oracle.GetMaterialItemIds(id, right, recursion);
                Check(left.SetEquals(right), "recursive material identity collection " + id);
            }
        }
        foreach (int type in types.Values.Distinct())
        {
            Check(local.GetItemMergeInfosByType(type).Select(row => row.ProductId).SequenceEqual(
                oracle.GetItemMergeInfosByType((R.ItemType)type).Select(row => row.ProductId)), "product type index order " + type);
            Check(local.GetItemMergeInfosByTypeList(new List<int> { type, type }).Select(row => row.ProductId).SequenceEqual(
                oracle.GetItemMergeInfosByTypeList(new List<R.ItemType> { (R.ItemType)type, (R.ItemType)type }).Select(row => row.ProductId)), "requested type order and duplicates " + type);
        }
        foreach (long budget in new long[] { 0, 1, 1000000, long.MaxValue / 4 })
        {
            foreach (int id in ids) stock.Counts[id] = budget;
            foreach (int id in ids)
            {
                foreach (bool recursive in new[] { false, true })
                {
                    Check(local.CalcCanMergeAmout(stock, id, recursive) == oracle.CalcCanMergeAmout(player, id, recursive), "recursive capacity and cap " + id);
                    Check(local.CanMerge(stock, id, recursive) == oracle.CanMerge(player, id, recursive), "merge availability " + id);
                }
                CompareMergeCost(local, oracle, stock, player, new Dictionary<int, long> { { id, 7 } }, null, null);
                Dictionary<int, long> left = null, right = null;
                local.CalcMergeItemCostBaseMaterials(id, 7, ref left); oracle.CalcMergeItemCostBaseMaterials(id, 7, ref right);
                EqualCosts(left, right, "base-material expansion " + id);
                left = right = null;
                local.CalcPlayerMergeItemCost(stock, id, 7, ref left); oracle.CalcPlayerMergeItemCost(player, id, 7, ref right);
                EqualCosts(left, right, "player-aware expansion " + id);
            }
        }
        foreach (var row in localRows)
            foreach (int level in new[] { -10, 0, 1, 40, int.MaxValue })
                Check(BitConverter.SingleToInt32Bits(local.GetMergeCountFactor(level, row.ProductId)) ==
                    BitConverter.SingleToInt32Bits(oracle.GetMergeCountFactor(level, row.ProductId)), "original product count factor " + row.ProductId);
        foreach (var row in localNumbers)
        {
            foreach (int level in new[] { -1, 0, 1, row.Nums.Count, int.MaxValue })
                Check(localLevels.GetNumberDouble(row.Id, level) == oracle.ConfigManager.Levels.GetNumberDouble(row.Id, level), "level-number endpoint lookup");
            var values = localLevels.GetNumbers(row.Id); values[0] = double.MaxValue;
            Check(localLevels.GetNumbers(row.Id)[0] == row.Nums[0], "level-number list returns a detached copy");
            foreach (float level in new[] { -0.5f, 0.5f, 1.75f, 1000f })
                Check(localLevels.GetNumberLerp(row.Id, level) == oracle.ConfigManager.Levels.GetNumberLerp(row.Id, level), "original level factor interpolation");
        }
        int missing = 0;
        var missingLevels = new M.ItemMergeLevelNumbers(Array.Empty<M.ItemMergeLevelNumber>(), _ => missing++);
        Check(missingLevels.GetNumber(7, 1) == 0 && missingLevels.GetNumbers(7) == null && missing == 2, "missing original level rows report errors and retain original return values");
        CheckSyntheticMergeGraph();
        CheckSavedTreasureMerge(local, oracle);

        var owningCatalog = OfflineItemMergeCatalog.LoadDefault();
        var first = owningCatalog.CreateRules(); var second = owningCatalog.CreateRules();
        var change = localRows.First(row => first.Get(row.ProductId).Materials.Count > 0);
        int material = first.Get(change.ProductId).Materials.Keys.First();
        int expected = second.Get(change.ProductId).Materials[material];
        first.Get(change.ProductId).Materials[material] = -123;
        Check(second.Get(change.ProductId).Materials[material] == expected, "separate parser instances own separate processed material dictionaries");
    }

    private static R.OriginalItemMergeParser MergeOracle(JObject data)
    {
        var result = new R.OriginalItemMergeParser();
        R.Parsers.GameSettings.MaxMergeAmountAtATime = (long)data["MaxMergeAmountAtATime"];
        result.ConfigManager = new R.ConfigManager {
            Items = new R.ItemInfoParser { Types = data["ItemTypes"].ToDictionary(row => (int)row["ClassId"], row => (R.ItemType)(int)row["ItemType"]) },
            Levels = new R.LevelNumberParser { Rows = data["Levels"].ToObject<R.LevelNumber[]>().ToDictionary(row => row.Id) }
        };
        foreach (var row in data["Recipes"].ToObject<R.ItemMergeInfo[]>()) result.configs.Add(row.ProductId, row);
        result.PostParse();
        return result;
    }
    private static void EqualCosts(Dictionary<int, long> left, Dictionary<int, long> right, string name)
        => Check(left == null && right == null || left != null && right != null && CostText(left) == CostText(right), name);
    private static void CompareMergeCost(M.OfficialItemMergeRules local, R.OriginalItemMergeParser oracle, MergeStock stock, R.Player player,
        Dictionary<int, long> need, Dictionary<int, long> initial, Dictionary<int, long> consumed)
    {
        var left = initial == null ? null : new Dictionary<int, long>(initial);
        var right = initial == null ? null : new Dictionary<int, long>(initial);
        var lc = consumed == null ? null : new Dictionary<int, long>(consumed);
        var rc = consumed == null ? null : new Dictionary<int, long>(consumed);
        var before = CostText(need);
        var lr = local.CalcMergeCost(stock, ref left, need, lc); var rr = oracle.CalcMergeCost(player, ref right, need, rc);
        EqualCosts(left, right, "shared inventory consumption and exact cost order");
        EqualCosts(lc, rc, "shared consumed dictionary mutations");
        Check(ReferenceEquals(lr, left) && ReferenceEquals(rr, right) && CostText(need) == before, "cost ref replacement and requested dictionary ownership");
    }
    private static void CheckSyntheticMergeGraph()
    {
        var data = JObject.Parse(@"{'MaxMergeAmountAtATime':17,'Recipes':[
            {'ProductId':100,'Count':3,'Material1':101,'Count1':2,'Material2':102,'Count2':1,'LevelFactorId':7},
            {'ProductId':101,'Count':1,'Material1':103,'Count1':2},
            {'ProductId':102,'Count':1,'Material1':103,'Count1':1,'Material2':104,'Count2':1},
            {'ProductId':105,'Count':5}],
            'ItemTypes':[{'ClassId':100,'ItemType':1},{'ClassId':101,'ItemType':2},{'ClassId':102,'ItemType':2},{'ClassId':105,'ItemType':1}],
            'Levels':[{'Id':7,'Nums':[0.1,0.25,0.7]}]}");
        var levels = new M.ItemMergeLevelNumbers(data["Levels"].ToObject<M.ItemMergeLevelNumber[]>(), _ => { });
        var types = data["ItemTypes"].ToDictionary(row => (int)row["ClassId"], row => (int)row["ItemType"]);
        var local = new M.OfficialItemMergeRules(data["Recipes"].ToObject<M.ItemMergeInfo[]>(), 17, id => types[id], levels.GetNumber);
        var oracle = MergeOracle(data);
        var stock = new MergeStock(); stock.Counts[101] = 1; stock.Counts[103] = 4; stock.Counts[104] = 5;
        var player = new R.Player { Read = stock.GetItemAmount };
        var need = new Dictionary<int, long> { {100,2}, {101,2}, {103,1}, {105,3}, {109,0}, {110,-7} };
        foreach (var request in new[] { need, need.Reverse().ToDictionary(row => row.Key, row => row.Value) })
            foreach (var prior in new[] { null, new Dictionary<int, long> {{103,1},{104,2}} })
                CompareMergeCost(local, oracle, stock, player, request, new Dictionary<int, long> {{104,3}}, prior);
        Check(local.CalcCanMergeAmout(stock,105,true) == 17 && !local.IsBaseMaterial(105), "empty recipe keeps the original cap and recipe identity");
        Dictionary<int, long> left = null, right = null;
        local.CalcMergeItemCostBaseMaterials(105,3,ref left); oracle.CalcMergeItemCostBaseMaterials(105,3,ref right);
        EqualCosts(left,right,"empty recipe remains a terminal material in base expansion");
        left=right=null; local.CalcPlayerMergeItemCost(stock,100,0,ref left); oracle.CalcPlayerMergeItemCost(player,100,0,ref right);
        Check(left==null && right==null,"nonpositive player-aware cost requests preserve null dictionaries");
        foreach(int level in new[]{-1,0,1,2,3,1000}) Check(local.GetMergeCountFactor(level,100)==oracle.GetMergeCountFactor(level,100),"floating product count factors retain original float conversion order");
        var detachedNumbers=levels.GetNumbers(7); detachedNumbers[0]=100;
        Check(levels.GetNumbers(7)[0]==0.1,"nonempty level-number lists are detached from parser state");
        foreach(float level in new[]{-0.5f,0f,0.5f,1.75f,1000f})
            Check(levels.GetNumberLerp(7,level)==oracle.ConfigManager.Levels.GetNumberLerp(7,level),"level interpolation retains original truncation and endpoint behavior");
        bool cycle=false; data["Recipes"][1]["Material1"]=100;
        try { OfflineItemMergeCatalog.Parse(data.ToString()); } catch(FormatException) { cycle=true; }
        Check(cycle,"invalid cyclic local payload is rejected before calling original recursive methods");
    }
    private static void CheckSavedTreasureMerge(M.OfficialItemMergeRules local, R.OriginalItemMergeParser oracle)
    {
        // Restore the actual table's cap after the independent synthetic graph.
        R.Parsers.GameSettings.MaxMergeAmountAtATime = (long)JObject.Parse(File.ReadAllText(Path.Combine(Work,"OriginalItemMergeRules.json")))["MaxMergeAmountAtATime"];
        var live = new OfflineTreasureService(profile,catalog,ReadItem);
        var selected = (from info in catalog.Treasures where !info.LimitUseCommonMaterial
            from star in Enumerable.Range(0,info.StarMax)
            let next = live.GetUpgrade(items[info.ClassId].Quality,star+1)
            where next != null && next.Materials.Keys.Any(material => local.Get(material)!=null)
            select new { Info=info, Star=star }).First();
        int id=selected.Info.ClassId;
        Dictionary<int,long> required=null;
        Seed(id,selected.Star,true,true,1000000);
        Check(live.GetTreasureNextLevelUpgradeMaterial(id,false,false,out required)==0,"real automatic-merge quote uses original next-star requirements");
        Dictionary<int,long> bases=null;
        foreach(var cost in required) oracle.CalcMergeItemCostBaseMaterials(cost.Key,cost.Value,ref bases);
        var initial=new Dictionary<int,long>(bases); initial[id]=1;
        profile.Reload();
        Check(profile.ApplyCharacterMutation(p=>{
            p.Inventory=initial.Select(row=>new OfflineItemStack{ItemId=row.Key,Count=row.Value}).ToList();
            p.CharacterProperties.Treasures.Clear();
            p.CharacterProperties.Treasures.Add(new OfflineCharacterTreasure{ClassId=id,Active=true,Star=selected.Star,HitCount=selected.Info.InitialHitCount});
        }).IsApplied,"persist only the required base materials in an isolated real profile");
        original.Items.Clear();
        foreach(var stack in profile.Save.Player.Inventory) original.Items.Add(stack.ItemId,new O.PlayerItemDataWrap{
            Id=stack.ItemId,Count=stack.Count,Info=O.Parsers.Instance.ItemInfoParser.Get(stack.ItemId),
            State=stack.ItemId==id?new O.PlayerItemParamTreasureWrap{Active=true,Star=selected.Star}:null});
        O.Parsers.Instance.ItemMergeInfoParser.Calculate=(O.Player p,ref Dictionary<int,long> result,Dictionary<int,long> request)
            =>oracle.CalcMergeCost(new R.Player{Read=p.GetItemAmount},ref result,request);
        int expected=O.OriginalTreasureHelper.CanTreasureUpgrade(id,new Dictionary<int,long>(required),original,true,true);
        Check(expected==0 && live.CanTreasureUpgrade(id,new Dictionary<int,long>(required),true,true)==0,"actual default owner makes a saved base-material-only star-up eligible");
        Check(live.CanTreasureUpgrade(id,new Dictionary<int,long>(required),false,true)==1188,"same inventory remains insufficient when automatic merging is disabled");
        var originalCosts=O.OriginalTreasureHelper.GetTreasureUpgradeMaterial(new Dictionary<int,long>(required),true,original);
        var localCosts=live.Checks.GetTreasureUpgradeMaterial(new Dictionary<int,long>(required),true,live);
        EqualCosts(localCosts,originalCosts,"formal saved treasure binding uses the actual recursive material expansion");
        string saved=JsonConvert.SerializeObject(profile.Save);
        Check(live.CanTreasureUpgrade(id,new Dictionary<int,long>(required),true,true)==0 && JsonConvert.SerializeObject(profile.Save)==saved,"actual automatic-merge checks never consume saved material quantities");
    }
}
