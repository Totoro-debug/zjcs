// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System;
using UnityEngine;

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
			throw new System.ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotOutLinkNode);
	}

	public void Fill(TipDotOutLinkNode obj)
	{
		mNodeSelector = obj.mNodeSelector?.Clone();
	}
}

}
