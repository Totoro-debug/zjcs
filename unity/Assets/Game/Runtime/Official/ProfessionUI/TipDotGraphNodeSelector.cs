// Ported from the verified official source; only dependency/serialization adapters differ.
using Object = UnityEngine.Object;
using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class TipDotGraphNodeSelector
{
	[SerializeField]
	public TipDotGraphNode GraphNode;

	public TipDotNodeKey GetTipDotNodeKey()
	{
		if ((Object)(object)GraphNode == (Object)null)
		{
			return default;
		}
		return new TipDotNodeKey(((Object)GraphNode.graph).name, GraphNode.TipDotNodeId.ToString(), BaseNodeInstanceInfo.kDefault);
	}

	public virtual TipDotGraphNodeSelector Clone()
	{
		TipDotGraphNodeSelector tipDotGraphNodeSelector = new TipDotGraphNodeSelector();
		tipDotGraphNodeSelector.Fill(this);
		return tipDotGraphNodeSelector;
	}

	public virtual void Fill(TipDotGraphNodeSelector obj)
	{
		GraphNode = obj.GraphNode;
	}
}
}
