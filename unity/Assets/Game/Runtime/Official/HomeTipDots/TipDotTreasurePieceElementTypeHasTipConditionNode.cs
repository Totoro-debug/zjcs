// Selected original Home TipDot condition body; explicit local ports.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeTipDots
{
[Serializable]
public class TipDotTreasurePieceElementTypeHasTipConditionNode : TipDotConditionNodeBase, IHomeTipDotNode
{
	public IHomeTipDotServices Services { get; set; }

	public ElementType ElementType;

	public override void Init()
	{
		base.Init();
		TreasureElementNodeInstanceInfo instanceInfo = GetInstanceInfo<TreasureElementNodeInstanceInfo>();
		if (instanceInfo != null)
		{
			ElementType = instanceInfo.ElementType;
		}
		Services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
	}

	public override void Release()
	{
		if (Services.Player != null)
		{
			Services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		base.Release();
	}

	protected void MainPlayer_OnItemCountUpdate(HomeTipItemData itemData, long preCount, object sourceData)
	{
		if (itemData != null && Services.IsItemEffectTreasurePromote(itemData))
		{
			SetDirty();
		}
	}

	public override bool CheckShowTip()
	{
		return Services.Player.TreasurePieceRedDot(ElementType);
	}

	public override TipDotNodeBase Clone()
	{
		TipDotTreasurePieceElementTypeHasTipConditionNode tipDotTreasurePieceElementTypeHasTipConditionNode = new TipDotTreasurePieceElementTypeHasTipConditionNode();
		tipDotTreasurePieceElementTypeHasTipConditionNode.Fill(this);
		return tipDotTreasurePieceElementTypeHasTipConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotTreasurePieceElementTypeHasTipConditionNode tipDotTreasurePieceElementTypeHasTipConditionNode = source as TipDotTreasurePieceElementTypeHasTipConditionNode;
		if (tipDotTreasurePieceElementTypeHasTipConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotTreasurePieceElementTypeHasTipConditionNode);
	}

	public void Fill(TipDotTreasurePieceElementTypeHasTipConditionNode obj)
	{
		ElementType = obj.ElementType;
	}
}

}
