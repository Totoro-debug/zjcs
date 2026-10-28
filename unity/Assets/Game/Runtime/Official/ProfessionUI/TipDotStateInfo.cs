// Ported from the verified official source; only dependency/serialization adapters differ.
using System;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public struct TipDotStateInfo
{
	public ETipDotLevelType TipLevel;

	public string Data;

	public override string ToString()
	{
		return string.Format("{0}[ TipLevel:{1} Data:{2} ]", "TipDotStateInfo", TipLevel, Data);
	}
}
}
