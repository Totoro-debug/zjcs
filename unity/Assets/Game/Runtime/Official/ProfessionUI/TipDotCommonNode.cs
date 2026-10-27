// Ported from the verified official source; only dependency/serialization adapters differ.
using System;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class TipDotCommonNode : TipDotNodeBase
{
	public override TipDotNodeBase Clone()
	{
		TipDotCommonNode tipDotCommonNode = new TipDotCommonNode();
		tipDotCommonNode.Fill(this);
		return tipDotCommonNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotCommonNode tipDotCommonNode = source as TipDotCommonNode;
		if (tipDotCommonNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotCommonNode);
	}

	public void Fill(TipDotCommonNode obj)
	{
	}
}
}
