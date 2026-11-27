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
public abstract class TipDotDynamicLinkNodeBase : TipDotOutLinkNode
{
	public override void Init()
	{
		base.Init();
		IDynamicTipDotNodeManager nodeManager = (IDynamicTipDotNodeManager)NodeManager;
		nodeManager.OnAddNode = (Action<TipDotNodeKey>)Delegate.Combine(nodeManager.OnAddNode, new Action<TipDotNodeKey>(OnAddNode));
		IDynamicTipDotNodeManager nodeManager2 = (IDynamicTipDotNodeManager)NodeManager;
		nodeManager2.OnReduceNode = (Action<TipDotNodeKey>)Delegate.Combine(nodeManager2.OnReduceNode, new Action<TipDotNodeKey>(OnReduceNode));
	}

	public override void Release()
	{
		IDynamicTipDotNodeManager nodeManager = (IDynamicTipDotNodeManager)NodeManager;
		nodeManager.OnAddNode = (Action<TipDotNodeKey>)Delegate.Remove(nodeManager.OnAddNode, new Action<TipDotNodeKey>(OnAddNode));
		IDynamicTipDotNodeManager nodeManager2 = (IDynamicTipDotNodeManager)NodeManager;
		nodeManager2.OnReduceNode = (Action<TipDotNodeKey>)Delegate.Remove(nodeManager2.OnReduceNode, new Action<TipDotNodeKey>(OnReduceNode));
		base.Release();
	}

	public abstract void InitLinkNode();

	protected virtual void OnAddNode(TipDotNodeKey key)
	{
	}

	protected virtual void OnReduceNode(TipDotNodeKey key)
	{
	}
}

}
