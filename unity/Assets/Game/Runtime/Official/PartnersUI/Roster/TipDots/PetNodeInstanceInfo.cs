// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System;

public class PetNodeInstanceInfo : BaseNodeInstanceInfo
{
	public IPartnerPanelTipDotServices Services { get; set; }
	public long PetIndex;

	public PetNodeInstanceInfo(long petId)
	{
		PetIndex = petId;
	}

	public override string GetInstanceDesc()
	{
		return GetPetName() ?? "";
	}

	protected string GetPetName()
	{
		if (Services.Player == null || !Services.Player.PetManager.TryGetValue(PetIndex, out var obj) || obj == null)
		{
			return PetIndex.ToString();
		}
		PartnerPanelPetInfo petInfo = Services.GetPet(obj.ClassId);
		if (petInfo == null)
		{
			return PetIndex.ToString();
		}
		return petInfo.ItemInfo.Name;
	}

	public override bool Equals(object obj)
	{
		if (obj is PetNodeInstanceInfo petNodeInstanceInfo)
		{
			return PetIndex == petNodeInstanceInfo.PetIndex;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine<long>(PetIndex);
	}
}

}
