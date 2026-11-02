// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System;

public class PetAbilityNodeInstanceInfo : PetNodeInstanceInfo
{
	public IPartnerPanelTipDotServices Services { get; set; }
	public int PageIndex;

	public PetAbilityNodeInstanceInfo(long petIndex, int pageIndex)
		: base(petIndex)
	{
		PageIndex = pageIndex;
	}

	public override string GetInstanceDesc()
	{
		return $"{GetPetName()} 能力页： {PageIndex}";
	}

	public override bool Equals(object obj)
	{
		if (obj is PetAbilityNodeInstanceInfo petAbilityNodeInstanceInfo && PetIndex == petAbilityNodeInstanceInfo.PetIndex)
		{
			return PageIndex == petAbilityNodeInstanceInfo.PageIndex;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine<long, int>(PetIndex, PageIndex);
	}
}

}
