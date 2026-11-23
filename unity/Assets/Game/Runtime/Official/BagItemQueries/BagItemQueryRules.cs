// Original Bag item eligibility/count queries; only explicit owner boundaries differ.
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.BagTipDots;
namespace Zjcs.Official.BagItemQueries
{
public sealed class BagItemQueryRules
{
    readonly IBagItemQueryServices Services;
    public BagItemQueryRules(IBagItemQueryServices services) => Services = services ?? throw new ArgumentNullException(nameof(services));
public bool ShowBagCanUseItemRedDot(PlayerItemDataWrap itemData)
	{
		if (itemData.Count <= 0)
		{
			return false;
		}
		ItemType itemType = itemData.Info.ItemType;
		if (!Services.Settings.ShowCanUseRedDotItemTypes.Contains(itemType))
		{
			return false;
		}
		return itemType switch
		{
			ItemType.CommonEquipPiece => CanMergeEquipPiece(itemData.Id),
			ItemType.CommonItemPiece => CanMergeItemPiece(itemData.Id),
			ItemType.RandomTreasurePiece => CanMergeRandomTreasurePiece(itemData.Id),
			ItemType.PetPiece => CanMergePetPiece(itemData.Id),
			ItemType.PeriodCard => CanUsePeriodCardItem(itemData.Info.PartInfo as PeriodCardItemInfo),
			ItemType.CookingFormula => CanUseCookingFormula(itemData.Id),
			_ => true,
		};
	}

public long GetCanUseBlueDotCount(PlayerItemDataWrap itemData)
	{
		int num = 1;
		ItemType itemType = itemData.Info.ItemType;
		int classId = itemData.Info.ClassId;
		if (!Services.Settings.ShowCanUseRedDotItemTypes.Contains(itemType) || itemData.Info.RedDotType != RedDotType.BlueDot)
		{
			return 0L;
		}
		switch (itemType)
		{
		case ItemType.CommonEquipPiece:
			num = Services.Tables.EquipPieceMergeInfoParser.Get(classId).PieceMergeCount;
			break;
		case ItemType.CommonItemPiece:
			num = Services.Tables.ItemPieceMergeInfoParser.Get(classId).PieceMergeCount;
			break;
		case ItemType.PetPiece:
			num = Services.Tables.PetInfoParser.GetByPieceClassId(classId).PieceMergeCount;
			break;
		case ItemType.RandomTreasurePiece:
			num = Services.Tables.RandomTreasureExchangeInfoParser.Get(classId).RequireCount;
			break;
		}
		return itemData.Count / num;
	}

public bool CanMergeEquipPiece(int itemId)
	{
		EquipPieceMergeInfo equipPieceMergeInfo = Services.Tables.EquipPieceMergeInfoParser.Get(itemId);
		if (equipPieceMergeInfo == null)
		{
			return false;
		}
		if (!Services.Player.HasItem(itemId, equipPieceMergeInfo.PieceMergeCount))
		{
			return false;
		}
		return true;
	}

public bool CanMergeItemPiece(int itemId)
	{
		ItemPieceMergeInfo itemPieceMergeInfo = Services.Tables.ItemPieceMergeInfoParser.Get(itemId);
		if (itemPieceMergeInfo == null)
		{
			return false;
		}
		if (!Services.Player.HasItem(itemId, itemPieceMergeInfo.PieceMergeCount))
		{
			return false;
		}
		return true;
	}

public bool CanMergePetPiece(int itemId)
	{
		PetInfo byPieceClassId = Services.Tables.PetInfoParser.GetByPieceClassId(itemId);
		if (byPieceClassId == null)
		{
			return false;
		}
		if (Services.Player.HasPet(byPieceClassId.ClassId))
		{
			return false;
		}
		if (!Services.Player.HasItem(itemId, byPieceClassId.PieceMergeCount))
		{
			return false;
		}
		return true;
	}

public bool CanMergeRandomTreasurePiece(int itemId)
	{
		RandomTreasureExchangeInfo randomTreasureExchangeInfo = Services.Tables.RandomTreasureExchangeInfoParser.Get(itemId);
		if (randomTreasureExchangeInfo == null)
		{
			return false;
		}
		return Services.Player.HasItem(itemId, randomTreasureExchangeInfo.RequireCount);
	}

public bool CanUsePeriodCardItem(PeriodCardItemInfo info)
	{
		if (info == null)
		{
			return false;
		}
		int periodCardId = info.PeriodCardId;
		if (Services.Tables.PeriodicCardPayInfoParser.Get(periodCardId) == null)
		{
			return false;
		}
		if (!Services.Player.TryGetBuyCount(PayType.PeriodicCard, periodCardId, Services.GetDateTime(), out var count))
		{
			return false;
		}
		if (count > Services.Settings.Pay.PeriodicCardMaxDurationDay)
		{
			return false;
		}
		return true;
	}

public bool CanUseCookingFormula(int itemId)
	{
		if (!Services.Tables.CookingInfoParser.Formula2CookingLookup.TryGetValue(itemId, out var value))
		{
			return false;
		}
		if (!Services.Tables.CookingInfoParser.TryGet(value, out var config))
		{
			return false;
		}
		if (!Services.IsLimit(config))
		{
			return false;
		}
		return true;
	}

public bool CanUseEquipReplaceItem(PlayerItemDataWrap itemData)
	{
		EquipAttributeReplaceItemInfo equipAttributeReplaceItemInfo = Services.Tables.EquipAttributeReplaceItemInfoParser.Get(itemData.Info.ClassId);
		foreach (PlayerItemDataWrap item in Services.Player.FindItemsByType(Services.Settings.EquipTypes))
		{
			if (!equipAttributeReplaceItemInfo.Quality.Contains(item.Info.Quality))
			{
				continue;
			}
			PlayerItemParamEquipWrap itemParamEquip = item.GetItemParamEquip();
			if (itemParamEquip.PurifyProps.Count > 0)
			{
				continue;
			}
			foreach (KeyValuePair<int, EquipPropBase> rawDatum in itemParamEquip.OtherProps.RawData)
			{
				if (rawDatum.Value.PropId == equipAttributeReplaceItemInfo.AttributePropId)
				{
					return true;
				}
			}
		}
		return false;
	}

public RedDotType GetItemRedDotType(PlayerItemDataWrap itemData)
	{
		if (itemData.Info.ItemType == ItemType.EquipAttributeReplaceItem && CanUseEquipReplaceItem(itemData))
		{
			return RedDotType.RedDot;
		}
		return itemData.Info.RedDotType;
	}
}
}
