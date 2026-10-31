// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System;

public class NpcProfileNodeInstanceInfo : BaseNodeInstanceInfo
{
	public int NpcId;

	public int NpcProfileId;

	public NpcProfileNodeInstanceInfo(int npcId, int npcProfileId)
	{
		NpcId = npcId;
		NpcProfileId = npcProfileId;
	}

	public override string GetInstanceDesc() { return $"{NpcId} 档案Id: {NpcProfileId}"; }

	public override bool Equals(object obj)
	{
		if (obj is NpcProfileNodeInstanceInfo npcProfileNodeInstanceInfo && NpcId == npcProfileNodeInstanceInfo.NpcId)
		{
			return NpcProfileId == npcProfileNodeInstanceInfo.NpcProfileId;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine<int, int>(NpcId, NpcProfileId);
	}
}

}
