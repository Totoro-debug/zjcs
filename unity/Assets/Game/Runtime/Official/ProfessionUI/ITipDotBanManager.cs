// Ported from the verified official source; only dependency/serialization adapters differ.


namespace Zjcs.Official.ProfessionUI
{
public interface ITipDotBanManager
{
	bool IsTipDotBan(TipDotNodeKey nodeKey);

	void ClearTipDotBan();
}
}
