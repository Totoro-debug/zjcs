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
public class TipDotLinkTreasureElementItemNode : TipDotDynamicLinkNodeBase, IHomeTipDotNode
{
	public IHomeTipDotServices Services { get; set; }

	public List<ElementType> Elements;

	public override void Init()
	{
		base.Init();
		InitLinkNode();
	}

	public override void InitLinkNode()
	{
		if (Services.Player == null)
		{
			return;
		}
		List<TipDotNodeKey> list = new List<TipDotNodeKey>();
		foreach (ElementType element in Elements)
		{
			list.Add(new TipDotNodeKey(LinkNodeKey.GraphName, LinkNodeKey.NodeName, new TreasureElementNodeInstanceInfo(element)));
		}
		((IDynamicTipDotNodeManager)NodeManager).ConnectNodes(NodeKey, list);
	}

	public override TipDotNodeBase Clone()
	{
		TipDotLinkTreasureElementItemNode tipDotLinkTreasureElementItemNode = new TipDotLinkTreasureElementItemNode();
		tipDotLinkTreasureElementItemNode.Fill(this);
		return tipDotLinkTreasureElementItemNode;
	}

	public override void Fill(TipDotNodeBase source)
	{
		TipDotLinkTreasureElementItemNode tipDotLinkTreasureElementItemNode = source as TipDotLinkTreasureElementItemNode;
		if (tipDotLinkTreasureElementItemNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotLinkTreasureElementItemNode);
	}

	public void Fill(TipDotLinkTreasureElementItemNode obj)
	{
		Elements = obj.Elements != null ? new List<ElementType>(obj.Elements) : null;
		mNodeSelector = obj.mNodeSelector?.Clone();
	}
}

}
