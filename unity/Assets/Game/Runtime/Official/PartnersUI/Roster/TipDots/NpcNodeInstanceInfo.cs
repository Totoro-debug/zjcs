// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System;

public class NpcNodeInstanceInfo : BaseNodeInstanceInfo
{
	public int NpcId;

	public NpcNodeInstanceInfo(int npcId)
	{
		NpcId = npcId;
	}

	public override string GetInstanceDesc() { return NpcId.ToString(); }

	public override bool Equals(object obj)
	{
		if (obj is NpcNodeInstanceInfo npcNodeInstanceInfo)
		{
			return NpcId == npcNodeInstanceInfo.NpcId;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine<int>(NpcId);
	}
}

}
