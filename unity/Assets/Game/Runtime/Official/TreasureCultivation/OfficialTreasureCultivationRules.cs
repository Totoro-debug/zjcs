// Original TreasureHelper promotion methods; explicit player/parser ports.
using System;
using System.Collections.Generic;
using System.Linq;
namespace Zjcs.Official.TreasureCultivation {
public sealed class OfficialTreasureCultivationRules {
private readonly ITreasureCultivationTables tables;
public OfficialTreasureCultivationRules(ITreasureCultivationTables tables) { this.tables = tables ?? throw new ArgumentNullException(nameof(tables)); }
public int CanActivateTreasure(int treasureId, ITreasureCultivationPlayer player)
	{
		TreasureItem itemById = player.GetItemById(treasureId);
		if (itemById == null)
		{
			return 1189;
		}
		TreasureState itemParamTreasure = itemById.GetItemParamTreasure();
		if (itemParamTreasure == null)
		{
			return 1324;
		}
		if (itemParamTreasure.Active)
		{
			return 1224;
		}
		Dictionary<int, long> materials = tables.GetUpgrade(itemById.Info.Quality, itemParamTreasure.Star).Materials;
		if (!player.HasItems(materials))
		{
			return 1188;
		}
		return 0;
	}

public int CanMergeTreasure(int treasureId, ITreasureCultivationPlayer player)
	{
		TreasureInfo treasureInfo = tables.GetTreasure(treasureId);
		ItemInfo itemInfo = tables.GetItemInfo(treasureId);
		if (treasureInfo == null || itemInfo == null)
		{
			return 1324;
		}
		if (player.HasItem(treasureId, 1L))
		{
			return 1187;
		}
		Dictionary<int, long> mergeMaterials = tables.GetMergeMaterials(treasureId);
		if (!player.HasItems(mergeMaterials))
		{
			return 1188;
		}
		return 0;
	}

public bool PieceCanMerge(int treasurePieceClassId, ITreasureCultivationPlayer player)
	{
		TreasureItem itemById = player.GetItemById(treasurePieceClassId);
		if (itemById == null)
		{
			return false;
		}
		int treasureIdByPieceId = tables.GetTreasureIdByPieceId(treasurePieceClassId);
		TreasureInfo treasureInfo = tables.GetTreasure(treasureIdByPieceId);
		ItemInfo itemInfo = tables.GetItemInfo(treasureIdByPieceId);
		if (treasureInfo == null || itemInfo == null)
		{
			return false;
		}
		if (player.HasItem(treasureIdByPieceId, 1L))
		{
			return false;
		}
		TreasureUpgradeInfo treasureUpgradeInfo = tables.GetUpgrade(itemInfo.Quality, 0);
		return itemById.Count >= treasureUpgradeInfo.PieceCount;
	}

public Dictionary<int, long> GetTreasureUpgradeMaterial(Dictionary<int, long> materials, bool autoMergeMaterial, ITreasureCultivationPlayer player)
	{
		Dictionary<int, long> costs;
		if (autoMergeMaterial)
		{

			costs = new Dictionary<int, long>(materials.Count);
			tables.CalcMergeCost(player, ref costs, materials);
		}
		else
		{
			costs = materials;
		}
		foreach (int item in costs.Keys.ToList())
		{
			if (costs[item] <= 0)
			{
				costs.Remove(item);
			}
		}
		return costs;
	}

public int GetTreasureNextLevelUpgradeMaterial(int treasureId, ITreasureCultivationPlayer player, bool hasEmptyItem, bool useCommonItem, out Dictionary<int, long> materials)
	{
		materials = new Dictionary<int, long>();
		TreasureInfo treasureInfo = tables.GetTreasure(treasureId);
		ItemInfo itemInfo = tables.GetItemInfo(treasureId);
		if (treasureInfo == null || itemInfo == null)
		{
			return 1325;
		}
		if (!player.HasItem(treasureId, 1L))
		{
			return 1324;
		}
		TreasureItem itemById = player.GetItemById(treasureId);
		TreasureState itemParamTreasure = itemById.GetItemParamTreasure();
		if (itemParamTreasure == null)
		{
			return 1324;
		}
		if (!itemParamTreasure.Active)
		{
			return 1223;
		}
		int star = itemParamTreasure.Star;
		if (star >= treasureInfo.StarMax)
		{
			return 1190;
		}
		if (treasureInfo.LimitUseCommonMaterial)
		{
			materials = tables.GetSpecialUpgradeMaterials(itemById.Id, itemById.GetItemParamTreasure().Star, itemById.Info.Quality);
			return 0;
		}
		Dictionary<int, long> upgradeMaterials = tables.GetUpgradeMaterials(itemById);
		int pieceIdByTreasureId = tables.GetPieceIdByTreasureId(treasureId);
		upgradeMaterials.TryGetValue(pieceIdByTreasureId, out var value);
		if (!useCommonItem)
		{
			if ((value > 0) | hasEmptyItem)
			{
				materials.Add(pieceIdByTreasureId, value);
			}
		}
		else
		{
			long itemAmount = player.GetItemAmount(pieceIdByTreasureId);
			long num = Math.Min(value, itemAmount);
			if ((num != 0) | hasEmptyItem)
			{
				materials.Add(pieceIdByTreasureId, num);
			}
			long num2 = value - num;
			if (num2 > 0)
			{
				if (!tables.TryGetCommonMaterial(itemById.Info.Quality, out var value2))
				{
					return 1324;
				}
				materials.Add(value2.ItemClassId, value2.Item2PieceCount * num2);
			}
		}
		foreach (KeyValuePair<int, long> item in tables.GetStarUpNonPiece(treasureId, star))
		{
			if (materials.TryGetValue(item.Key, out var value3))
			{
				materials[item.Key] = value3 + item.Value;
			}
			else
			{
				materials.Add(item.Key, item.Value);
			}
		}
		return 0;
	}

public int CanTreasureUpgrade(int treasureId, Dictionary<int, long> materials, ITreasureCultivationPlayer player, bool autoMergeMaterial = false, bool canUseCommonMaterial = true)
	{
		TreasureInfo treasureInfo = tables.GetTreasure(treasureId);
		ItemInfo itemInfo = tables.GetItemInfo(treasureId);
		if (treasureInfo == null || itemInfo == null)
		{
			return 1325;
		}
		if (!player.HasItem(treasureId, 1L))
		{
			return 1189;
		}
		TreasureItem itemById = player.GetItemById(treasureId);
		if (itemById == null)
		{
			return 1324;
		}
		TreasureState itemParamTreasure = itemById.GetItemParamTreasure();
		if (itemParamTreasure == null)
		{
			return 1324;
		}
		if (!itemParamTreasure.Active)
		{
			return 1223;
		}
		int star = itemParamTreasure.Star;
		if (star >= treasureInfo.StarMax)
		{
			return 1190;
		}
		_ = treasureInfo.UpgradeMaxStar;
		Dictionary<int, long> treasureUpgradeMaterial = GetTreasureUpgradeMaterial(materials, autoMergeMaterial, player);
		if (!player.HasItems(treasureUpgradeMaterial))
		{
			return 1188;
		}
		if (!tables.TryGetCommonMaterial(itemById.Info.Quality, out var value))
		{
			return 1324;
		}
		int pieceIdByTreasureId = tables.GetPieceIdByTreasureId(treasureId);
		if (treasureInfo.LimitUseCommonMaterial)
		{
			foreach (KeyValuePair<int, long> specialUpgradeMaterial in tables.GetSpecialUpgradeMaterials(itemById.Id, itemById.GetItemParamTreasure().Star, itemById.Info.Quality))
			{
				if (!treasureUpgradeMaterial.TryGetValue(specialUpgradeMaterial.Key, out var value2))
				{
					return 1188;
				}
				if (specialUpgradeMaterial.Value > value2)
				{
					return 1188;
				}
			}
			return 0;
		}
		long num = 0L;
		if ((!treasureInfo.LimitUseCommonMaterial & canUseCommonMaterial) && materials.TryGetValue(value.ItemClassId, out var value3))
		{
			num = value3 / value.Item2PieceCount;
		}
		tables.GetUpgradeMaterials(itemById).TryGetValue(pieceIdByTreasureId, out var value4);
		if (treasureUpgradeMaterial.TryGetValue(pieceIdByTreasureId, out var value5) && value5 + num < value4)
		{
			return 1188;
		}
		foreach (KeyValuePair<int, long> item in GetTreasureUpgradeMaterial(tables.GetStarUpNonPiece(treasureId, star).ToDictionary((KeyValuePair<int, long> pair) => pair.Key, (KeyValuePair<int, long> pair) => pair.Value), autoMergeMaterial, player))
		{
			if (!treasureUpgradeMaterial.TryGetValue(item.Key, out var value6) || value6 < item.Value)
			{
				return 1188;
			}
		}
		return 0;
	}

public bool IsItemEffectTreasurePromote(TreasureItem itemData)
	{
		if (itemData.Info.ItemType == ItemType.TreasurePiece)
		{
			return true;
		}
		if (itemData.Info.ItemType == ItemType.CommonTreasureMaterial)
		{
			return true;
		}
		if (tables.IsUpgradeMaterial(itemData.Id))
		{
			return true;
		}
		if (tables.IsLevelMaterial(itemData.Id))
		{
			return true;
		}
		return false;
	}
}}
