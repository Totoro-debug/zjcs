namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;

public static class TeamInviteSortTypeUtils
{
	public static PropType ToPropType(this TeamInviteSubPanelBase.TeamInviteSortType self)
	{
		switch (self)
		{
		case TeamInviteSubPanelBase.TeamInviteSortType.LightAdd:
			return PropType.LightDamageAdd;
		case TeamInviteSubPanelBase.TeamInviteSortType.LightReduce:
			return PropType.LightDamageReduce;
		case TeamInviteSubPanelBase.TeamInviteSortType.DarkAdd:
			return PropType.DarkDamageAdd;
		case TeamInviteSubPanelBase.TeamInviteSortType.DarkReduce:
			return PropType.DarkDamageReduce;
		case TeamInviteSubPanelBase.TeamInviteSortType.FireAdd:
			return PropType.FireDamageAdd;
		case TeamInviteSubPanelBase.TeamInviteSortType.FireReduce:
			return PropType.FireDamageReduce;
		case TeamInviteSubPanelBase.TeamInviteSortType.WaterAdd:
			return PropType.WaterDamageAdd;
		case TeamInviteSubPanelBase.TeamInviteSortType.WaterReduce:
			return PropType.WaterDamageReduce;
		case TeamInviteSubPanelBase.TeamInviteSortType.WindAdd:
			return PropType.WindDamageAdd;
		case TeamInviteSubPanelBase.TeamInviteSortType.WindReduce:
			return PropType.WindDamageReduce;
		default:
		{
		UnityEngine.Debug.LogWarning("Unhandled original invitation sort value.");
			return PropType.None;
		}
		}
	}

	public static TeamInviteSubPanelBase.TeamInviteSortType ToSortType(this PropType self)
	{
		switch (self)
		{
		case PropType.LightDamageAdd:
			return TeamInviteSubPanelBase.TeamInviteSortType.LightAdd;
		case PropType.LightDamageReduce:
			return TeamInviteSubPanelBase.TeamInviteSortType.LightReduce;
		case PropType.DarkDamageAdd:
			return TeamInviteSubPanelBase.TeamInviteSortType.DarkAdd;
		case PropType.DarkDamageReduce:
			return TeamInviteSubPanelBase.TeamInviteSortType.DarkReduce;
		case PropType.FireDamageAdd:
			return TeamInviteSubPanelBase.TeamInviteSortType.FireAdd;
		case PropType.FireDamageReduce:
			return TeamInviteSubPanelBase.TeamInviteSortType.FireReduce;
		case PropType.WaterDamageAdd:
			return TeamInviteSubPanelBase.TeamInviteSortType.WaterAdd;
		case PropType.WaterDamageReduce:
			return TeamInviteSubPanelBase.TeamInviteSortType.WaterReduce;
		case PropType.WindDamageAdd:
			return TeamInviteSubPanelBase.TeamInviteSortType.WindAdd;
		case PropType.WindDamageReduce:
			return TeamInviteSubPanelBase.TeamInviteSortType.WindReduce;
		default:
		{
		UnityEngine.Debug.LogWarning("Unhandled original invitation sort value.");
			return TeamInviteSubPanelBase.TeamInviteSortType.CombatRating;
		}
		}
	}
}

}
