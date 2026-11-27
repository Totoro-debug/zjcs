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
public class TipDotTreasureElementTypeHasTipConditionNode : TipDotConditionNodeBase, IHomeTipDotNode
{
	public IHomeTipDotServices Services { get; set; }

	public ElementType ElementType;

	public List<TreasureRedDotEnum> TipType;

	private IDisposable mSettingSub;



	public override void Init()
	{
		base.Init();
		TreasureElementNodeInstanceInfo instanceInfo = GetInstanceInfo<TreasureElementNodeInstanceInfo>();
		if (instanceInfo != null)
		{
			ElementType = instanceInfo.ElementType;
		}
		Services.Player.OnTreasureRefresh += MainPlayer_OnTreasureRefresh;
		Services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
		mSettingSub = Services.SubscribeTreasureSettingUpdate(OnSettingUpdate);
	}

	public override void Release()
	{
		mSettingSub?.Dispose();
		mSettingSub = null;
		if (Services.Player != null)
		{
			Services.Player.OnTreasureRefresh -= MainPlayer_OnTreasureRefresh;
			Services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
		}
		base.Release();
	}

	public override bool CheckShowTip()
	{
		return Services.Player.TreasureRedDot(ElementType, TipType, Services.AutoMergeWhenLevelUp, Services.Player.AwakeAutoMergeEnable);
	}

	private void MainPlayer_OnItemCountUpdate(HomeTipItemData itemData, long preCount, object sourceData)
	{
		if (itemData != null && Services.IsItemEffectTreasurePromote(itemData))
		{
			SetDirty();
		}
	}

	private void MainPlayer_OnTreasureRefresh(HomeTipItemData itemData, bool isNew)
	{
		SetDirty();
	}

	private void OnSettingUpdate()
	{
		SetDirty();
	}

	public override TipDotNodeBase Clone()
	{
		TipDotTreasureElementTypeHasTipConditionNode tipDotTreasureElementTypeHasTipConditionNode = new TipDotTreasureElementTypeHasTipConditionNode();
		tipDotTreasureElementTypeHasTipConditionNode.Fill(this);
		return tipDotTreasureElementTypeHasTipConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotTreasureElementTypeHasTipConditionNode tipDotTreasureElementTypeHasTipConditionNode = source as TipDotTreasureElementTypeHasTipConditionNode;
		if (tipDotTreasureElementTypeHasTipConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotTreasureElementTypeHasTipConditionNode);
	}

	public void Fill(TipDotTreasureElementTypeHasTipConditionNode obj)
	{
		ElementType = obj.ElementType;
		TipType = obj.TipType != null ? new List<TreasureRedDotEnum>(obj.TipType) : null;
	}
}

}
