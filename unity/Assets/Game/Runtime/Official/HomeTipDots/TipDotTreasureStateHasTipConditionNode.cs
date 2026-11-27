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
public class TipDotTreasureStateHasTipConditionNode : TipDotConditionNodeBase, IHomeTipDotNode
{
	public IHomeTipDotServices Services { get; set; }

	public List<TreasureRedDotEnum> mRedDots;

	private IDisposable mSettingSub;



	public override void Init()
	{
		base.Init();
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
		return Services.Player.AnyTreasureRedDot(mRedDots, Services.AutoMergeWhenLevelUp, Services.Player.AwakeAutoMergeEnable);
	}

	protected void MainPlayer_OnItemCountUpdate(HomeTipItemData itemData, long preCount, object sourceData)
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
		TipDotTreasureStateHasTipConditionNode tipDotTreasureStateHasTipConditionNode = new TipDotTreasureStateHasTipConditionNode();
		tipDotTreasureStateHasTipConditionNode.Fill(this);
		return tipDotTreasureStateHasTipConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotTreasureStateHasTipConditionNode tipDotTreasureStateHasTipConditionNode = source as TipDotTreasureStateHasTipConditionNode;
		if (tipDotTreasureStateHasTipConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotTreasureStateHasTipConditionNode);
	}

	public void Fill(TipDotTreasureStateHasTipConditionNode obj)
	{
		mRedDots = obj.mRedDots != null ? new List<TreasureRedDotEnum>(obj.mRedDots) : null;
	}
}

}
