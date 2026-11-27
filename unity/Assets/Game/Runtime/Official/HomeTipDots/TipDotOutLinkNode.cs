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
public class TipDotOutLinkNode : TipDotNodeBase
{
	[Header("外接节点")]
	public TipDotGraphNodeSelector mNodeSelector;
	public TipDotNodeKey LinkNodeKey => mNodeSelector.GetTipDotNodeKey();

	public override TipDotNodeBase Clone()
	{
		TipDotOutLinkNode tipDotOutLinkNode = new TipDotOutLinkNode();
		tipDotOutLinkNode.Fill(this);
		return tipDotOutLinkNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotOutLinkNode tipDotOutLinkNode = source as TipDotOutLinkNode;
		if (tipDotOutLinkNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotOutLinkNode);
	}

	public void Fill(TipDotOutLinkNode obj)
	{
		mNodeSelector = obj.mNodeSelector?.Clone();
	}
}

}
