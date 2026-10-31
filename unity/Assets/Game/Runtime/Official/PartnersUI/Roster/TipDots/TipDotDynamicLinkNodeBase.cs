// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System;

[Serializable]
public abstract class TipDotDynamicLinkNodeBase : TipDotOutLinkNode
{
	public override void Init()
	{
		base.Init();
		IPartnerDynamicTipDotNodeManager nodeManager = (IPartnerDynamicTipDotNodeManager)NodeManager;
		nodeManager.OnAddNode = (Action<TipDotNodeKey>)Delegate.Combine(nodeManager.OnAddNode, new Action<TipDotNodeKey>(OnAddNode));
		IPartnerDynamicTipDotNodeManager nodeManager2 = (IPartnerDynamicTipDotNodeManager)NodeManager;
		nodeManager2.OnReduceNode = (Action<TipDotNodeKey>)Delegate.Combine(nodeManager2.OnReduceNode, new Action<TipDotNodeKey>(OnReduceNode));
	}

	public override void Release()
	{
		IPartnerDynamicTipDotNodeManager nodeManager = (IPartnerDynamicTipDotNodeManager)NodeManager;
		nodeManager.OnAddNode = (Action<TipDotNodeKey>)Delegate.Remove(nodeManager.OnAddNode, new Action<TipDotNodeKey>(OnAddNode));
		IPartnerDynamicTipDotNodeManager nodeManager2 = (IPartnerDynamicTipDotNodeManager)NodeManager;
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
