namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using UnityEngine;

public class TeamInvitePlayerInfoView : MonoBehaviour
{
	private ITeamInviteUiServices services;
	public void Configure(ITeamInviteUiServices value)
	{ services = value; mPlayerIcon?.Configure(value.Icons); mFriendShipInfo.Configure(value); mPropInfo.Configure(value); }

	[SerializeField]
	protected TeamPlayerIcon mPlayerIcon;

	[Header("互斥属性")]
	[SerializeField]
	protected FriendshipInfo mFriendShipInfo;

	[SerializeField]
	protected PropInfo mPropInfo;

	public TeamInvitePlayerInfoView SetPlayerIcon(IProfessionInfoTarget data)
	{
		if ((Object)(object)mPlayerIcon != (Object)null)
		{
			mPlayerIcon.Init(data);
		}
		return this;
	}

	public TeamInvitePlayerInfoView SetShowCollectionImg(bool isShow)
	{
		if ((Object)(object)mPlayerIcon != (Object)null)
		{
			mPlayerIcon.SetShowCollectionImg(isShow);
		}
		return this;
	}

	public TeamInvitePlayerInfoView SetFriendShip(ITeamInviteFriendshipData data)
	{
		HideAllExcludeItem();
		if (data != null)
		{
			mFriendShipInfo.Init(data);
			((Component)mFriendShipInfo).gameObject.SetActive(mPlayerIcon.TargetData.TargetType == ActorTargetType.Player);
		}
		return this;
	}

	public TeamInvitePlayerInfoView SetProp(PropType propType, long propValue)
	{
		HideAllExcludeItem();
		((Component)mPropInfo).gameObject.SetActive(true);
		mPropInfo.Refresh(propType, propValue);
		return this;
	}

	public void HideAllExcludeItem()
	{
		((Component)mFriendShipInfo).gameObject.SetActive(false);
		((Component)mPropInfo).gameObject.SetActive(false);
	}
}

}
