// Selected original NpcPanel graph closure; see partner-panel-native-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System;

public class PetSkillNodeInstanceInfo : PetNodeInstanceInfo
{
	public IPartnerPanelTipDotServices Services { get; set; }
	public int SkillId;

	public PetSkillNodeInstanceInfo(long petIndex, int skillIndex)
		: base(petIndex)
	{
		SkillId = skillIndex;
	}

	public override string GetInstanceDesc()
	{
		return GetPetName() + " 技能： " + Services.ItemName(SkillId);
	}

	public override bool Equals(object obj)
	{
		if (obj is PetSkillNodeInstanceInfo petSkillNodeInstanceInfo && PetIndex == petSkillNodeInstanceInfo.PetIndex)
		{
			return SkillId == petSkillNodeInstanceInfo.SkillId;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine<long, int>(PetIndex, SkillId);
	}
}

}
