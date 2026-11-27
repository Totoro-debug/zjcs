// Original player treasure tip branches; explicit local queries.
using System;
using System.Collections.Generic;
using System.Linq;
using ElementType=Zjcs.Official.TreasureSlots.ElementType;
namespace Zjcs.Official.TreasureCultivation {


public enum TreasureRedDotEnum
{
	Active = 0,
	Merge = 1,
	LevelUp = 2,
	Upgrade = 3,
	Wakeup = 4,
	None = 1000
}

public sealed class OfficialTreasureTipRules {
private readonly ITreasureTipContext context;
public OfficialTreasureTipRules(ITreasureTipContext context) { this.context=context??throw new ArgumentNullException(nameof(context)); }
public bool TreasureRedDot(ElementType elementType, List<TreasureRedDotEnum> treasureRedDots, bool autoMerge, bool autoMergeUpgrade, bool canUseCommonMaterial = true)
	{
		if (treasureRedDots.Contains(TreasureRedDotEnum.Merge) && TreasurePieceRedDot(elementType))
		{
			return true;
		}
		IReadOnlyCollection<TreasureItem> readOnlyCollection = context.FindItemsByType((ItemType)305);
		if (readOnlyCollection == null)
		{
			return false;
		}
		foreach (TreasureItem item in readOnlyCollection)
		{
			if ((elementType == ElementType.None || elementType == context.GetElement(item.Info.ClassId)) && TreasureRedDot(item, treasureRedDots, out var _, autoMerge, autoMergeUpgrade, canUseCommonMaterial))
			{
				return true;
			}
		}
		return false;
	}
public bool TreasureRedDot(TreasureItem itemData, List<TreasureRedDotEnum> treasureRedDots, out TreasureRedDotEnum tipType, bool autoMerge, bool autoMergeAwake, bool canUseCommonMaterial = true)
	{
		tipType = TreasureRedDotEnum.None;
		if (!itemData.GetItemParamTreasure().Active && treasureRedDots.Contains(TreasureRedDotEnum.Active) && context.CanActivateTreasure(itemData.Info.ClassId) == 0)
		{
			tipType = TreasureRedDotEnum.Active;
			return true;
		}
		if (treasureRedDots.Contains(TreasureRedDotEnum.LevelUp))
		{
			bool autoMergeMaterial = autoMerge;
			int treasureInSlotIndex = context.GetTreasureInSlotIndex(itemData.Info.ClassId);
			if (context.CanTreasureSlotLevelUp(context.GetElement(itemData.Info.ClassId), treasureInSlotIndex, autoMergeMaterial) == 0)
			{
				tipType = TreasureRedDotEnum.LevelUp;
				return true;
			}
		}
		if (treasureRedDots.Contains(TreasureRedDotEnum.Wakeup) && TreasureCanWakeup(itemData, autoMergeAwake))
		{
			tipType = TreasureRedDotEnum.Wakeup;
			return true;
		}
		if (treasureRedDots.Contains(TreasureRedDotEnum.Upgrade) && TreasureCanUpgrade(itemData, autoMergeUpgrade: true, canUseCommonMaterial))
		{
			tipType = TreasureRedDotEnum.Upgrade;
			return true;
		}
		return false;
	}
private bool TreasureCheckCanUpgrade(TreasureItem itemData, bool autoMergeUpgrade, bool canUseCommonMaterial = true)
	{

		context.GetTreasureNextLevelUpgradeMaterial(itemData.Id, hasEmptyItem: true, IsTreasureCanUpgradeUseCommonMaterial(itemData.Id) & canUseCommonMaterial, out var materials);
		return context.CanTreasureUpgrade(itemData.Info.ClassId, materials, autoMergeUpgrade, canUseCommonMaterial) == 0;
	}
private bool TreasureCheckCanAwake(TreasureItem itemData, bool autoMergeOnAwake, bool canUseCommonMaterial = true)
	{

		context.GetTreasureNextLevelUpgradeMaterial(itemData.Id, hasEmptyItem: true, useCommonItem: false, out var materials);
		return context.CanTreasureUpgrade(itemData.Info.ClassId, materials, autoMergeOnAwake, canUseCommonMaterial) == 0;
	}
public bool TreasureCanUpgrade(TreasureItem itemData, bool autoMergeUpgrade, bool canUseCommonMaterial = true)
	{
		TreasureInfo treasureInfo = itemData.Info.PartInfo as TreasureInfo;
		if (itemData.GetItemParamTreasure().Star < treasureInfo.StarMax - 1)
		{
			return TreasureCheckCanUpgrade(itemData, autoMergeUpgrade, canUseCommonMaterial);
		}
		return false;
	}
public bool TreasureCanUpgrade(TreasureItem itemData, bool autoMergeUpgrade)
	{
		return TreasureCheckCanUpgrade(itemData, autoMergeUpgrade);
	}
public bool TreasureCanWakeup(TreasureItem itemData, bool autoMergeUpgrade)
	{
		TreasureInfo treasureInfo = itemData.Info.PartInfo as TreasureInfo;
		if (itemData.GetItemParamTreasure().Star == treasureInfo.StarMax - 1)
		{
			return TreasureCheckCanAwake(itemData, autoMergeUpgrade);
		}
		return false;
	}
public bool TreasureSummaryRedDot(List<TreasureRedDotEnum> treasureRedDots, bool autoMerge, bool autoMergeUpgrade)
	{
		IReadOnlyCollection<TreasureItem> readOnlyCollection = context.FindItemsByType((ItemType)305);
		if (readOnlyCollection == null)
		{
			return false;
		}
		foreach (TreasureItem item in readOnlyCollection)
		{
			if (context.GetSuitClassIdByTreasure(item.Info.ClassId) > 0 && TreasureRedDot(item, treasureRedDots, out var _, autoMerge, autoMergeUpgrade))
			{
				return true;
			}
		}
		return false;
	}
public bool TreasurePieceRedDot(ElementType elementType)
	{
		foreach (TreasureItem item in context.FindItemsByType(ItemType.TreasurePiece))
		{
			int treasureIdByPieceId = context.GetTreasureIdByPieceId(item.Info.ClassId);
			TreasureTipItemInfo itemInfo = context.GetItemInfo(treasureIdByPieceId);
			if ((elementType == ElementType.None || elementType == itemInfo.ElementType) && !context.HasItem(treasureIdByPieceId, 1L) && context.CanMergeTreasure(treasureIdByPieceId) == 0)
			{
				return true;
			}
		}
		return false;
	}
public bool AnyTreasureRedDot(List<TreasureRedDotEnum> treasureRedDots, bool autoMerge, bool autoMergeUpgrade)
	{
		foreach (TreasureItem item in context.FindItemsByType((ItemType)305))
		{
			if (TreasureRedDot(item, treasureRedDots, out var _, autoMerge, autoMergeUpgrade))
			{
				return true;
			}
		}
		if (treasureRedDots.Contains(TreasureRedDotEnum.Merge) && TreasurePieceRedDot(ElementType.None))
		{
			return true;
		}
		return false;
	}
private bool IsTreasureCanUpgradeUseCommonMaterial(int treasureId)
	{
		if (!context.GetTreasure(treasureId).LimitUseCommonMaterial)
		{
			return context.UpgradeUseCommonMaterailEnable;
		}
		return false;
	}
}}
