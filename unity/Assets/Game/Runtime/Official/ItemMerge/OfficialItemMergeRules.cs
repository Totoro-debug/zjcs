// Original ItemMergeInfoParser methods; explicit inventory/configuration owners.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.ItemMerge {
public sealed partial class OfficialItemMergeRules {
private void PostParse()
	{

		foreach (KeyValuePair<int, ItemMergeInfo> config in configs)
		{
			ItemMergeInfo value = config.Value;
			int itemType = readItemType(value.ProductId);
			if (value.Material1 > 0 && value.Count1 > 0)
			{
				value.Materials.Add(value.Material1, value.Count1);
			}
			if (value.Material2 > 0 && value.Count2 > 0)
			{
				value.Materials.Add(value.Material2, value.Count2);
			}
			if (value.Material3 > 0 && value.Count3 > 0)
			{
				value.Materials.Add(value.Material3, value.Count3);
			}
			if (value.Material4 > 0 && value.Count4 > 0)
			{
				value.Materials.Add(value.Material4, value.Count4);
			}
			if (value.Material5 > 0 && value.Count5 > 0)
			{
				value.Materials.Add(value.Material5, value.Count5);
			}
			foreach (KeyValuePair<int, int> material in value.Materials)
			{
				if (!ItemMergeDict.TryGetValue(material.Key, out var value2))
				{
					value2 = new List<ItemMergeInfo>();
					ItemMergeDict[material.Key] = value2;
				}
				value2.Add(config.Value);
			}
			if (!ProductDict.TryGetValue(itemType, out var value3))
			{
				value3 = new List<ItemMergeInfo>();
				ProductDict.Add(itemType, value3);
			}
			value3.Add(config.Value);
		}
	}

public Dictionary<int, long> GetMaterials(int productId, long amount = 1L)
	{
		Dictionary<int, long> dictionary = null;
		ItemMergeInfo itemMergeInfo = Get(productId);
		if (itemMergeInfo != null)
		{
			dictionary = new Dictionary<int, long>();
			foreach (KeyValuePair<int, int> material in itemMergeInfo.Materials)
			{
				dictionary.Add(material.Key, material.Value * amount);
			}
		}
		return dictionary;
	}

public long CalcCanMergeAmout(IItemMergeInventory player, int itemId, bool recursion = false)
	{
		Dictionary<int, long> materials = GetMaterials(itemId, 1L);
		if (materials == null)
		{
			return 0L;
		}
		long num = maxMergeAmountAtATime;
		foreach (KeyValuePair<int, long> item in materials)
		{
			long num2 = player.GetItemAmount(item.Key);
			if (recursion)
			{
				num2 += CalcCanMergeAmout(player, item.Key, recursion);
			}
			num = Math.Min(num, num2 / item.Value);
		}
		return num;
	}

public Dictionary<int, long> CalcMergeCost(IItemMergeInventory player, ref Dictionary<int, long> costs, Dictionary<int, long> needCost, Dictionary<int, long> consumedItems = null)
	{
		costs = costs ?? new Dictionary<int, long>();
		consumedItems = consumedItems ?? new Dictionary<int, long>();
		foreach (KeyValuePair<int, long> item in needCost)
		{
			int key = item.Key;
			long num = item.Value;
			if (num <= 0)
			{
				continue;
			}
			long itemAmount = player.GetItemAmount(key);
			long num2 = (consumedItems.TryGetValue(key, out var value) ? value : 0);
			long num3 = Math.Max(0L, itemAmount - num2);
			if (num3 < num)
			{
				Dictionary<int, long> materials = GetMaterials(key, 1L);
				if (materials != null)
				{
					if (num3 > 0)
					{
						if (consumedItems.TryGetValue(key, out var value2))
						{
							consumedItems[key] = value2 + num3;
						}
						else
						{
							consumedItems[key] = num3;
						}
					}
					long num4 = num - num3;
					Dictionary<int, long> dictionary = new Dictionary<int, long>();
					foreach (KeyValuePair<int, long> item2 in materials)
					{
						dictionary[item2.Key] = item2.Value * num4;
					}
					CalcMergeCost(player, ref costs, dictionary, consumedItems);
					num = num3;
				}
			}
			if (num > 0)
			{
				if (consumedItems.TryGetValue(key, out var value3))
				{
					consumedItems[key] = value3 + num;
				}
				else
				{
					consumedItems[key] = num;
				}
				if (costs.TryGetValue(key, out var value4))
				{
					costs[key] = value4 + num;
				}
				else
				{
					costs[key] = num;
				}
			}
		}
		return costs;
	}

public void CalcMergeItemCostBaseMaterials(int itemId, long needCount, ref Dictionary<int, long> costs)
	{
		if (needCount <= 0)
		{
			return;
		}
		costs = costs ?? new Dictionary<int, long>();
		Dictionary<int, long> materials = GetMaterials(itemId, 1L);
		if (materials != null && materials.Count > 0)
		{
			foreach (KeyValuePair<int, long> item in materials)
			{
				CalcMergeItemCostBaseMaterials(item.Key, item.Value * needCount, ref costs);
			}
			return;
		}
		costs[itemId] = Existing(costs, itemId) + needCount;
	}

public void CalcPlayerMergeItemCost(IItemMergeInventory player, int itemId, long needCount, ref Dictionary<int, long> costs)
	{
		if (needCount > 0)
		{
			costs = costs ?? new Dictionary<int, long>();
			CalcPlayerMergeItemCostImpl(player, itemId, needCount, ref costs);
		}
	}

private void CalcPlayerMergeItemCostImpl(IItemMergeInventory player, int itemId, long needCount, ref Dictionary<int, long> costs)
	{
		Dictionary<int, long> materials = GetMaterials(itemId, 1L);
		if (materials != null && materials.Count > 0)
		{
			foreach (KeyValuePair<int, long> item in materials)
			{
				int key = item.Key;
				long num = item.Value * needCount;
				long itemAmount = player.GetItemAmount(key);
				long num2 = Math.Max(0L, num - itemAmount);
				if (itemAmount > 0)
				{
					long num3 = Math.Min(itemAmount, num);
					costs[key] = Existing(costs, key) + num3;
				}
				if (num2 > 0)
				{
					CalcPlayerMergeItemCostImpl(player, key, num2, ref costs);
				}
			}
			return;
		}
		costs[itemId] = Existing(costs, itemId) + needCount;
	}

public bool CanMerge(IItemMergeInventory player, int itemId, bool recursion = false)
	{
		return CalcCanMergeAmout(player, itemId, recursion) > 0;
	}

public bool IsBaseMaterial(int productId)
	{
		return Get(productId) == null;
	}

public float GetMergeCountFactor(int level, int itemId)
	{
		ItemMergeInfo itemMergeInfo = Get(itemId);
		int levelFactorId = itemMergeInfo.LevelFactorId;
		return ((levelFactorId > 0) ? levelNumber(levelFactorId, level) : 1f) * (float)itemMergeInfo.Count;
	}

public List<ItemMergeInfo> GetItemMergeInfosByMaterialId(int itemId)
	{
		ItemMergeDict.TryGetValue(itemId, out var value);
		return value;
	}

public List<ItemMergeInfo> GetItemMergeInfosByType(int itemType)
	{
		ProductDict.TryGetValue(itemType, out var value);
		return value;
	}

public List<ItemMergeInfo> GetItemMergeInfosByTypeList(List<int> itemTypes)
	{
		List<ItemMergeInfo> list = new List<ItemMergeInfo>();
		foreach (int itemType in itemTypes)
		{
			list.AddRange(GetItemMergeInfosByType(itemType));
		}
		return list;
	}

public void GetMaterialItemIds(int productId, HashSet<int> hashSet, bool recursion = false)
	{
		Dictionary<int, long> materials = GetMaterials(productId, 1L);
		if (materials == null)
		{
			return;
		}
		foreach (KeyValuePair<int, long> item in materials)
		{
			if (hashSet.Add(item.Key) && recursion)
			{
				GetMaterialItemIds(item.Key, hashSet, recursion);
			}
		}
	}
}
public sealed partial class ItemMergeLevelNumbers {
public float GetNumber(int id, int level)
	{
		return (float)GetNumberDouble(id, level);
	}

public double GetNumberDouble(int id, int level)
	{
		ItemMergeLevelNumber levelNumber = Get(id);
		if (levelNumber == null)
		{
			missing(id);
			return 0.0;
		}
		List<double> nums = levelNumber.Nums;
		if (level < 0)
		{
			return nums[0];
		}
		if (level >= nums.Count)
		{
			return nums[nums.Count - 1];
		}
		return nums[level];
	}

public List<double> GetNumbers(int id)
	{
		ItemMergeLevelNumber levelNumber = Get(id);
		if (levelNumber == null)
		{
			missing(id);
			return null;
		}
		return new List<double>(levelNumber.Nums);
	}

public float GetNumberLerp(int id, float level)
	{
		int num = (int)level;
		int level2 = num + 1;
		float num2 = level - (float)num;
		float number = GetNumber(id, num);
		float number2 = GetNumber(id, level2);
		return number + (number2 - number) * num2;
	}
}}
