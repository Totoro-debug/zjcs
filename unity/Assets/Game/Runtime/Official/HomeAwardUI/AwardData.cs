// Migrated from the selected official 884_156692 client/common source.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using UIParticle = Coffee.UIExtensions.UIParticle;
namespace Zjcs.Official.HomeAwardUI
{
public class AwardData : IMergeableItem, IComparable<AwardData>
{

	public IHomeAwardCatalog Catalog;

	public ulong Index;

	public int AwardId;

	public long Amount;

	public long OverflowCount;

	public int AgiotageId;

	public long Agiotage;

	public int RecycleItemId;

	public long RecycleAmount;

	public List<RecycleAwardData> RecycleAwards;

	public Dictionary<string, object> ParamDict;

	public DateTime GetDate;

	public object ExtraData;

	public AddItemRewardType AddItemType;

	public bool IsOutputAddition;

	public bool IsOnlyShow;

	public ShowAttachmentType ShowAttachmentType;

	public int CompareTo(AwardData other)
	{
		ItemInfo itemInfo = Catalog.GetItemInfo(AwardId);
		ItemInfo itemInfo2 = Catalog.GetItemInfo(other.AwardId);
		if (itemInfo.ItemType == ItemType.Gem && itemInfo2.ItemType == ItemType.Gem)
		{
			if (AddItemType != other.AddItemType)
			{
				return other.AddItemType - AddItemType;
			}
			if (itemInfo.Rank != itemInfo2.Rank)
			{
				return itemInfo2.Rank - itemInfo.Rank;
			}
			return (itemInfo.PartInfo as GemPartInfo).GemType - (itemInfo2.PartInfo as GemPartInfo).GemType;
		}
		int awardItemTypeScoreForSortAward = HomeAwardFormula.GetAwardItemTypeScoreForSortAward(itemInfo);
		int awardItemTypeScoreForSortAward2 = HomeAwardFormula.GetAwardItemTypeScoreForSortAward(itemInfo2);
		if (awardItemTypeScoreForSortAward == awardItemTypeScoreForSortAward2)
		{
			if (itemInfo.Quality == itemInfo2.Quality)
			{
				int num = (IsOutputAddition ? 1 : 0);
				int num2 = (other.IsOutputAddition ? 1 : 0);
				return num - num2;
			}
			if (itemInfo.Quality <= itemInfo2.Quality)
			{
				return 1;
			}
			return -1;
		}
		if (awardItemTypeScoreForSortAward <= awardItemTypeScoreForSortAward2)
		{
			return 1;
		}
		return -1;
	}

	public bool IsCanMerge()
	{
		if (Agiotage > 0)
		{
			return false;
		}
		if (!Catalog.GetItemInfo(AwardId).CanOverlap)
		{
			return false;
		}
		return true;
	}

	public bool TryMerge(IMergeableItem mergeableItem)
	{
		if (!(mergeableItem is AwardData awardData))
		{
			return false;
		}
		if (Agiotage <= 0 && Index == awardData.Index && AwardId == awardData.AwardId && AddItemType == awardData.AddItemType && IsOnlyShow == awardData.IsOnlyShow)
		{
			Amount += awardData.Amount;
			IsOutputAddition = IsOutputAddition || awardData.IsOutputAddition;
			OverflowCount += awardData.OverflowCount;
			return true;
		}
		return false;
	}

	public ushort GetOpCode()
	{
		return 14326;
	}
}
}
