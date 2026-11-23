// Selected original Bag condition/identity body; explicit local owners.
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
namespace Zjcs.Official.BagTipDots
{
[Serializable]
public class TipDotBagCanUseItemConditionNode : TipDotConditionNodeBase, IBagTipDotNode
{
	public IBagTipDotServices Services { get; set; }

	public RedDotType Type;
	public List<ItemType> CheckTypes;

	public bool ShowNum;

	private IDisposable mEventDisposable;

	public override void Init()
	{
		base.Init();
		Services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		mEventDisposable = default;
		mEventDisposable = Services.SubscribeEnterNewDay(OnEnterNewDay);
	}

	public override void Release()
	{
		mEventDisposable.Dispose();
		if (Services.Player != null)
		{
			Services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		base.Release();
	}

	private void MainPlayer_OnItemCountUpdate(BagTipItem itemData, long preCount, BagTipAward sourceData)
	{
		if (itemData != null && !TipDotBagUtils.IsExcludedFromShowCanUseRedDot(Services.Settings, itemData.Info.ItemType))
		{
			SetDirty();
		}
	}

	private void OnEnterNewDay()
	{
		foreach (ItemType item in Services.Settings.NewDayRefreshRedDotItemType)
		{
			if (Services.Player.FindItemsByType(item).Count > 0)
			{
				SetDirty();
				break;
			}
		}
	}

	public override bool CheckShowTip()
	{
		long num = 0L;
		foreach (ItemType checkType in CheckTypes)
		{
			IReadOnlyCollection<BagTipItem> readOnlyCollection = Services.Player.FindItemsByType(checkType);
			if (readOnlyCollection.Count == 0)
			{
				continue;
			}
			foreach (BagTipItem item in readOnlyCollection)
			{
				if (Services.Player.ShowBagCanUseItemRedDot(item) && Services.Player.GetItemRedDotType(item) == Type)
				{
					if (Type == RedDotType.RedDot)
					{
						num++;
					}
					else if (Type == RedDotType.BlueDot)
					{
						num += Services.Player.GetCanUseBlueDotCount(item);
					}
				}
			}
		}
		if (ShowNum)
		{
			TipDotInfo.Data = num.ToString();
		}
		return num > 0;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotBagCanUseItemConditionNode tipDotBagCanUseItemConditionNode = new TipDotBagCanUseItemConditionNode();
		tipDotBagCanUseItemConditionNode.Fill(this);
		return tipDotBagCanUseItemConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotBagCanUseItemConditionNode tipDotBagCanUseItemConditionNode = source as TipDotBagCanUseItemConditionNode;
		if (tipDotBagCanUseItemConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotBagCanUseItemConditionNode);
	}

	public void Fill(TipDotBagCanUseItemConditionNode obj)
	{
		Type = obj.Type;
		CheckTypes = obj.CheckTypes == null ? null : new List<ItemType>(obj.CheckTypes);
		ShowNum = obj.ShowNum;
	}
}

}
