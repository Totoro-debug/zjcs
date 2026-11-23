// Selected original Bag condition/identity body; explicit local owners.
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
namespace Zjcs.Official.BagTipDots
{
[Serializable]
public class TipDotCanRecycleItemConditionNode : TipDotConditionNodeBase, IBagTipDotNode
{
	public IBagTipDotServices Services { get; set; }

	public override void Init()
	{
		base.Init();
		Services.Player.OnNpcFriendshipLevelUpdate += MainPlayer_OnNpcFriendshipLevelUpdate;
		Services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnNpcFriendshipLevelUpdate -= MainPlayer_OnNpcFriendshipLevelUpdate;
			Services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		base.Release();
	}

	private void MainPlayer_OnNpcFriendshipLevelUpdate(int npcClassId, int fromLevel, int toLevel)
	{
		SetDirty();
	}

	private void MainPlayer_OnItemCountUpdate(BagTipItem itemData, long preCount, BagTipAward sourceData)
	{
		if (sourceData == null)
		{
			UnityEngine.Debug.LogWarning("有奖励来源为空！请检查！");
		}
		else if (Services.ItemTagKeys.Contains(itemData.Info.ClassId) || sourceData.SourceOperate == SourceOperate.GiftRecycle)
		{
			SetDirty();
		}
	}

	public override bool CheckShowTip()
	{
		List<BagTipItem> canRecycleGift = Services.Player.GetCanRecycleGift();
		canRecycleGift.AddRange(Services.Player.GetCanRecycleDoorkey());
		return canRecycleGift.Count > 0;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotCanRecycleItemConditionNode tipDotCanRecycleItemConditionNode = new TipDotCanRecycleItemConditionNode();
		tipDotCanRecycleItemConditionNode.Fill(this);
		return tipDotCanRecycleItemConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotCanRecycleItemConditionNode tipDotCanRecycleItemConditionNode = source as TipDotCanRecycleItemConditionNode;
		if (tipDotCanRecycleItemConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotCanRecycleItemConditionNode);
	}

	public void Fill(TipDotCanRecycleItemConditionNode obj)
	{
	}
}

}
