// Selected official ActivityDomainBossTeamButton; online team operations use the local team adapter.
using Zjcs.Official.WorldUI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.ActivityBattleUI
{
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ActivityDomainBossTeamButton : WorldInitializedUI
{
	[SerializeField]
	private Button mTeamInviteBtn;

	[SerializeField]
	private MultiLanguageText mTeamInviteText;

	private IActivityBattleTeam mTeamTarget;

	public void Init(IActivityBattleTeam teamTarget)
	{
		InitWrapper(() =>
		{
			mTeamTarget = teamTarget ?? throw new ArgumentNullException(nameof(teamTarget));
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mTeamTarget.Changed += Team_OnTeamUpdate;
		((UnityEvent)mTeamInviteBtn.onClick).AddListener((UnityAction)OnClickTeamInviteBtn);
		RefreshTeamMemberCount();
	}

	protected override void OnDisableAndInited()
	{
		mTeamTarget.Changed -= Team_OnTeamUpdate;
		((UnityEvent)mTeamInviteBtn.onClick).RemoveListener((UnityAction)OnClickTeamInviteBtn);
		base.OnDisableAndInited();
	}

	private void Team_OnTeamUpdate()
	{
		RefreshTeamMemberCount();
	}

	private void RefreshTeamMemberCount()
	{
		int teamMaxPlayer = mTeamTarget.MaximumMemberCount;
		int num = mTeamTarget.MemberCount;
		mTeamInviteText.Format(num, teamMaxPlayer);
	}

	private void OnClickTeamInviteBtn()
	{
		mTeamTarget.OpenInvitation();
	}
}
}
